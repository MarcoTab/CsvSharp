namespace CsvSharp
{
    /// <summary>
    /// Base exception type for CSV-related errors.
    /// </summary>
    
    [Serializable]
    public class CsvException : Exception
    {
        public CsvException(string message)
            : base(message)
        {}

        public CsvException(string message, Exception innerException)
            : base(message, innerException)
        {}
    }
}