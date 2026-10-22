namespace SharpAI.Security
{
    using System;

    using SharpAI.Helpers;

    /// <summary>
    /// A tenant. Every user, credential, and session belongs to exactly one tenant.
    /// </summary>
    public class Tenant
    {
        #region Public-Members

        /// <summary>
        /// Tenant identifier (prefix "ten_", or "default" for the seeded default tenant).
        /// </summary>
        public string Guid { get; set; } = IdGenerator.GenerateTenantId();

        /// <summary>
        /// Tenant name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Whether the tenant is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Whether the tenant is protected from deletion/modification via tenant APIs.
        /// </summary>
        public bool IsProtected { get; set; } = false;

        /// <summary>
        /// Creation time in UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last update time in UTC.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public Tenant()
        {
        }

        #endregion
    }
}
