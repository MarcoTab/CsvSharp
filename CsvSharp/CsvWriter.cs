using System.Text;

namespace CsvSharp
{
    /// <summary>
    /// Writes CSV data to a text stream.
    /// </summary>
    public sealed class CsvWriter : ICsvWriter
    {
        private readonly TextWriter _writer;
        private readonly CsvConfiguration _config;

        private CsvDialect _dialect;

        private CsvRecord? _header;
        
        private bool _hasHeader = false;

        private bool _headerWritten;

        private int _recordsWritten;

        public CsvWriter(Stream stream, CsvConfiguration? configuration = null, CsvRecord? header = null){
            if (!stream.CanWrite)
            {
                throw new ArgumentException("Stream is not writeable", nameof(stream));
            }

            _config = configuration ?? new CsvConfiguration();

            _writer = new StreamWriter(stream, _config.Encoding);

            if (_config.HasHeader && header is null)
            {
                throw new ArgumentException("Configuration indicates a header is required but no header was provided", nameof(header));
            }

            _header = header;

            if (_header != null)
                _hasHeader = true;

            _headerWritten = false;

            _dialect = _config.Dialect;

            _recordsWritten = 0;
        }

        public void WriteHeader()
        {
            if (_header != null && !_headerWritten && _hasHeader)
            {
                _writer.WriteLine(GetLineFromCsvRecord(_header));
                _headerWritten = true;
            }
        }

        public void Write(CsvRecord record)
        {
            WriteHeader();
            string line = GetLineFromCsvRecord(record);
            _writer.WriteLine(line);
            _recordsWritten++;
        }

        private string GetLineFromCsvRecord(CsvRecord record)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < record.Count; i++)
            {
                string field = record[i];

                bool shouldQuote = _config.QuoteAllFields
                                    || field.Contains(_dialect.Delimiter)
                                    || field.Contains('\n')
                                    || field.Contains(_dialect.Quote);

                if (shouldQuote)
                {
                    field = field.Replace(_dialect.Quote.ToString(),
                                            new string(_dialect.Quote, 2));

                    sb
                    .Append(_dialect.Quote)
                    .Append(field)
                    .Append(_dialect.Quote);
                }
                else
                {
                    sb.Append(field);
                }

                if (i < record.Count-1)
                    sb.Append(_dialect.Delimiter);
            }

            return sb.ToString();
        }

        public void WriteAll(IEnumerable<CsvRecord> records, bool flush = true)
        {
            WriteHeader();
            foreach (var record in records)
                Write(record);
            
            if (flush)
                Flush();
        }

        public void Flush()
        {
            _writer.Flush();
        }

        public int RecordsWritten => _recordsWritten;

        public void Dispose()
        {
            _writer.Flush();
            _writer.Dispose();
        }
    }
}