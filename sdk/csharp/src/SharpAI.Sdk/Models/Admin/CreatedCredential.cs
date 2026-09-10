namespace SharpAI.Sdk.Models.Admin
{
    /// <summary>
    /// The one-time credential creation response, including the plaintext secret key (shown only once).
    /// </summary>
    public class CreatedCredential
    {
        /// <summary>
        /// Credential identifier.
        /// </summary>
        public string? Guid { get; set; }

        /// <summary>
        /// Owning user identifier.
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Human-readable name.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Access key (safe to store).
        /// </summary>
        public string? AccessKey { get; set; }

        /// <summary>
        /// Plaintext secret key, returned only once at creation.
        /// </summary>
        public string? SecretKey { get; set; }

        /// <summary>
        /// Expiry time in UTC, if any.
        /// </summary>
        public string? ExpiresUtc { get; set; }
    }
}
