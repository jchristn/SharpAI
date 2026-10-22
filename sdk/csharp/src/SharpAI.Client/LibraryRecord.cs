namespace SharpAI.Client
{
    /// <summary>Public scoped library record. Mutable; not thread safe. Scope and owner cannot change in place.</summary>
    public sealed class LibraryRecord
    {
        /// <summary>Server Id value.</summary>
        public string Id { get; set; } = string.Empty;
        /// <summary>Server Name value.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Server Description value.</summary>
        public string Description { get; set; } = string.Empty;
        /// <summary>Server Scope value.</summary>
        public string Scope { get; set; } = string.Empty;
        /// <summary>Server TenantId value.</summary>
        public string? TenantId { get; set; }
        /// <summary>Server UserId value.</summary>
        public string? UserId { get; set; }
        /// <summary>Server Content value.</summary>
        public string? Content { get; set; }
        /// <summary>Server Configuration value.</summary>
        public GenerationConfiguration? Configuration { get; set; }
        /// <summary>Server CreatedUtc value.</summary>
        public System.DateTimeOffset CreatedUtc { get; set; }
        /// <summary>Server UpdatedUtc value.</summary>
        public System.DateTimeOffset UpdatedUtc { get; set; }
        /// <summary>Server CanEdit value.</summary>
        public bool CanEdit { get; set; }
        /// <summary>Server CanDelete value.</summary>
        public bool CanDelete { get; set; }
    }
}
