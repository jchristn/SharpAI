namespace SharpAI.Server.Classes.Runtime
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;

    using SharpAI.Database;
    using SharpAI.Security;

    /// <summary>
    /// Durable AES-GCM storage for administrator-revealable API bearer tokens. Authentication continues
    /// to use database digests. The encryption key and ciphertext files reside in the private auth directory.
    /// File operations are serialized within this process. Back up the directory together with the database.
    /// </summary>
    public static class CredentialTokenVault
    {
        private static readonly object _Lock = new object();

        /// <summary>Encrypt and retain a token, binding its ciphertext to the credential identifier.</summary>
        /// <param name="credential">Persisted credential that owns the token.</param>
        /// <param name="token">Bearer token matching the stored digest.</param>
        public static void Store(Credential credential, string token)
        {
            ArgumentNullException.ThrowIfNull(credential);
            if (!PasswordHasher.Verify(token, credential.SecretSha256)) throw new ArgumentException("Token does not match credential.", nameof(token));
            lock (_Lock)
            {
                string directory = DirectoryPath();
                byte[] key = ReadKey(directory);
                byte[] nonce = RandomNumberGenerator.GetBytes(12);
                byte[] plaintext = Encoding.UTF8.GetBytes(token);
                byte[] ciphertext = new byte[plaintext.Length];
                byte[] tag = new byte[16];
                try
                {
                    using AesGcm cipher = new AesGcm(key, 16);
                    cipher.Encrypt(nonce, plaintext, ciphertext, tag, Encoding.UTF8.GetBytes(credential.Guid));
                    byte[] envelope = new byte[28 + ciphertext.Length];
                    nonce.CopyTo(envelope, 0); tag.CopyTo(envelope, 12); ciphertext.CopyTo(envelope, 28);
                    WritePrivate(TokenPath(directory, credential.Guid), envelope);
                    if (credential.Guid == "crd_bootstrap_" + credential.UserGuid)
                    {
                        WritePrivate(Path.Combine(directory, "default-bearer-token"), Encoding.UTF8.GetBytes(token));
                        string metricsDirectory = Path.Combine(directory, "metrics");
                        Directory.CreateDirectory(metricsDirectory);
                        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(metricsDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                        WritePrivate(Path.Combine(metricsDirectory, "default-bearer-token"), Encoding.UTF8.GetBytes(token));
                    }
                }
                finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plaintext); }
            }
        }

        /// <summary>Decrypt a retained token. Returns null for legacy or mismatched credentials.</summary>
        /// <param name="credential">Current persisted credential; its digest must match the decrypted token.</param>
        /// <returns>Bearer token, or null when it was not retained.</returns>
        public static string Reveal(Credential credential)
        {
            ArgumentNullException.ThrowIfNull(credential);
            lock (_Lock)
            {
                string directory = DirectoryPath();
                string path = TokenPath(directory, credential.Guid);
                if (!File.Exists(path)) return null;
                byte[] envelope = File.ReadAllBytes(path);
                if (envelope.Length < 29) throw new CryptographicException("Invalid credential token envelope.");
                byte[] key = ReadKey(directory, false);
                byte[] plaintext = new byte[envelope.Length - 28];
                try
                {
                    using AesGcm cipher = new AesGcm(key, 16);
                    cipher.Decrypt(envelope.AsSpan(0, 12), envelope.AsSpan(28), envelope.AsSpan(12, 16), plaintext, Encoding.UTF8.GetBytes(credential.Guid));
                    string token = Encoding.UTF8.GetString(plaintext);
                    return PasswordHasher.Verify(token, credential.SecretSha256) ? token : null;
                }
                finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plaintext); }
            }
        }

        /// <summary>Regenerate a token while preserving credential identity, scopes, status and expiry.</summary>
        /// <param name="database">Initialized account store.</param>
        /// <param name="credentialId">Existing credential identifier.</param>
        /// <returns>The replacement token.</returns>
        public static string Regenerate(DatabaseDriverBase database, string credentialId)
        {
            ArgumentNullException.ThrowIfNull(database);
            lock (_Lock)
            {
                Credential credential = database.Credentials.Read(credentialId) ?? throw new InvalidOperationException("Credential not found.");
                string previous = Reveal(credential);
                string previousLookup = credential.AccessKey;
                string previousDigest = credential.SecretSha256;
                string replacement = CredentialKeyGenerator.GenerateBearerToken();
                credential.AccessKey = "bearer_" + PasswordHasher.Hash(replacement);
                credential.SecretSha256 = PasswordHasher.Hash(replacement);
                try
                {
                    Store(credential, replacement);
                    database.Credentials.Update(credential);
                }
                catch
                {
                    credential.AccessKey = previousLookup; credential.SecretSha256 = previousDigest;
                    if (previous != null) Store(credential, previous); else Delete(credential.Guid);
                    throw;
                }
                return replacement;
            }
        }

        /// <summary>Forget retained ciphertext after credential deletion. Does not remove the vault key.</summary>
        /// <param name="credentialId">Deleted credential identifier.</param>
        public static void Delete(string credentialId)
        {
            lock (_Lock) File.Delete(TokenPath(DirectoryPath(), credentialId));
        }

        private static string DirectoryPath()
        {
            string directory = Environment.GetEnvironmentVariable("SHARPAI_AUTH_DIRECTORY") ?? "./auth";
            Directory.CreateDirectory(directory);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            return directory;
        }

        private static string TokenPath(string directory, string credentialId)
        {
            return Path.Combine(directory, "credential-" + PasswordHasher.Hash(credentialId) + ".token");
        }

        private static byte[] ReadKey(string directory, bool create = true)
        {
            string path = Path.Combine(directory, "credential-encryption.key");
            if (!File.Exists(path))
            {
                if (!create) throw new CryptographicException("Credential encryption key is missing. Restore the auth directory backup.");
                WritePrivate(path, RandomNumberGenerator.GetBytes(32));
            }
            byte[] key = File.ReadAllBytes(path);
            if (key.Length != 32) throw new CryptographicException("Invalid credential encryption key.");
            return key;
        }

        private static void WritePrivate(string path, byte[] value)
        {
            string temporary = path + ".tmp";
            FileStreamOptions options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using (FileStream stream = new FileStream(temporary, options)) stream.Write(value);
            File.Move(temporary, path, true);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
