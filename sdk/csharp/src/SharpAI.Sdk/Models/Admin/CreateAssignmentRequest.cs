namespace SharpAI.Sdk.Models.Admin
{
    /// <summary>
    /// Request body to assign a role to a user at a given scope. Reference a role by GUID or by name.
    /// </summary>
    public class CreateAssignmentRequest
    {
        /// <summary>
        /// User identifier the role is assigned to.
        /// </summary>
        public string UserGuid { get; set; } = string.Empty;

        /// <summary>
        /// Role identifier, or null when referencing a role by name.
        /// </summary>
        public string? RoleGuid { get; set; }

        /// <summary>
        /// Role name, used when the GUID is null (for a built-in role).
        /// </summary>
        public string? RoleName { get; set; }

        /// <summary>
        /// Resource scope ("Tenant" or "Resource").
        /// </summary>
        public string? ResourceScope { get; set; }

        /// <summary>
        /// Target resource identifier when resource-scoped.
        /// </summary>
        public string? ResourceGuid { get; set; }

        /// <summary>
        /// Whether a tenant-scoped grant flows to child resources.
        /// </summary>
        public bool InheritsToChildren { get; set; } = true;
    }
}
