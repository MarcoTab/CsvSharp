using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

using SharpReader = CsvSharp.CsvReader;
using SharpWriter = CsvSharp.CsvWriter;
using SharpRecord = CsvSharp.CsvRecord;
using HelperReader = CsvHelper.CsvReader;
using HelperWriter = CsvHelper.CsvWriter;

// -----------------------------------------------------------------------
// Entry point
//
// Usage:
//   dotnet run -c Release                           (auto-generates sample.csv if it doesn't exist. Replace this value with an absolute path to use an arbitrary csv file)
// -----------------------------------------------------------------------

// BenchmarkDotNet runs benchmarks in a child process with a different working
// directory, so all paths must be absolute. We anchor the sample file to the
// assembly directory so it is always found regardless of cwd.
string assemblyDir = AppContext.BaseDirectory;
string samplePath = Path.Combine(assemblyDir, "sample.csv");

// Set the shared input path before BenchmarkDotNet takes over.
// BenchmarkDotNet instantiates benchmark classes itself, so a static
// field is the standard workaround for passing data into benchmarks.
ReadBenchmarks.InputPath = GenerateSampleIfMissing(samplePath, rows: 100_000);

var config = DefaultConfig.Instance
    .WithOption(ConfigOptions.DisableOptimizationsValidator, true);

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).RunAll(config);

static string GenerateSampleIfMissing(string path, int rows)
{
    if (!File.Exists(path))
    {
        Console.WriteLine($"No --input provided. Generating {path} with {rows:N0} rows...");
        using var sw = new StreamWriter(path, false, Encoding.UTF8);
        sw.WriteLine("Col1,Col2,Col3,Col4,Col5");
        for (int i = 0; i < rows; i++)
            sw.WriteLine($"value{i},text{i},data{i},field{i},item{i}");
        Console.WriteLine("Done.\n");
    }
    return path;
}

// -----------------------------------------------------------------------
// Sample CSV row type used for typed CsvHelper mapping
// -----------------------------------------------------------------------

/// <summary>
/// Used by the write benchmark for CsvHelper's WriteRecord&lt;T&gt;().
/// Column count should match the CsvSharp write benchmark record.
/// </summary>
public class CsvRow
{
    public string Col1 { get; set; } = "";
    public string Col2 { get; set; } = "";
    public string Col3 { get; set; } = "";
    public string Col4 { get; set; } = "";
    public string Col5 { get; set; } = "";
}

// -----------------------------------------------------------------------
// READ benchmarks
// -----------------------------------------------------------------------

/// <summary>
/// Compares CsvSharp's raw field-by-field read against CsvHelper's idiomatic
/// typed-mapping read (GetRecord&lt;T&gt;). These are not the same abstraction level,
/// but they represent what a real user of each library would write.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0)]
public class ReadBenchmarks
{
    /// <summary>
    /// Set by the entry point before BenchmarkDotNet runs — see top of file.
    /// BenchmarkDotNet instantiates this class itself so we use a static field
    /// rather than a constructor argument.
    /// </summary>
    public static string InputPath { get; set; } = "sample.csv";

    [GlobalSetup]
    public void Setup()
    {
        if (!File.Exists(InputPath))
            throw new FileNotFoundException($"Input file not found: {InputPath}");
    }

    /// <summary>
    /// CsvSharp: streaming read, accessing each field by index.
    /// </summary>
    [Benchmark(Baseline = true)]
    public int CsvSharp_Read()
    {
        int count = 0;
        using var fs = File.OpenRead(InputPath);
        using var reader = new SharpReader(fs);

        while (reader.Read() is SharpRecord rec)
        {
            // touch every field to prevent the loop from being optimized away
            for (int i = 0; i < rec.Count; i++) _ = rec[i];
            count++;
        }

        return count;
    }

