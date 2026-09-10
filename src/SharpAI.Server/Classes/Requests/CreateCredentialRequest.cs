namespace SharpAI.Server.Classes.Requests
{
    using System;

    /// <summary>
    /// Request body to create a credential. The access key and secret key are generated server-side; the
    /// plaintext secret is returned exactly once in the create response and never again.
    /// </summary>
    public class CreateCredentialRequest
    {
        #region Public-Members

        /// <summary>
        /// Owning user identifier.
        /// </summary>
        public string UserGuid { get; set; } = String.Empty;

        /// <summary>
        /// Human-readable name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Optional expiry time in UTC.
        /// </summary>
        public DateTime? ExpiresUtc { get; set; } = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public CreateCredentialRequest()
        {
        }

        #endregion
    }
}
