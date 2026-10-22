namespace Test.Xunit
{
    using System;
    using System.IO;
    using System.Threading.Tasks;

    using SharpAI.Database;
    using SharpAI.Database.Sqlite;
    using SharpAI.Security;

    using SyslogLogging;

    using Xunit;

    /// <summary>Opaque bearer credentials preserve credential identity and account lifecycle restrictions.</summary>
    public sealed class BearerCredentialTests
    {
        /// <summary>Tokens are random, digest-only credentials with no inherited administrator bypass.</summary>
        [Fact]
        public async Task BearerCredentialsRespectRevocationExpiryAndOwnerStatus()
        {
            string filename = Path.Combine(Path.GetTempPath(), "sharpai-bearer-" + Guid.NewGuid().ToString("N") + ".db");
            using LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;
            try
            {
                using SqliteDatabaseDriver database = new SqliteDatabaseDriver(new DatabaseSettings { Filename = filename }, logging);
                await database.InitializeAsync();
                Tenant tenant = database.Tenants.Create(new Tenant { Name = "Bearer tenant" });
                User owner = database.Users.Create(new User { TenantGuid = tenant.Guid, Email = "owner@example.test", IsAdmin = true });
                string token = CredentialKeyGenerator.GenerateBearerToken();
                Assert.Equal(72, token.Length);
                Assert.StartsWith("sharpai_", token);
                Assert.NotEqual(token, CredentialKeyGenerator.GenerateBearerToken());
                Credential credential = database.Credentials.Create(new Credential
                {
                    TenantGuid = tenant.Guid, UserGuid = owner.Guid, Name = "API token",
                    AccessKey = "bearer_" + PasswordHasher.Hash(token), SecretSha256 = PasswordHasher.Hash(token)
                });
                AuthenticationEngine engine = new AuthenticationEngine(database, new SessionTokenService("fixture signing key"));
                RequestContext context = Resolve(engine, token);
                Assert.True(context.IsAuthenticated);
                Assert.Equal(PrincipalTypeEnum.Credential, context.PrincipalType);
                Assert.Equal(AuthSchemeEnum.BearerToken, context.AuthScheme);
                Assert.Equal(credential.Guid, context.PrincipalGuid);
                Assert.Equal(owner.Guid, context.OwnerUserGuid);
                Assert.False(context.IsAdmin);
                Assert.False(context.IsTenantAdmin);
                Assert.True(Resolve(engine, token + "bad").ShouldChallenge);
                credential.Active = false; database.Credentials.Update(credential);
                Assert.True(Resolve(engine, token).ShouldChallenge);
                credential.Active = true; credential.ExpiresUtc = DateTime.UtcNow.AddMinutes(-1); database.Credentials.Update(credential);
                Assert.True(Resolve(engine, token).ShouldChallenge);
                credential.ExpiresUtc = null; database.Credentials.Update(credential);
                owner.Active = false; database.Users.Update(owner);
                Assert.True(Resolve(engine, token).ShouldChallenge);
                owner.Active = true; database.Users.Update(owner);
                tenant.Active = false; database.Tenants.Update(tenant);
                Assert.True(Resolve(engine, token).ShouldChallenge);
                tenant.Active = true; database.Tenants.Update(tenant);
                Assert.True(Resolve(engine, token).IsAuthenticated);
                database.Credentials.Delete(credential.Guid);
                Assert.True(Resolve(engine, token).ShouldChallenge);
            }
            finally { if (File.Exists(filename)) File.Delete(filename); }
        }

        private static RequestContext Resolve(AuthenticationEngine engine, string token)
        {
            return engine.Authenticate(true, "/v1/models", null, null, token, null, null);
        }
    }
}
