namespace SharpAI.Sdk.Models.Admin
{
    /// <summary>
    /// The one-time credential creation response, including the plaintext bearer token (shown only once).
    /// </summary>
    public class CreatedCredential
    {
        private string? _Token;
        private string? _TokenType;
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
        /// Opaque bearer token, returned only once.
        /// </summary>
        public string? Token { get { return _Token; } set { _Token = value; } }

        /// <summary>
        /// Authorization scheme, always Bearer.
        /// </summary>
        public string? TokenType { get { return _TokenType; } set { _TokenType = value; } }

        /// <summary>
        /// Expiry time in UTC, if any.
        /// </summary>
        public string? ExpiresUtc { get; set; }
    }
}
