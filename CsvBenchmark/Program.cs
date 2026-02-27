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
// Entry point — run all benchmark classes
// -----------------------------------------------------------------------
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).RunAll(
    DefaultConfig.Instance.WithOption(ConfigOptions.DisableOptimizationsValidator, true));

// -----------------------------------------------------------------------
// Sample CSV row type used for typed CsvHelper mapping
// -----------------------------------------------------------------------

/// <summary>
/// A generic 5-column row — change to match the shape of your real input file.
/// CsvHelper maps by header name, so the property names must match your CSV headers.
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
    private string _inputPath = "";

    // Path to a real CSV file with headers Col1..Col5.
    // Set via --input argument or change the default here.
    [Params("sample.csv")]
    public string InputFile { get; set; } = "sample.csv";

    [GlobalSetup]
    public void Setup()
    {
        _inputPath = InputFile;

        if (!File.Exists(_inputPath))
        {
            GenerateSampleCsv(_inputPath, rows: 100_000);
            Console.WriteLine($"Generated sample file: {_inputPath}");
        }
    }

    /// <summary>
    /// CsvSharp: streaming read, accessing each field by index.
    /// </summary>
    [Benchmark(Baseline = true)]
    public int CsvSharp_Read()
    {
        int count = 0;
        using var fs = File.OpenRead(_inputPath);
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
    /// CsvHelper: idiomatic usage — typed mapping via GetRecord&lt;CsvRow&gt;.
    /// This is what the CsvHelper docs recommend and what most users do.
    /// Note: this includes reflection/mapping overhead that CsvSharp doesn't have,
    /// because CsvSharp has no mapping layer. That's a fair difference to expose.
    /// </summary>
    [Benchmark]
    public int CsvHelper_Read_Typed()
    {
        int count = 0;
        using var sr = new StreamReader(_inputPath, Encoding.UTF8);
        using var reader = new HelperReader(sr, CultureInfo.InvariantCulture);

        reader.Read();
        reader.ReadHeader();

        while (reader.Read())
        {
            _ = reader.GetRecord<CsvRow>();
            count++;
        }

        return count;
    }

    private static void GenerateSampleCsv(string path, int rows)
    {
        using var sw = new StreamWriter(path, false, Encoding.UTF8);
        sw.WriteLine("Col1,Col2,Col3,Col4,Col5");
        for (int i = 0; i < rows; i++)
            sw.WriteLine($"value{i},text{i},data{i},field{i},item{i}");
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

    [Params(1, 10_000, 100_000)]
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