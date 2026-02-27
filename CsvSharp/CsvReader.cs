using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace CsvSharp
{
    /// <summary>
    /// Reads CSV data from a text stream.
    /// </summary>
    public sealed class CsvReader : ICsvReader
    {
        private readonly TextReader _reader;
        private readonly CsvConfiguration _config;
        private int _recordIndex = -1;

        private ParsingState _state = ParsingState.StartField;

        private CsvDialect _dialect = CsvDialect.Default;

        private List<string> _currentRecord = new List<string>();

        private StringBuilder _currentField = new StringBuilder();

        private bool _anyRead = false;

        private bool _endRecord = false;

        public CsvReader(Stream stream, CsvConfiguration? configuration = null)
        {
            if (!stream.CanRead)
            {
                throw new ArgumentException("Stream is not readable", nameof(stream));
            }

            _config = configuration ?? new CsvConfiguration();

            _reader = new StreamReader(stream, _config.Encoding);

        }

        public CsvRecord? Read()
        {
            ResetForNewRecord();

            int ch;
            while ((ch = _reader.Read()) != -1)
            {
                char c = (char)ch;
                _anyRead = true;

                switch (_state)
                {
                    case ParsingState.StartField:
                        HandleStartField(c);
                        break;

                    case ParsingState.InUnquotedField:
                        HandleUnquotedField(c);
                        break;

                    case ParsingState.InQuotedField:
                        HandleQuotedField(c);
                        break;

                    case ParsingState.AfterQuote:
                        HandleAfterQuote(c);
                        break;
                }

                if (_endRecord)
                    return FinishRecord();
            }

            return FinishOnEOF();
        }

        private void HandleStartField(char c)
        {
            if (c == _dialect.Quote)
            {
                _state = ParsingState.InQuotedField;
            }
            else if (c == _dialect.Delimiter)
            {
                EndField();
            }
            else if (IsNewLine(c))
            {
                ConsumeCRLF(c);
                EndField();
                EndRecord();
            }
            else
            {
                _state = ParsingState.InUnquotedField;
                _currentField.Append(c);
            }
        }

        private void HandleUnquotedField(char c)
        {
            if (c == _dialect.Delimiter)
            {
                EndField();
            }
            else if (IsNewLine(c))
            {
                ConsumeCRLF(c);
                EndField();
                EndRecord();
            }
            else if (c == _dialect.Quote && _config.Strict)
            {
                throw new CsvParsingException(
                    $"Found quote character ({_dialect.Quote}) in invalid state.", 
                    _recordIndex
                );
            }
            else
            {
                _currentField.Append(c);
            }
        }

        private void HandleQuotedField(char c)
        {
            if (c == _dialect.Quote)
            {
                if (_reader.Peek() == _dialect.Quote)
                {
                    _reader.Read(); // escaped quote
                    _currentField.Append(_dialect.Quote);
                }
                else
                {
                    _state = ParsingState.AfterQuote;
                }
            }

            else if (IsNewLine(c))
            {
                ConsumeCRLF(c);
                _currentField.Append('\n');
            }
            else
            {
                _currentField.Append(c);
            }
        }

        private void HandleAfterQuote(char c)
        {
            if (c == _dialect.Delimiter)
            {
                EndField();
                _state = ParsingState.StartField;
            }
            else if (IsNewLine(c))
            {
                ConsumeCRLF(c);
                EndField();
                EndRecord();
            }
            else if (_config.Strict)
            {
                throw new CsvParsingException(
                    $"Found quote character ({_dialect.Quote}) in invalid state."
                    , _recordIndex
                );
            }
            else
            {
                _state = ParsingState.InUnquotedField;
                _currentField.Append(c);
            }
        }

        private void EndField()
        {
            _currentRecord.Add(_currentField.ToString());
            _currentField.Clear();
            _state = ParsingState.StartField;
        }

        private void EndRecord()
        {
            _recordIndex++;
            _endRecord = true;
            _state = ParsingState.StartField;
            _currentField.Clear();
        }

        private CsvRecord FinishRecord()
        {
            _endRecord = false;
            return new CsvRecord(_currentRecord);
        }

        private CsvRecord? FinishOnEOF()
        {
            if (!_anyRead)
                return null;

            if (_state == ParsingState.InQuotedField && _config.Strict)
                throw new CsvParsingException("Unexpected EOF inside quoted field.", _recordIndex);

            EndField();
            EndRecord();
            _recordIndex++;
            return new CsvRecord(_currentRecord);
        }

        private static bool IsNewLine(char c)
        {
            return c == '\n' || c == '\r';
        }

        private void ResetForNewRecord()
        {
            _state = ParsingState.StartField;

            _currentRecord.Clear();
            _currentField.Clear();

            _endRecord = false;
            _anyRead = false;
        }

        private void ConsumeCRLF(char character)
        {
            if (character == '\r' && _reader.Peek() == '\n')
            {
                _reader.Read();
            }
        }

        public IEnumerable<CsvRecord> ReadAll()
        {
            CsvRecord? record;

            while ((record = Read()) != null)
            {
                yield return record;
            }
        }

        public int RecordIndex => _recordIndex;

        public void Dispose()
        {
            _reader.Dispose();
        }

        private enum ParsingState
        {
            StartField,
            InUnquotedField,
            AfterQuote,
            InQuotedField,
        }
    }
}