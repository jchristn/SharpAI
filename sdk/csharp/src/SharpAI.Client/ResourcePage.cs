namespace SharpAI.Client
{
    using System.Collections.Generic;

    /// <summary>Server-paginated resources. Mutable; not thread safe. Counts reflect visibility filters.</summary>
    public sealed class ResourcePage<T>
    {
        /// <summary>Only the visible records on this page.</summary>
        public List<T> Objects { get; set; } = new List<T>();
        /// <summary>Total matching records.</summary>
        public long TotalRecords { get; set; }
        /// <summary>True for the last page.</summary>
        public bool EndOfResults { get; set; }
    }
}
