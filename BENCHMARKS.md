# Benchmarks

Benchmarks compare CsvSharp against [CsvHelper](https://joshclose.github.io/CsvHelper/) (v33.1.0) using [BenchmarkDotNet](https://benchmarkdotnet.org) (v0.15.8).

**Environment:** AMD Ryzen 7 7800X3D 4.20GHz, Windows 11, .NET 10.0.3

## Methodology

**Read** is a parser-to-parser comparison. Both libraries read each field by index with no typed mapping, using CsvHelper's `reader.Parser` directly. This isolates raw parsing cost without CsvHelper's reflection/mapping overhead.

**Write** compares CsvSharp's `Write(CsvRecord)` against CsvHelper's idiomatic `WriteRecord<T>()` with a typed object. These are not the same abstraction level — CsvHelper includes one-time reflection-based mapping setup that CsvSharp doesn't have. Both write to `Stream.Null` to measure serialization cost only, with no I/O.

## Results

### Read (~20 MB input file)

| Method                | Mean     | StdDev   | Ratio | Allocated | Alloc Ratio |
|---------------------- |---------:|---------:|------:|----------:|------------:|
| CsvSharp_Read         | 83.84 ms | 5.874 ms |  1.00 |  90.21 MB |        1.00 |
| CsvHelper_Read_Parser | 87.07 ms | 2.839 ms |  1.04 |  72.73 MB |        0.81 |

CsvSharp is ~4% faster but allocates ~24% more per run. CsvHelper's parser reuses internal buffers more aggressively; CsvSharp allocates a fresh `List<string>` and `CsvRecord` object for every row.

### Write

| Method                | Rows      | Mean         | StdDev      | Ratio | Allocated | Alloc Ratio |
|---------------------- |---------- |-------------:|------------:|------:|----------:|------------:|
| CsvSharp_Write        | 10,000    | 860.5 μs     | 17.66 μs    |  1.00 |   2.83 MB |        1.00 |
| CsvHelper_Write_Typed | 10,000    | 3,184.6 μs   | 147.39 μs   |  3.70 |   2.06 MB |        0.73 |
| CsvSharp_Write        | 1,000,000 | 86,336.1 μs  | 4,184.29 μs |  1.00 | 282.29 MB |        1.00 |
| CsvHelper_Write_Typed | 1,000,000 | 180,525.4 μs | 4,423.73 μs |  2.10 | 128.06 MB |        0.45 |

CsvSharp is ~2-3.7x faster, but uses ~2.2x more memory at scale. CsvSharp allocates a new `StringBuilder` per row; CsvHelper reuses its write buffer across rows, which pays off significantly at high row counts.
