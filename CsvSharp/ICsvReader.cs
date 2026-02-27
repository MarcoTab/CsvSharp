namespace CsvSharp
{
    /// <summary>
    /// Defines functionality for reading CSV data.
    /// </summary>
    public interface ICsvReader : IDisposable
    {
        /// <summary>
        /// Reads the next record from the CSV stream.
        /// </summary>
        /// <returns>
        /// A <see cref="CsvRecord"/> if a record was read,
        /// or <c>null</c> if end of file was reached.
        /// </returns>
        CsvRecord? Read();

        /// <summary>
        /// Reads all remaining records from the CSV stream.
        /// </summary>
        /// <returns>An enumerable sequence of CSV records.</returns>
        IEnumerable<CsvRecord> ReadAll();

        /// <summary>
        /// Gets the zero-based index of the current record.
        /// </summary>
        int RecordIndex { get; }
    }
}