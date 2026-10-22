namespace SharpAI.Security
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Generates access keys and secret keys for credentials. Access keys carry the prefix "access_" and
    /// secret keys "secret_", each followed by a run of high-entropy alphanumerics.
    /// </summary>
    public static class CredentialKeyGenerator
    {
        #region Private-Members

        private const string _Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Generate an opaque API bearer token with 256 bits of cryptographic randomness.
        /// </summary>
        /// <returns>Bearer token, shown only on creation.</returns>
        public static string GenerateBearerToken()
        {
            return "sharpai_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        }

        /// <summary>
        /// Generate a legacy access key. Server API authentication requires bearer tokens.
        /// </summary>
        /// <returns>Access key.</returns>
        public static string GenerateAccessKey()
        {
            return "access_" + RandomString(32);
        }

        /// <summary>
        /// Generate a secret key (prefix "secret_", 48 random characters).
        /// </summary>
        /// <returns>Secret key.</returns>
        public static string GenerateSecretKey()
        {
            return "secret_" + RandomString(48);
        }

        #endregion

        #region Private-Methods

        private static string RandomString(int length)
        {
            byte[] bytes = RandomNumberGenerator.GetBytes(length);
            StringBuilder builder = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                builder.Append(_Alphabet[bytes[i] % _Alphabet.Length]);
            }
            return builder.ToString();
        }

        #endregion
    }
}
