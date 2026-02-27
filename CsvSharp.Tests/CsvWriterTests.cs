using System;
using System.IO;
using System.Text;
using System.Linq;
using Xunit;

namespace CsvSharp.Tests
{
    public class CsvWriterTests
    {
        [Fact]
        public void Constructor_Throws_WhenStreamNotWritable()
        {
            using var stream = new MemoryStream(new byte[0], writable: false);
            Assert.Throws<ArgumentException>(() => new CsvWriter(stream));
        }

        [Fact]
        public void Constructor_Throws_WhenHeaderRequiredButMissing()
        {
            using var stream = new MemoryStream();
            var config = new CsvConfiguration { HasHeader = true };
            Assert.Throws<ArgumentException>(() => new CsvWriter(stream, config, null));
        }

        [Fact]
        public void WriteHeader_WritesHeaderOnce()
        {
            using var stream = new MemoryStream();
            var config = new CsvConfiguration {QuoteAllFields = true};
            var header = new CsvRecord(new[] { "A", "B", "C" });
            var writer = new CsvWriter(stream, configuration: config, header: header);
            
            writer.WriteHeader();
            writer.WriteHeader(); // should do nothing second time
            writer.Flush();

            stream.Position = 0;
            using var reader = new StreamReader(stream);
            string line = reader.ReadLine()!;
            Assert.Equal("\"A\",\"B\",\"C\"", line);
        }

        [Fact]
        public void Write_RecordWithoutHeader_WritesSingleLine()
        {
            using var stream = new MemoryStream();
            var writer = new CsvWriter(stream);

            var record = new CsvRecord(new[] { "foo", "bar" });
            writer.Write(record);
            writer.Flush();

            stream.Position = 0;
            using var reader = new StreamReader(stream);
            string line = reader.ReadLine()!;
            Assert.Equal("foo,bar", line);
            Assert.Equal(1, writer.RecordsWritten);
        }

        [Fact]
        public void Write_RecordWithHeader_WritesHeaderAndRecord()
        {
            using var stream = new MemoryStream();
            var config = new CsvConfiguration {QuoteAllFields = false};
            var header = new CsvRecord(new[] { "H1", "H2" });
            var writer = new CsvWriter(stream, header: header);

            var record = new CsvRecord(new[] { "foo", "bar" });
            writer.Write(record);
            writer.Flush();

            stream.Position = 0;
            using var reader = new StreamReader(stream);
            string headerLine = reader.ReadLine()!;
            string recordLine = reader.ReadLine()!;
            
            Assert.Equal("H1,H2", headerLine);
            Assert.Equal("foo,bar", recordLine);
            Assert.Equal(1, writer.RecordsWritten);
        }

        [Fact]
        public void WriteAll_WritesMultipleRecords()
        {
            using var stream = new MemoryStream();
            var writer = new CsvWriter(stream);

            var records = new[]
            {
                new CsvRecord(new[] { "a", "b" }),
                new CsvRecord(new[] { "1", "2" }),
                new CsvRecord(new[] { "x", "y" })
            };

            writer.WriteAll(records);

            stream.Position = 0;
            using var reader = new StreamReader(stream);
            var lines = reader.ReadToEnd().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal(3, lines.Length);
            Assert.Equal("a,b", lines[0]);
            Assert.Equal("1,2", lines[1]);
            Assert.Equal("x,y", lines[2]);
            Assert.Equal(3, writer.RecordsWritten);
        }

        [Fact]
        public void Write_QuotesAndEscapesFields_WhenNecessary()
        {
            using var stream = new MemoryStream();
            var writer = new CsvWriter(stream);

            var record = new CsvRecord(new[] { "hello,world", "line\nbreak", "she said \"hi\"" });
            writer.Write(record);
            writer.Flush();

            stream.Position = 0;
            using var reader = new StreamReader(stream);
            string csv = reader.ReadToEnd();  // read the whole stream

            // The CSVWriter writes a line separator at the end (Environment.NewLine)
            string expected = "\"hello,world\",\"line\nbreak\",\"she said \"\"hi\"\"\"" + Environment.NewLine;

            Assert.Equal(expected, csv);
        }

        [Fact]
        public void Flush_DoesNotThrow()
        {
            using var stream = new MemoryStream();
            var writer = new CsvWriter(stream);
            writer.Flush(); // just ensure it works
        }

        [Fact]
        public void Dispose_FlushesAndDisposes()
        {
            var stream = new MemoryStream();
            var writer = new CsvWriter(stream);

            writer.Dispose();
            Assert.Throws<ObjectDisposedException>(() => writer.Write(new CsvRecord(new[] { "a" })));
        }
    }
}