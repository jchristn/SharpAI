namespace SharpAI.Sdk.Models.Admin
{
    /// <summary>
    /// Credential creation response, including a bearer token that authorized administrators may reveal later.
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
        /// Initial opaque bearer token. Treat as secret.
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
