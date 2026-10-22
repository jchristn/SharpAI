namespace SharpAI.Security
{
    using System;

    using SharpAI.Helpers;

    /// <summary>
    /// A non-interactive credential owned by a user within a tenant. Authentication uses a SHA-256
    /// digest. The server retains encrypted bearer tokens separately for authorized administrator reveal.
    /// </summary>
    public class Credential
    {
        #region Public-Members

        /// <summary>
        /// Credential identifier (prefix "crd_").
        /// </summary>
        public string Guid { get; set; } = IdGenerator.GenerateCredentialId();

        /// <summary>
        /// Owning user identifier.
        /// </summary>
        public string UserGuid { get; set; } = String.Empty;

        /// <summary>
        /// Owning tenant identifier.
        /// </summary>
        public string TenantGuid { get; set; } = String.Empty;

        /// <summary>
        /// Human-readable name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Access key (prefix "access_"). Safe to store and display.
        /// </summary>
        public string AccessKey { get; set; } = String.Empty;

        /// <summary>
        /// SHA-256 hex digest of the secret key. Never the plaintext.
        /// </summary>
        public string SecretSha256 { get; set; } = String.Empty;

        /// <summary>
        /// Whether the credential is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Whether the credential is protected from deletion/modification via tenant APIs.
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

        /// <summary>
        /// Last used time in UTC, if ever.
        /// </summary>
        public DateTime? LastUsedUtc { get; set; } = null;

        /// <summary>
        /// Expiry time in UTC, if the credential expires.
        /// </summary>
        public DateTime? ExpiresUtc { get; set; } = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public Credential()
        {
        }

        #endregion
    }
}
