namespace CsvSharp
{
    /// <summary>
    /// Defines functionality for writing CSV data.
    /// </summary>
    public interface ICsvWriter : IDisposable
    {
        /// <summary>
        /// Writes a single record to the CSV stream.
        /// </summary>
        /// <param name="record">The record to write.</param>
        void Write(CsvRecord record);

        /// <summary>
        /// Writes multiple records to the CSV stream.
        /// </summary>
        /// <param name="records">The records to write.</param>
        void WriteAll(IEnumerable<CsvRecord> records, bool flush = true);

        /// <summary>
        /// Flushes any buffered output.
        /// </summary>
        void Flush();
    }
}