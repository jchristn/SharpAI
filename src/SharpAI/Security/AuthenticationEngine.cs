namespace SharpAI.Security
{
    using System;
    using System.Collections.Generic;

    using SharpAI.Database;

    /// <summary>
    /// Resolves request principals against the persisted account store and issues/validates session
    /// tokens. This is the functional heart of authentication and is deliberately free of any HTTP
    /// framework dependency so it can be unit-tested directly against a database driver.
    ///
    /// It layers three credentialed schemes on top of the anonymous/admin-key decision made by
    /// <see cref="AuthEvaluator"/>: interactive login (email + password) that mints a revocable
    /// server-side <see cref="AuthSession"/> referenced by an encrypted bearer token, bearer-token
    /// resolution, and non-interactive access-key / secret-key resolution. Secrets are compared only as
    /// SHA-256 digests, in constant time.
    /// </summary>
    public class AuthenticationEngine
    {
        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly SessionTokenService _Tokens;
        private int _DefaultSessionTtlMinutes = 60;

        #endregion

        #region Public-Members

        /// <summary>
        /// Default session lifetime in minutes when a login does not specify one. Default 60; clamped to
        /// the range 1..43200 (30 days).
        /// </summary>
        public int DefaultSessionTtlMinutes
        {
            get
            {
                return _DefaultSessionTtlMinutes;
            }
            set
            {
                _DefaultSessionTtlMinutes = Math.Clamp(value, 1, 43200);
            }
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Initialized database driver. May not be null.</param>
        /// <param name="tokens">Session token service. May not be null.</param>
        public AuthenticationEngine(DatabaseDriverBase database, SessionTokenService tokens)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Evaluate a request's authentication decision across every supported scheme. When authentication
        /// is disabled the result is the implicit system principal. When enabled: anonymous endpoints are
        /// allowed, then a valid admin API key, then a valid bearer session token, then a valid
        /// access-key / secret-key pair are tried in order; anything else is flagged to be challenged.
        /// </summary>
        /// <param name="authEnabled">Whether authentication is enabled.</param>
        /// <param name="path">Request path (without query string).</param>
        /// <param name="apiKey">Admin API key (x-api-key), or null.</param>
        /// <param name="adminApiKeys">Configured valid admin API keys, or null/empty.</param>
        /// <param name="bearerToken">Bearer session token, or null.</param>
        /// <param name="accessKey">Access key, or null.</param>
        /// <param name="secretKey">Secret key, or null.</param>
        /// <returns>The established request context. Never null.</returns>
        public RequestContext Authenticate(
            bool authEnabled,
            string path,
            string apiKey,
            IReadOnlyList<string> adminApiKeys,
            string bearerToken,
            string accessKey,
            string secretKey)
        {
            RequestContext baseline = AuthEvaluator.Evaluate(authEnabled, path, apiKey, adminApiKeys);

            // Disabled server, anonymous endpoint, or an admin key already matched: nothing more to try.
            if (!authEnabled) return baseline;
            if (baseline.IsAnonymousEndpoint) return baseline;
            if (baseline.IsAuthenticated) return baseline;

            if (!String.IsNullOrEmpty(bearerToken))
            {
                RequestContext bearerContext = ResolveBearerToken(bearerToken);
                if (bearerContext != null) return bearerContext;
            }

            if (!String.IsNullOrEmpty(accessKey) && !String.IsNullOrEmpty(secretKey))
            {
                RequestContext keyContext = ResolveAccessKey(accessKey, secretKey);
                if (keyContext != null) return keyContext;
            }

            return new RequestContext
            {
                IsAuthenticated = false,
                ShouldChallenge = true,
                PrincipalType = PrincipalTypeEnum.None
            };
        }

        /// <summary>
        /// Validate an email/password login within a tenant and, on success, create a revocable session and
        /// return its encrypted bearer token. Returns null on any failure (unknown tenant/user, inactive
        /// user, or bad password); the failure is intentionally indistinguishable to the caller.
        /// </summary>
        /// <param name="tenantGuid">Tenant identifier. When null/empty the tenant named "default" is used.</param>
        /// <param name="email">Email address.</param>
        /// <param name="password">Plaintext password.</param>
        /// <param name="ttlMinutes">Session lifetime in minutes; values &lt; 1 fall back to <see cref="DefaultSessionTtlMinutes"/>.</param>
        /// <param name="session">On success, the created session; otherwise null.</param>
        /// <returns>An encrypted bearer token, or null on failure.</returns>
        public string Login(string tenantGuid, string email, string password, int ttlMinutes, out AuthSession session)
        {
            session = null;
            if (String.IsNullOrEmpty(email)) return null;

            string resolvedTenantGuid = ResolveTenantGuid(tenantGuid);
            if (String.IsNullOrEmpty(resolvedTenantGuid)) return null;

            Tenant tenant = _Database.Tenants.Read(resolvedTenantGuid);
            if (tenant == null || !tenant.Active) return null;
            User user = _Database.Users.GetByEmail(resolvedTenantGuid, email);
            if (user == null || !user.Active) return null;
            if (!PasswordHasher.Verify(password, user.PasswordSha256)) return null;

            int effectiveTtl = ttlMinutes >= 1 ? ttlMinutes : _DefaultSessionTtlMinutes;

            AuthSession created = new AuthSession
            {
                UserGuid = user.Guid,
                TenantGuid = user.TenantGuid,
                PrincipalType = PrincipalTypeEnum.User,
                CreatedUtc = DateTime.UtcNow,
                ExpiresUtc = DateTime.UtcNow.AddMinutes(effectiveTtl)
            };

            session = _Database.Sessions.Create(created);
            return _Tokens.Encrypt(session.Guid);
        }

        /// <summary>
        /// Resolve a bearer token to a live session, or null when the token is invalid, the session is
        /// unknown, revoked (inactive), or expired.
        /// </summary>
        /// <param name="bearerToken">Bearer token.</param>
        /// <returns>The live session, or null.</returns>
        public AuthSession ReadSession(string bearerToken)
        {
            if (String.IsNullOrEmpty(bearerToken)) return null;

            string sessionId = _Tokens.Decrypt(bearerToken);
            if (String.IsNullOrEmpty(sessionId)) return null;

            AuthSession session = _Database.Sessions.Read(sessionId);
            if (session == null || !session.Active) return null;
            if (session.ExpiresUtc <= DateTime.UtcNow) return null;
            User owner = _Database.Users.Read(session.UserGuid);
            Tenant tenant = _Database.Tenants.Read(session.TenantGuid);
            if (owner == null || !owner.Active || owner.TenantGuid != session.TenantGuid || tenant == null || !tenant.Active) return null;

            return session;
        }

        /// <summary>
        /// Revoke a session by identifier, marking it inactive with a reason.
        /// </summary>
        /// <param name="sessionGuid">Session identifier.</param>
        /// <param name="reason">Revocation reason.</param>
        public void RevokeSession(string sessionGuid, string reason)
        {
            if (String.IsNullOrEmpty(sessionGuid)) return;
            _Database.Sessions.Revoke(sessionGuid, reason);
        }

        #endregion

        #region Private-Methods

        private RequestContext ResolveBearerToken(string bearerToken)
        {
            AuthSession session = ReadSession(bearerToken);
            if (session == null) return null;

            RequestContext context = new RequestContext
            {
                IsAuthenticated = true,
                PrincipalType = PrincipalTypeEnum.User,
                PrincipalGuid = session.UserGuid,
                TenantGuid = session.TenantGuid,
                SessionGuid = session.Guid,
                AuthScheme = AuthSchemeEnum.BearerToken
            };

            ApplyUserFlags(context, session.UserGuid);
            return context;
        }

        private RequestContext ResolveAccessKey(string accessKey, string secretKey)
        {
            Credential credential = _Database.Credentials.GetByAccessKey(accessKey);
            if (credential == null || !credential.Active) return null;
            if (credential.ExpiresUtc.HasValue && credential.ExpiresUtc.Value <= DateTime.UtcNow) return null;
            if (!PasswordHasher.Verify(secretKey, credential.SecretSha256)) return null;
            User owner = _Database.Users.Read(credential.UserGuid);
            Tenant tenant = _Database.Tenants.Read(credential.TenantGuid);
            if (owner == null || !owner.Active || owner.TenantGuid != credential.TenantGuid || tenant == null || !tenant.Active) return null;

            // The principal IS the credential (least privilege); the owning user is carried only for the
            // RBAC owner-ceiling. A credential does not inherit its owner's admin bypass.
            return new RequestContext
            {
                IsAuthenticated = true,
                PrincipalType = PrincipalTypeEnum.Credential,
                PrincipalGuid = credential.Guid,
                OwnerUserGuid = credential.UserGuid,
                TenantGuid = credential.TenantGuid,
                AuthScheme = AuthSchemeEnum.AccessKeySecret
            };
        }

        private void ApplyUserFlags(RequestContext context, string userGuid)
        {
            if (String.IsNullOrEmpty(userGuid)) return;

            User user = _Database.Users.Read(userGuid);
            if (user == null) return;

            context.IsAdmin = user.IsAdmin;
            context.IsTenantAdmin = user.IsTenantAdmin;
        }

        private string ResolveTenantGuid(string tenantGuid)
        {
            if (!String.IsNullOrEmpty(tenantGuid)) return tenantGuid;

            Tenant defaultTenant = _Database.Tenants.GetByName("default");
            return defaultTenant != null ? defaultTenant.Guid : null;
        }

        #endregion
    }
}
