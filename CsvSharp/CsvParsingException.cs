namespace CsvSharp
{
    /// <summary>
    /// Thrown when malformed CSV data is encountered.
    /// </summary>
    
    [Serializable]
    public sealed class CsvParsingException : CsvException
    {
        /// <summary>
        /// The zero-based record index where the error occurred.
        /// </summary>
        public int RecordIndex { get; }

        public CsvParsingException(string message, int recordIndex)
            : base($"{message} (Record {recordIndex})")
        {
            RecordIndex = recordIndex;
        }

        public CsvParsingException(string message, int recordIndex, Exception innerException)
            : base($"{message} (Record {recordIndex})", innerException)
        {
            RecordIndex = recordIndex;
        }
    }
}