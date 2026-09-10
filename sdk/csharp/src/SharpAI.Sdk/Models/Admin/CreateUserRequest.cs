namespace SharpAI.Sdk.Models.Admin
{
    /// <summary>
    /// Request body to create a user. The plaintext password is hashed server-side.
    /// </summary>
    public class CreateUserRequest
    {
        /// <summary>
        /// Email address (unique within the tenant).
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Plaintext password.
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// First name.
        /// </summary>
        public string? FirstName { get; set; }

        /// <summary>
        /// Last name.
        /// </summary>
        public string? LastName { get; set; }

        /// <summary>
        /// Whether the user is a global administrator.
        /// </summary>
        public bool IsAdmin { get; set; }

        /// <summary>
        /// Whether the user is a tenant administrator.
        /// </summary>
        public bool IsTenantAdmin { get; set; }
    }
}
