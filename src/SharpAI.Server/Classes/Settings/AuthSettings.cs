namespace SharpAI.Server.Classes.Settings
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Server authentication settings. Bearer authentication is mandatory. Health/readiness probes and
    /// POST /v1.0/token remain reachable anonymously. Legacy API key settings are retained only so older
    /// configuration files deserialize; the server does not accept those keys.
    /// </summary>
    public class AuthSettings
    {
        #region Public-Members

        /// <summary>
        /// Authentication is mandatory. Legacy false values are ignored.
        /// </summary>
        public bool Enabled { get { return true; } set { } }

        /// <summary>
        /// Legacy administrator API keys, ignored by the server. Never null.
        /// </summary>
        public List<string> AdminApiKeys
        {
            get
            {
                return _AdminApiKeys;
            }
            set
            {
                _AdminApiKeys = value ?? new List<string>();
            }
        }

        /// <summary>
        /// Key material used to encrypt session bearer tokens (AES-256, SHA-256-derived key). When null or
        /// empty a random per-boot key is generated, which invalidates existing tokens on restart; set a
        /// stable value to keep sessions valid across restarts. Never null after loading.
        /// </summary>
        public string TokenSigningKey
        {
            get
            {
                return _TokenSigningKey;
            }
            set
            {
                _TokenSigningKey = value ?? String.Empty;
            }
        }

        /// <summary>
        /// Lifetime of a login session in minutes. Default 60; clamped to the range 1..43200 (30 days).
        /// </summary>
        public int SessionTtlMinutes
        {
            get
            {
                return _SessionTtlMinutes;
            }
            set
            {
                _SessionTtlMinutes = Math.Clamp(value, 1, 43200);
            }
        }

        #endregion

        #region Private-Members

        private List<string> _AdminApiKeys = new List<string>();
        private string _TokenSigningKey = String.Empty;
        private int _SessionTtlMinutes = 60;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AuthSettings()
        {
        }

        #endregion
    }
}
