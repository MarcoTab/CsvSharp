using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CsvSharp
{
    /// <summary>
    /// Represents a single CSV record (row).
    /// </summary>
    public sealed class CsvRecord : IReadOnlyList<string>
    {
        private readonly IReadOnlyList<string> _fields;

        /// <summary>
        /// Initializes a new CSV record.
        /// </summary>
        /// <param name="fields">The fields in the record.</param>
        public CsvRecord(IEnumerable<string> fields)
        {
            _fields = fields.ToList();
        }

        /// <summary>
        /// Gets the number of fields in the record.
        /// </summary>
        public int Count => _fields.Count;

        /// <summary>
        /// Gets the field at the specified index.
        /// </summary>
        public string this[int index] => _fields[index];

        /// <summary>
        /// Returns an enumerator over the fields.
        /// </summary>
        public IEnumerator<string> GetEnumerator() => _fields.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
    }
}