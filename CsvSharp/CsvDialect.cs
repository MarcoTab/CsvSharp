namespace CsvSharp
{
    /// <summary>
    /// Defines CSV formatting rules.
    /// </summary>
    public sealed class CsvDialect
    {
        /// <summary>
        /// The field delimiter character.
        /// </summary>
        public char Delimiter { get; init; } = ',';

        /// <summary>
        /// The character used to quote fields.
        /// </summary>
        public char Quote { get; init; } = '"';

        /// <summary>
        /// The default RFC 4180 dialect.
        /// </summary>
        public static CsvDialect Default { get; } = new CsvDialect();
    }
}