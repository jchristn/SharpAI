namespace SharpAI.Server.Classes.Requests
{
    using System;

    using SharpAI.Security;

    /// <summary>
    /// Request body to assign a role to a user at a given scope. The role may be referenced by GUID or by
    /// name (for built-in roles).
    /// </summary>
    public class CreateAssignmentRequest
    {
        #region Public-Members

        /// <summary>
        /// User identifier the role is assigned to.
        /// </summary>
        public string UserGuid { get; set; } = String.Empty;

        /// <summary>
        /// Role identifier, or null when referencing a role by name.
        /// </summary>
        public string RoleGuid { get; set; } = null;

        /// <summary>
        /// Role name, used when <see cref="RoleGuid"/> is null (for example a built-in role name).
        /// </summary>
        public string RoleName { get; set; } = null;

        /// <summary>
        /// Resource scope of the grant.
        /// </summary>
        public ResourceScopeEnum ResourceScope { get; set; } = ResourceScopeEnum.Tenant;

        /// <summary>
        /// Target resource identifier when resource-scoped.
        /// </summary>
        public string ResourceGuid { get; set; } = null;

        /// <summary>
        /// Whether a tenant-scoped grant flows to child resources.
        /// </summary>
        public bool InheritsToChildren { get; set; } = true;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public CreateAssignmentRequest()
        {
        }

        #endregion
    }
}
