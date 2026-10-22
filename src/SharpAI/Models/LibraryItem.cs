namespace SharpAI.Models
{
    using System;

    /// <summary>A scoped system prompt or model configuration. Shared defaults and private user records use the same ownership rules.</summary>
    public class LibraryItem
    {
        /// <summary>Immutable identifier.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        /// <summary>SystemPrompt or ModelConfiguration, assigned by the endpoint.</summary>
        public string Kind { get; set; }
        /// <summary>Display name, at most 256 characters.</summary>
        public string Name
        {
            get {return _Name;}
            set
            {
                if(String.IsNullOrWhiteSpace(value)||value.Trim().Length>256)throw new ArgumentException("A name of 1 to 256 characters is required.",nameof(Name));
                _Name=value.Trim();
            }
        }
        private string _Name;
        /// <summary>Optional operator description, at most 4096 characters.</summary>
        public string Description
        {
            get {return _Description;}
            set
            {
                if((value?.Length ?? 0)>4096)throw new ArgumentOutOfRangeException(nameof(Description),"Description exceeds 4096 characters.");
                _Description=value ?? String.Empty;
            }
        }
        private string _Description=String.Empty;
        /// <summary>System, Tenant, or User. Default User.</summary>
        public string Scope
        {
            get {return _Scope;}
            set
            {
                if(value!="System"&&value!="Tenant"&&value!="User")throw new ArgumentException("Scope must be System, Tenant, or User.",nameof(Scope));
                _Scope=value;
            }
        }
        private string _Scope="User";
        /// <summary>Owning tenant for Tenant and User items; null for System items.</summary>
        public string TenantId { get; set; }
        /// <summary>Owning user for User items; otherwise null. Always derived from the authenticated user.</summary>
        public string UserId { get; set; }
        /// <summary>System prompt text. Only present for SystemPrompt items.</summary>
        public string Content
        {
            get {return _Content;}
            set
            {
                if((value?.Length ?? 0)>1048576)throw new ArgumentOutOfRangeException(nameof(Content),"Prompt content exceeds 1048576 characters.");
                _Content=value;
            }
        }
        private string _Content;
        /// <summary>Generation parameters. Only present for ModelConfiguration items.</summary>
        public GenerationConfiguration Configuration { get; set; }
        /// <summary>Creation timestamp.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        /// <summary>Modification timestamp.</summary>
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
        /// <summary>Whether the current caller can edit this record. Computed by REST, never trusted on writes.</summary>
        public bool CanEdit { get; set; }
        /// <summary>Whether the current caller may delete this record. Computed by REST.</summary>
        public bool CanDelete { get; set; }
    }
}
