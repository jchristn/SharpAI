namespace SharpAI.Server.Classes.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using SharpAI.Database;
    using SharpAI.Security;

    /// <summary>
    /// Provisions a revocable, inference-only bearer credential once per administrator account.
    /// Plaintext is kept outside the database in an owner-readable bootstrap file. Not thread safe;
    /// invoke once during startup before accepting requests.
    /// </summary>
    public static class BootstrapCredential
    {
        /// <summary>
        /// Create the initial API credential and its grant, recovering safely after interrupted startup.
        /// An existing provisioning marker prevents deleted or revoked credentials from being recreated.
        /// </summary>
        /// <param name="database">Initialized account database.</param>
        /// <param name="admin">Default administrator who owns the credential.</param>
        public static void Ensure(DatabaseDriverBase database, User admin)
        {
            ArgumentNullException.ThrowIfNull(database);
            ArgumentNullException.ThrowIfNull(admin);
            string directory = Environment.GetEnvironmentVariable("SHARPAI_AUTH_DIRECTORY") ?? "./auth";
            Directory.CreateDirectory(directory);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            string tokenFile = Path.Combine(directory, "default-bearer-token");
            string markerFile = Path.Combine(directory, "default-credential-owner");
            if (File.Exists(markerFile) && File.ReadAllText(markerFile).Trim() == admin.Guid) return;
            string credentialId = "crd_bootstrap_" + admin.Guid;
            Credential credential = database.Credentials.Read(credentialId);
            if (credential == null)
            {
                string bearer = File.Exists(tokenFile) && !File.Exists(markerFile)
                    ? File.ReadAllText(tokenFile).Trim() : CredentialKeyGenerator.GenerateBearerToken();
                if (!bearer.StartsWith("sharpai_", StringComparison.Ordinal) || bearer.Length != 72)
                    throw new InvalidOperationException("Invalid bootstrap token file.");
                WritePrivate(tokenFile, bearer);
                credential = database.Credentials.Create(new Credential
                {
                    Guid = credentialId,
                    TenantGuid = admin.TenantGuid,
                    UserGuid = admin.Guid,
                    Name = "Default API bearer token",
                    AccessKey = "bearer_" + PasswordHasher.Hash(bearer),
                    SecretSha256 = PasswordHasher.Hash(bearer)
                });
            }
            string assignmentId = "asn_bootstrap_" + admin.Guid;
            if (database.CredentialScopeAssignments.GetForCredential(admin.TenantGuid, credentialId).Count == 0)
                database.CredentialScopeAssignments.Create(new CredentialScopeAssignment
                {
                    Guid = assignmentId,
                    CredentialGuid = credentialId,
                    TenantGuid = admin.TenantGuid,
                    Permissions = new List<string> { "Read", "Execute" },
                    ResourceTypes = new List<string> { ResourceTypes.Model, ResourceTypes.Inference }
                });
            WritePrivate(markerFile, admin.Guid);
        }

        private static void WritePrivate(string path, string value)
        {
            string temporary = path + ".tmp";
            FileStreamOptions options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using (FileStream stream = new FileStream(temporary, options))
            using (StreamWriter writer = new StreamWriter(stream)) writer.Write(value);
            File.Move(temporary, path, true);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
