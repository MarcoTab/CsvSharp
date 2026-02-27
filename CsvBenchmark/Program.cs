using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

using SharpReader = CsvSharp.CsvReader;
using SharpWriter = CsvSharp.CsvWriter;
using SharpRecord = CsvSharp.CsvRecord;

using HelperReader = CsvHelper.CsvReader;
using HelperWriter = CsvHelper.CsvWriter;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run -c Release -- <input.csv> <output_dir> <stress_rows>");
            return;
        }

        string inputPath = args[0];
        string outputDir = args[1];
        int stressRows = int.Parse(args[2]);

        Directory.CreateDirectory(outputDir);

        string sharpOutMany = Path.Combine(outputDir, "csvsharp_many.csv");
        string helperOutMany = Path.Combine(outputDir, "csvhelper_many.csv");
        string sharpOutOne = Path.Combine(outputDir, "csvsharp_one.csv");
        string helperOutOne = Path.Combine(outputDir, "csvhelper_one.csv");

        Console.WriteLine("=== CSV BENCHMARK ===");
        Console.WriteLine($"Input file : {inputPath}");
        Console.WriteLine($"Output dir : {outputDir}");
        Console.WriteLine($"Stress rows: {stressRows:N0}\n");

        BenchmarkRead(inputPath);
        CompareReads(inputPath);

        BenchmarkWriteSingle(sharpOutOne, helperOutOne);
        BenchmarkWriteMany(sharpOutMany, helperOutMany, stressRows);

        DiffFiles(sharpOutMany, helperOutMany);

        Console.WriteLine("DONE.");
    }

    // --------------------------------------------------
    // READ BENCHMARKS (streaming, row-by-row)
    // --------------------------------------------------

    static void BenchmarkRead(string path)
    {
        Benchmark("CsvSharp READ", () =>
        {
            using var fs = File.OpenRead(path);
            using var reader = new SharpReader(fs);
            while (reader.Read() is SharpRecord rec)
            {
                // simulate minimal processing
                for (int i = 0; i < rec.Count; i++) _ = rec[i];
            }
        });

        Benchmark("CsvHelper READ", () =>
        {
            using var sr = new StreamReader(path, Encoding.UTF8);
            using var reader = new HelperReader(sr, CultureInfo.InvariantCulture);
            while (reader.Read())
            {
                for (int i = 0; i < reader.Parser.Count; i++) _ = reader.GetField(i);
            }
        });

        Console.WriteLine();
    }

    static void CompareReads(string path)
    {
        Console.WriteLine("=== Compare READ outputs ===");

        using var fsSharp = File.OpenRead(path);
        using var fsHelper = File.OpenRead(path);

        using var sharp = new SharpReader(fsSharp);
        using var helperSr = new StreamReader(fsHelper, Encoding.UTF8);
        using var helper = new HelperReader(helperSr, CultureInfo.InvariantCulture);

        int row = 0;

        while (true)
        {
            var sharpRec = sharp.Read();
            bool helperHasRow = helper.Read();

            if (sharpRec == null && !helperHasRow)
                break;

            if (sharpRec == null || !helperHasRow)
                throw new Exception($"Row count mismatch at row {row}");

            var helperRec = helper.Parser.Record
                ?? throw new Exception($"CsvHelper returned null record at row {row}");

            if (sharpRec.Count != helperRec.Length)
                throw new Exception($"Field count mismatch at row {row}");

            for (int i = 0; i < sharpRec.Count; i++)
            {
                if (sharpRec[i] != helperRec[i])
                    throw new Exception($"Mismatch at row {row}, col {i}");
            }

            row++;
        }

        Console.WriteLine("Read comparison: OK\n");
    }

    // --------------------------------------------------
    // WRITE BENCHMARKS (streaming, to files)
    // --------------------------------------------------

    static void BenchmarkWriteSingle(string sharpPath, string helperPath)
    {
        Console.WriteLine("=== Write ONE row ===");

        var record = new SharpRecord(new[] { "a", "b", "c", "d", "e" });

        Benchmark("CsvSharp WRITE (1)", () =>
        {
            using var fs = File.Create(sharpPath);
            using var writer = new SharpWriter(fs);
            writer.Write(record);
            writer.Flush();
        });

        Benchmark("CsvHelper WRITE (1)", () =>
        {
            using var fs = File.Create(helperPath);
            using var sw = new StreamWriter(fs, Encoding.UTF8);
            using var writer = new HelperWriter(sw, CultureInfo.InvariantCulture);
            foreach (var field in record)
                writer.WriteField(field);
            writer.NextRecord();
            sw.Flush();
        });

        Console.WriteLine();
    }

    static void BenchmarkWriteMany(string sharpPath, string helperPath, int count)
    {
        Console.WriteLine("=== Write MANY rows ===");

        var record = new SharpRecord(new[] { "1", "2", "3", "4", "5" });

        Benchmark("CsvSharp WRITE (many)", () =>
        {
            using var fs = File.Create(sharpPath);
            using var writer = new SharpWriter(fs);
            for (int i = 0; i < count; i++)
                writer.Write(record);
            writer.Flush();
        });

        Benchmark("CsvHelper WRITE (many)", () =>
        {
            using var fs = File.Create(helperPath);
            using var sw = new StreamWriter(fs, Encoding.UTF8);
            using var writer = new HelperWriter(sw, CultureInfo.InvariantCulture);
            for (int i = 0; i < count; i++)
            {
                foreach (var field in record)
                    writer.WriteField(field);
                writer.NextRecord();
            }
            sw.Flush();
        });

        Console.WriteLine();
    }

    // --------------------------------------------------
    // FILE DIFF (byte-by-byte)
    // --------------------------------------------------

    static void DiffFiles(string pathA, string pathB)
    {
        Console.WriteLine("=== Diff output files ===");

        using var a = File.OpenRead(pathA);
        using var b = File.OpenRead(pathB);

        long pos = 0;
        while (true)
        {
            int ba = a.ReadByte();
            int bb = b.ReadByte();

            if (ba != bb)
                throw new Exception($"Files differ at byte {pos} ({pathA}: {ba}, {pathB}: {bb})");

            if (ba == -1)
                break;

            pos++;
        }

        Console.WriteLine("File diff: IDENTICAL\n");
    }

    // --------------------------------------------------
    // BENCHMARK UTILITIES
    // --------------------------------------------------

    static void Benchmark(string label, Action action)
    {
        ForceGC();
        long memBefore = GC.GetTotalMemory(true);

        var sw = Stopwatch.StartNew();
        action();
        sw.Stop();

        long memAfter = GC.GetTotalMemory(false);

        Console.WriteLine(
            $"{label,-22} | " +
            $"Time: {sw.Elapsed.TotalMilliseconds,8:N2} ms | " +
            $"Mem: {(memAfter - memBefore) / 1024.0,8:N2} KB");
    }

    static void ForceGC()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}