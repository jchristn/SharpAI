namespace SharpAI.Security
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Encrypts and decrypts opaque bearer tokens that reference a server-side session. Tokens carry only
    /// the internal session identifier, encrypted with AES-256-CBC using a fresh random IV per token, then
    /// authenticated with HMAC-SHA256 over the IV and ciphertext (encrypt-then-MAC). Token layout is
    /// IV (16 bytes) + ciphertext + tag (32 bytes), base64-encoded. Tokens that fail authentication, including
    /// tokens produced under different key material, are rejected before decryption.
    /// Thread safety: instances are immutable after construction and safe for concurrent use.
    /// </summary>
    public class SessionTokenService
    {
        #region Private-Members

        private const int _IvLength = 16;
        private const int _TagLength = 32;
        private readonly byte[] _Key;
        private readonly byte[] _MacKey;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate with key material. The 256-bit AES key is derived as SHA-256 of the material; a separate
        /// HMAC-SHA256 key is derived from the AES key.
        /// </summary>
        /// <param name="keyMaterial">Key material. May not be null or empty.</param>
        /// <exception cref="ArgumentNullException">Thrown when keyMaterial is null or empty.</exception>
        public SessionTokenService(string keyMaterial)
        {
            if (String.IsNullOrEmpty(keyMaterial)) throw new ArgumentNullException(nameof(keyMaterial));
            _Key = SHA256.HashData(Encoding.UTF8.GetBytes(keyMaterial));
            _MacKey = HMACSHA256.HashData(_Key, Encoding.UTF8.GetBytes("sharpai-session-token-mac"));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Encrypt a session identifier into an opaque, authenticated base64 token (random IV prepended,
        /// HMAC tag appended).
        /// </summary>
        /// <param name="sessionId">Session identifier. May not be null or empty.</param>
        /// <returns>Opaque token.</returns>
        /// <exception cref="ArgumentNullException">Thrown when sessionId is null or empty.</exception>
        public string Encrypt(string sessionId)
        {
            if (String.IsNullOrEmpty(sessionId)) throw new ArgumentNullException(nameof(sessionId));

            using (Aes aes = Aes.Create())
            {
                aes.Key = _Key;
                aes.GenerateIV();
                byte[] iv = aes.IV;

                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                {
                    byte[] plain = Encoding.UTF8.GetBytes(sessionId);
                    byte[] cipher = encryptor.TransformFinalBlock(plain, 0, plain.Length);

                    byte[] result = new byte[iv.Length + cipher.Length + _TagLength];
                    Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
                    Buffer.BlockCopy(cipher, 0, result, iv.Length, cipher.Length);

                    byte[] tag = HMACSHA256.HashData(_MacKey, new ReadOnlySpan<byte>(result, 0, iv.Length + cipher.Length));
                    Buffer.BlockCopy(tag, 0, result, iv.Length + cipher.Length, _TagLength);
                    return Convert.ToBase64String(result);
                }
            }
        }

        /// <summary>
        /// Decrypt an opaque token back into its session identifier, or null when the token is malformed,
        /// tampered with, or was produced under different key material.
        /// </summary>
        /// <param name="token">Opaque token.</param>
        /// <returns>Session identifier, or null.</returns>
        public string Decrypt(string token)
        {
            if (String.IsNullOrEmpty(token)) return null;

            try
            {
                byte[] data = Convert.FromBase64String(token);
                if (data.Length <= _IvLength + _TagLength) return null;

                int signedLength = data.Length - _TagLength;
                byte[] expectedTag = HMACSHA256.HashData(_MacKey, new ReadOnlySpan<byte>(data, 0, signedLength));
                if (!CryptographicOperations.FixedTimeEquals(expectedTag, new ReadOnlySpan<byte>(data, signedLength, _TagLength)))
                    return null;

                byte[] iv = new byte[_IvLength];
                Buffer.BlockCopy(data, 0, iv, 0, _IvLength);

                using (Aes aes = Aes.Create())
                {
                    aes.Key = _Key;
                    aes.IV = iv;

                    using (ICryptoTransform decryptor = aes.CreateDecryptor())
                    {
                        byte[] plain = decryptor.TransformFinalBlock(data, _IvLength, signedLength - _IvLength);
                        return Encoding.UTF8.GetString(plain);
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}
