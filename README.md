# CsvSharp

A minimal, zero-dependency CSV parsing and writing library for .NET. Built as a learning project to explore C# fundamentals — streaming I/O, interface design, and benchmarking.

## Features

- Streaming row-by-row reads with no full-file buffering
- RFC 4180 compliant by default — handles quoted fields, escaped quotes (`""`), and embedded newlines
- CRLF, LF, and bare CR line endings all supported
- Strict mode (default) throws descriptive exceptions on malformed input; lenient mode silently recovers
- Configurable delimiter, quote character, and encoding via `CsvDialect` and `CsvConfiguration`
- Optional quote-all-fields mode for writing
- Clean interfaces (`ICsvReader`, `ICsvWriter`) for easy testing and mocking

## Usage

### Reading

```csharp
using var stream = File.OpenRead("data.csv");
using var reader = new CsvReader(stream);

while (reader.Read() is CsvRecord record)
{
    Console.WriteLine(string.Join(", ", record));
}
```

Or read all records at once:

```csharp
using var stream = File.OpenRead("data.csv");
using var reader = new CsvReader(stream);

foreach (var record in reader.ReadAll())
{
    Console.WriteLine(record[0]);
}
```

### Writing

```csharp
using var stream = File.Create("output.csv");
using var writer = new CsvWriter(stream);

writer.Write(new CsvRecord(["Alice", "30", "Engineer"]));
writer.Write(new CsvRecord(["Bob", "25", "Designer"]));
writer.Flush();
```

With a header row:

```csharp
var header = new CsvRecord(["Name", "Age", "Role"]);

using var stream = File.Create("output.csv");
using var writer = new CsvWriter(stream, header: header);

writer.WriteAll([
    new CsvRecord(["Alice", "30", "Engineer"]),
    new CsvRecord(["Bob", "25", "Designer"]),
]);
```

### Configuration

```csharp
var config = new CsvConfiguration
{
    Dialect = new CsvDialect { Delimiter = ';', Quote = '\'' },
    Strict = false,        // don't throw on malformed input
    QuoteAllFields = true, // always quote every field when writing
    Encoding = Encoding.Latin1,
};

using var reader = new CsvReader(stream, config);
```

### Error handling

In strict mode (the default), malformed input throws a `CsvParsingException`, which includes the zero-based record index where the error occurred:

```csharp
try
{
    while (reader.Read() is CsvRecord record) { ... }
}
catch (CsvParsingException ex)
{
    Console.WriteLine($"Parse error at record {ex.RecordIndex}: {ex.Message}");
}
```

## Project structure

```
CsvSharp/           Core library
CsvSharp.Tests/     xUnit test suite
CsvBenchmark/       BenchmarkDotNet benchmarks comparing CsvSharp to CsvHelper
```

## Benchmarks

Benchmarks are in `CsvBenchmark/` and use [BenchmarkDotNet](https://benchmarkdotnet.org). See [BENCHMARKS.md](BENCHMARKS.md) for full results and methodology.

```bash
dotnet run -c Release --project CsvBenchmark                                         # auto-generates a sample file
```

[Open issue](https://github.com/MarcoTab/CsvSharp/issues/1): can't pass in an arbitrary CSV file for benchmarking. In the meantime, find all occurences of `sample.csv` in `Program.cs` and replace them with the absolute path to a CSV file of your choice.

> **Note:** input paths must be absolute. BenchmarkDotNet spawns a child process with a different working directory, so relative paths won't resolve correctly.
