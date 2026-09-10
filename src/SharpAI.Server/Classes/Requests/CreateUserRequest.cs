namespace SharpAI.Server.Classes.Requests
{
    using System;

    /// <summary>
    /// Request body to create a user. The plaintext password is accepted here, hashed server-side (SHA-256),
    /// and never persisted or returned.
    /// </summary>
    public class CreateUserRequest
    {
        #region Public-Members

        /// <summary>
        /// Email address (unique within the tenant).
        /// </summary>
        public string Email { get; set; } = String.Empty;

        /// <summary>
        /// First name.
        /// </summary>
        public string FirstName { get; set; } = String.Empty;

        /// <summary>
        /// Last name.
        /// </summary>
        public string LastName { get; set; } = String.Empty;

        /// <summary>
        /// Plaintext password. Hashed server-side; never stored or echoed.
        /// </summary>
        public string Password { get; set; } = String.Empty;

        /// <summary>
        /// Whether the user is a global administrator.
        /// </summary>
        public bool IsAdmin { get; set; } = false;

        /// <summary>
        /// Whether the user is a tenant administrator.
        /// </summary>
        public bool IsTenantAdmin { get; set; } = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public CreateUserRequest()
        {
        }

        #endregion
    }
}
