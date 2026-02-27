using System.Text;

namespace CsvSharp
{
    /// <summary>
    /// Configuration options controlling CSV parsing and writing.
    /// </summary>
    public sealed class CsvConfiguration
    {
        /// <summary>
        /// Gets or sets the CSV dialect.
        /// </summary>
        public CsvDialect Dialect { get; set; } = CsvDialect.Default;

        /// <summary>
        /// Gets or sets whether the first row is treated as a header.
        /// </summary>
        public bool HasHeader { get; set; } = false;
        
        /// <summary>
        /// Gets or sets whether writing to a CSV file should quote fields or not
        /// </summary>
        public bool QuoteAllFields {get; set;} = false;

        /// <summary>
        /// Gets or sets whether leading and trailing whitespace is trimmed.
        /// </summary>
        public bool TrimWhitespace { get; set; } = false;

        /// <summary>
        /// Gets or sets whether malformed rows should throw exceptions.
        /// </summary>
        public bool Strict { get; set; } = true;

        /// <summary>
        /// Gets or sets the character encoding.
        /// </summary>
        public Encoding Encoding { get; set; } = Encoding.UTF8;
    }
}