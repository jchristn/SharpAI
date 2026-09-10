namespace SharpAI.Sdk.Models.Admin
{
    /// <summary>
    /// Response from a successful login.
    /// </summary>
    public class LoginResponse
    {
        /// <summary>
        /// Opaque bearer session token.
        /// </summary>
        public string? Token { get; set; }

        /// <summary>
        /// Session identifier.
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// User identifier.
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Expiry time in UTC.
        /// </summary>
        public string? ExpiresUtc { get; set; }
    }
}