    /// <summary>
    /// CsvHelper: parser-level read via reader.Parser, accessing each field by index.
    /// This is a schema-agnostic path that works with any file, making it a fair
    /// parser-to-parser comparison with CsvSharp. Users who need typed mapping would
    /// use GetRecord&lt;T&gt;(), but that requires a fixed schema and adds reflection
    /// overhead that isn't really CsvHelper's parsing cost.
    /// </summary>
    [Benchmark]
    public int CsvHelper_Read_Parser()
    {
        int count = 0;
        using var sr = new StreamReader(InputPath, Encoding.UTF8);
        using var reader = new HelperReader(sr, CultureInfo.InvariantCulture);

        while (reader.Parser.Read())
        {
            for (int i = 0; i < reader.Parser.Count; i++) _ = reader.Parser[i];
            count++;
        }

        return count;
    }
}

// -----------------------------------------------------------------------
// WRITE benchmarks
// -----------------------------------------------------------------------

/// <summary>
/// Compares CsvSharp's Write(CsvRecord) against CsvHelper's idiomatic
/// WriteRecord&lt;T&gt;. Both write to a MemoryStream to isolate serialization
/// cost from I/O.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0)]
public class WriteBenchmarks
{
    private SharpRecord _sharpRecord = null!;
    private CsvRow _typedRecord = null!;

    [Params(10_000, 1_000_000)]
    public int Rows { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _sharpRecord = new SharpRecord(["value1", "text2", "data3", "field4", "item5"]);
        _typedRecord = new CsvRow
        {
            Col1 = "value1",
            Col2 = "text2",
            Col3 = "data3",
            Col4 = "field4",
            Col5 = "item5"
        };
    }

    /// <summary>
    /// CsvSharp: write N rows using Write(CsvRecord).
    /// </summary>
    [Benchmark(Baseline = true)]
    public void CsvSharp_Write()
    {
        using var writer = new SharpWriter(Stream.Null);

        for (int i = 0; i < Rows; i++)
            writer.Write(_sharpRecord);

        writer.Flush();
    }

    /// <summary>
    /// CsvHelper: idiomatic usage — WriteRecord&lt;T&gt; with a typed object.
    /// This is what the CsvHelper docs recommend.
    /// </summary>
    [Benchmark]
    public void CsvHelper_Write_Typed()
    {
        using var sw = new StreamWriter(Stream.Null, Encoding.UTF8, leaveOpen: true);
        using var writer = new HelperWriter(sw, CultureInfo.InvariantCulture);

        for (int i = 0; i < Rows; i++)
            writer.WriteRecord(_typedRecord);

        sw.Flush();
    }
}

// -----------------------------------------------------------------------
// CORRECTNESS check (not a benchmark — run separately if desired)
// -----------------------------------------------------------------------

/// <summary>
/// Verifies that CsvSharp and CsvHelper produce identical parsed output
/// for the same input file. Compares at the record level, not byte-for-byte,
/// so minor formatting differences (e.g. CRLF vs LF) don't cause false failures.
/// </summary>
public static class CorrectnessCheck
{
    public static void Run(string inputPath)
    {
        Console.WriteLine("=== Correctness check ===");

        using var fsSharp = File.OpenRead(inputPath);
        using var sharp = new SharpReader(fsSharp);

        using var fsHelper = File.OpenRead(inputPath);
        using var sr = new StreamReader(fsHelper, Encoding.UTF8);
        using var helper = new HelperReader(sr, CultureInfo.InvariantCulture);

        int row = 0;
        while (true)
        {
            var sharpRec = sharp.Read();
            bool helperHasRow = helper.Parser.Read();

            if (sharpRec == null && !helperHasRow) break;

            if (sharpRec == null || !helperHasRow)
                throw new Exception($"Row count mismatch at row {row}");

            if (sharpRec.Count != helper.Parser.Count)
                throw new Exception($"Field count mismatch at row {row}: CsvSharp={sharpRec.Count}, CsvHelper={helper.Parser.Count}");

            for (int i = 0; i < sharpRec.Count; i++)
            {
                if (sharpRec[i] != helper.Parser[i])
                    throw new Exception($"Field mismatch at row {row}, col {i}: \"{sharpRec[i]}\" vs \"{helper.Parser[i]}\"");
            }

            row++;
        }

        Console.WriteLine($"Correctness check PASSED — {row} rows matched.\n");
    }
}