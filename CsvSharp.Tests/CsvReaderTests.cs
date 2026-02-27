using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace CsvSharp.Tests
{
    public class CsvReaderConstructorTests
    {
        [Fact]
        public void Constructor_Throws_If_Stream_Is_Not_Readable()
        {
            var stream = new MemoryStream();
            stream.Close(); // makes CanRead == false

            Assert.Throws<ArgumentException>(() =>
            {
                _ = new CsvReader(stream);
            });
        }

        [Fact]
        public void Constructor_Allows_Null_Configuration()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("a,b\n"));

            var reader = new CsvReader(stream, null);

            var record = reader.Read();
            Assert.NotNull(record);
        }

        [Fact]
        public void Constructor_Uses_Custom_Encoding()
        {
            var text = "á,é\n";
            var encoding = Encoding.GetEncoding("ISO-8859-1");
            using var stream = new MemoryStream(encoding.GetBytes(text));

            var config = new CsvConfiguration { Encoding = encoding };
            using var reader = new CsvReader(stream, config);

            var record = reader.Read();

            Assert.Equal(new[] { "á", "é" }, record!.ToArray());
        }

        [Fact]
        public void Dispose_Disposes_Underlying_Stream()
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes("a,b\n"));
            var reader = new CsvReader(stream);

            reader.Dispose();

            Assert.False(stream.CanRead);
        }
    }

    public class CsvReaderReadTests
    {
        private static CsvReader CreateReader(string input, CsvConfiguration? config = null)
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));
            return new CsvReader(stream, config);
        }

        // --------------------
        // Basic parsing
        // --------------------

        [Fact]
        public void Read_Simple_Record()
        {
            using var reader = CreateReader("a,b,c\n");

            var record = reader.Read();

            Assert.NotNull(record);
            Assert.Equal(new[] { "a", "b", "c" }, record!.ToArray());
            Assert.Null(reader.Read());
        }

        [Fact]
        public void Read_Multiple_Records_Sequentially()
        {
            using var reader = CreateReader("a,b\nc,d\n");

            var r1 = reader.Read();
            var r2 = reader.Read();
            var r3 = reader.Read();

            Assert.Equal(new[] { "a", "b" }, r1!.ToArray());
            Assert.Equal(new[] { "c", "d" }, r2!.ToArray());
            Assert.Null(r3);
        }

        // --------------------
        // Empty fields
        // --------------------

        [Fact]
        public void Read_Empty_Fields()
        {
            using var reader = CreateReader("a,,c,\n");

            var record = reader.Read();

            Assert.Equal(new[] { "a", "", "c", "" }, record!.ToArray());
        }

        [Fact]
        public void Read_All_Empty_Fields()
        {
            using var reader = CreateReader(",,\n");

            var record = reader.Read();

            Assert.Equal(new[] { "", "", "" }, record!.ToArray());
        }

        // --------------------
        // Quoted fields
        // --------------------

        [Fact]
        public void Read_Quoted_Field_With_Delimiter()
        {
            using var reader = CreateReader("a,\"b,c\",d\n");

            var record = reader.Read();

            Assert.Equal(new[] { "a", "b,c", "d" }, record!.ToArray());
        }

        [Fact]
        public void Read_Escaped_Quote()
        {
            using var reader = CreateReader("\"a \"\"quoted\"\" value\"\n");

            var record = reader.Read();

            Assert.Equal(new[] { "a \"quoted\" value" }, record!.ToArray());
        }

        // --------------------
        // Newlines
        // --------------------

        [Fact]
        public void Read_Multiline_Quoted_Field()
        {
            using var reader = CreateReader("a,\"line1\nline2\",c\n");

            var record = reader.Read();

            Assert.Equal(new[] { "a", "line1\nline2", "c" }, record!.ToArray());
        }

        [Fact]
        public void Read_CRLF_Line_Endings()
        {
            using var reader = CreateReader("a,b\r\nc,d\r\n");

            var r1 = reader.Read();
            var r2 = reader.Read();

            Assert.Equal(new[] { "a", "b" }, r1!.ToArray());
            Assert.Equal(new[] { "c", "d" }, r2!.ToArray());
        }

        [Fact]
        public void Read_Lone_CR_Line_Endings()
        {
            using var reader = CreateReader("a,b\rc,d\r");

            var r1 = reader.Read();
            var r2 = reader.Read();

            Assert.Equal(new[] { "a", "b" }, r1!.ToArray());
            Assert.Equal(new[] { "c", "d" }, r2!.ToArray());
        }

        // --------------------
        // EOF behavior
        // --------------------

        [Fact]
        public void Read_Last_Record_Without_Newline()
        {
            using var reader = CreateReader("a,b,c");

            var record = reader.Read();

            Assert.Equal(new[] { "a", "b", "c" }, record!.ToArray());
            Assert.Null(reader.Read());
        }

        [Fact]
        public void Read_Last_Record_Without_Newline_With_Preceding_Records()
        {
            using var reader = CreateReader("a,b,c\nd,e,f");

            var record = reader.Read();
            Assert.Equal(new[] { "a", "b", "c"}, record!.ToArray());

            record = reader.Read();
            Assert.Equal(new[] {"d", "e", "f"}, record!.ToArray());

            Assert.Null(reader.Read());
        }

        [Fact]
        public void Read_Empty_File_Returns_Null()
        {
            using var reader = CreateReader("");

            Assert.Null(reader.Read());
        }

        // --------------------
        // Strict mode errors
        // --------------------

        [Fact]
        public void Strict_Throws_On_Unterminated_Quoted_Field()
        {
            Assert.Throws<CsvParsingException>(() =>
            {
                using var reader = CreateReader("\"a,b\n");
                reader.Read();
            });
        }

        [Fact]
        public void Strict_Throws_On_Character_After_Closing_Quote()
        {
            Assert.Throws<CsvParsingException>(() =>
            {
                using var reader = CreateReader("\"abc\"x\n");
                reader.Read();
            });
        }

        [Fact]
        public void Strict_Throws_On_Quote_In_Unquoted_Field()
        {
            Assert.Throws<CsvParsingException>(() =>
            {
                using var reader = CreateReader("ab\"c\n");
                reader.Read();
            });
        }

        // --------------------
        // Non-strict mode
        // --------------------

        [Fact]
        public void NonStrict_Allows_Invalid_Quote()
        {
            var config = new CsvConfiguration { Strict = false };
            using var reader = CreateReader("ab\"c\n", config);

            var record = reader.Read();

            Assert.Equal(new[] { "ab\"c" }, record!.ToArray());
        }
    }

    public class CsvReaderReadAllTests
    {
        private static CsvReader CreateReader(string input)
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));
            return new CsvReader(stream);
        }

        [Fact]
        public void ReadAll_Returns_All_Records()
        {
            using var reader = CreateReader("a,b\nc,d\ne,f\n");

            var records = reader.ReadAll().ToList();

            Assert.Equal(3, records.Count);
            Assert.Equal(new[] { "a", "b" }, records[0].ToArray());
            Assert.Equal(new[] { "c", "d" }, records[1].ToArray());
            Assert.Equal(new[] { "e", "f" }, records[2].ToArray());
        }

        [Fact]
        public void ReadAll_On_Empty_File_Returns_Empty_Enumerable()
        {
            using var reader = CreateReader("");

            var records = reader.ReadAll().ToList();

            Assert.Empty(records);
        }

        [Fact]
        public void ReadAll_Stops_On_Parse_Error()
        {
            using var reader = CreateReader("a,b\n\"unterminated\n");

            var enumerator = reader.ReadAll().GetEnumerator();

            Assert.True(enumerator.MoveNext()); // first record ok

            Assert.Throws<CsvParsingException>(() =>
            {
                enumerator.MoveNext();
            });
        }

        [Fact]
        public void ReadAll_Advances_RecordIndex_Correctly()
        {
            using var reader = CreateReader("a,b\nc,d\n");

            var records = reader.ReadAll().ToList();

            Assert.Equal(2, records.Count);
            Assert.Equal(1, reader.RecordIndex);
        }

        [Fact]
        public void ReadAll_Does_Not_Skip_Records()
        {
            using var reader = CreateReader("1\n2\n3\n");

            var records = reader.ReadAll().ToList();

            Assert.Equal(new[] { "1" }, records[0].ToArray());
            Assert.Equal(new[] { "2" }, records[1].ToArray());
            Assert.Equal(new[] { "3" }, records[2].ToArray());
        }
    }
}