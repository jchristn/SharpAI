namespace SharpAI.Security
{
    /// <summary>
    /// Well-known resource-type identifiers used in permissions. Resource types are strings (not an enum) so
    /// the permission model stays open to new domain resources without a code change. <see cref="All"/> is the
    /// wildcard that matches any resource type.
    /// </summary>
    public static class ResourceTypes
    {
        /// <summary>Wildcard — matches any resource type.</summary>
        public const string All = "All";

        /// <summary>Tenant records and tenant-level metadata.</summary>
        public const string Tenant = "Tenant";

        /// <summary>User records.</summary>
        public const string User = "User";

        /// <summary>Credential records.</summary>
        public const string Credential = "Credential";

        /// <summary>Authentication session records.</summary>
        public const string Session = "Session";

        /// <summary>RBAC role records.</summary>
        public const string Role = "Role";

        /// <summary>RBAC permission records.</summary>
        public const string Permission = "Permission";

        /// <summary>RBAC assignment records.</summary>
        public const string Assignment = "Assignment";

        /// <summary>Security audit records.</summary>
        public const string Audit = "Audit";

        /// <summary>Privileged control-plane surface (administration, backup, configuration).</summary>
        public const string Admin = "Admin";

        /// <summary>Model registry records.</summary>
        public const string Model = "Model";

        /// <summary>Inference operations (chat, completion, embeddings).</summary>
        public const string Inference = "Inference";

        /// <summary>Server settings.</summary>
        public const string Settings = "Settings";

        /// <summary>Request-history records.</summary>
        public const string RequestHistory = "RequestHistory";
        /// <summary>Scoped reusable system prompts.</summary>
        public const string SystemPrompt = "SystemPrompt";
        /// <summary>Scoped reusable generation parameters.</summary>
        public const string ModelConfiguration = "ModelConfiguration";
    }
}
