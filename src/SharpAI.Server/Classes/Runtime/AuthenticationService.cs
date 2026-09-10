namespace SharpAI.Server.Classes.Runtime
{
    using System;

    using SharpAI.Database;
    using SharpAI.Security;
    using SharpAI.Server.Classes.Settings;

    using SyslogLogging;

    using WatsonWebserver.Core;

    /// <summary>
    /// Establishes the authenticated <see cref="RequestContext"/> for each request and attaches it to the
    /// HTTP context metadata. Invoked from the Watson <c>PreRouting</c> hook (which fires for every request,
    /// unlike the <c>AuthenticateRequest</c>/<c>AuthenticateApiRequest</c> hooks, which Watson only invokes
    /// for routes registered with <c>requiresAuthentication: true</c>). When authentication is disabled this
    /// installs the system principal; when enabled it resolves the request against the account store (admin
    /// API key, bearer session token, or access-key / secret-key). The 401 challenge and RBAC authorization
    /// are enforced per-route by the <c>Authorize</c> helper reading the attached context; unauthenticated
    /// requests to non-anonymous endpoints are recorded to the security audit log here.
    /// </summary>
    public class AuthenticationService
    {
        #region Private-Members

        private readonly string _Header = "[Auth] ";
        private readonly AuthSettings _Settings;
        private readonly AuthenticationEngine _Engine;
        private readonly DatabaseDriverBase _Database;
        private readonly LoggingModule _Logging;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Authentication settings.</param>
        /// <param name="engine">Authentication engine.</param>
        /// <param name="database">Database driver (for the audit log).</param>
        /// <param name="logging">Logging module.</param>
        public AuthenticationService(
            AuthSettings settings,
            AuthenticationEngine engine,
            DatabaseDriverBase database,
            LoggingModule logging)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Resolve the request's <see cref="RequestContext"/> and attach it to <c>ctx.Metadata</c> so the
        /// per-route <c>Authorize</c> helper can enforce authentication (401) and authorization (403).
        /// Invoked from the <c>PreRouting</c> hook, which — unlike the authentication hooks — fires for every
        /// request regardless of per-route configuration. When the resolved context indicates the request is
        /// unauthenticated against a non-anonymous path, the denial is recorded to the audit log here (the
        /// actual 401 is thrown by the guarded route).
        /// </summary>
        /// <param name="ctx">HTTP context.</param>
        /// <returns>The attached request context.</returns>
        public RequestContext AttachContext(HttpContextBase ctx)
        {
            string path;
            RequestContext context = ResolveContext(ctx, out path);
            ctx.Metadata = context;

            if (context.ShouldChallenge) RecordDenial(ctx, path, "Authentication required.");

            return context;
        }

        #endregion

        #region Private-Methods

        private RequestContext ResolveContext(HttpContextBase ctx, out string path)
        {
            path = ExtractPath(ctx);
            string apiKey = GetHeader(ctx, "x-api-key");
            string bearer = ExtractBearer(ctx);
            string accessKey = GetHeader(ctx, "x-access-key");
            string secretKey = GetHeader(ctx, "x-secret-key");

            return _Engine.Authenticate(
                _Settings.Enabled, path, apiKey, _Settings.AdminApiKeys, bearer, accessKey, secretKey);
        }

        private void RecordDenial(HttpContextBase ctx, string path, string reason)
        {
            try
            {
                AuditLogEntry entry = new AuditLogEntry
                {
                    EventType = "AuthenticationFailure",
                    PrincipalType = PrincipalTypeEnum.None,
                    Method = ctx.Request.Method.ToString(),
                    Path = path,
                    IpAddress = ctx.Request.Source != null ? ctx.Request.Source.IpAddress : null,
                    AuthResult = false,
                    AuthzResult = false,
                    DenialReason = reason,
                    StatusCode = 401
                };

                _Database.Audit.Create(entry);
            }
            catch (Exception e)
            {
                _Logging.Warn(_Header + "unable to record authentication denial: " + e.Message);
            }
        }

        private static string ExtractPath(HttpContextBase ctx)
        {
            string raw = ctx.Request.Url != null ? ctx.Request.Url.RawWithQuery : "/";
            if (String.IsNullOrEmpty(raw)) return "/";
            int queryIndex = raw.IndexOf('?');
            return queryIndex >= 0 ? raw.Substring(0, queryIndex) : raw;
        }

        private static string ExtractBearer(HttpContextBase ctx)
        {
            string authorization = GetHeader(ctx, "authorization");
            if (!String.IsNullOrEmpty(authorization) &&
                authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return authorization.Substring(7).Trim();
            }

            return GetHeader(ctx, "x-token");
        }

        private static string GetHeader(HttpContextBase ctx, string name)
        {
            System.Collections.Specialized.NameValueCollection headers = ctx.Request.Headers;
            if (headers == null) return null;

            foreach (string key in headers.AllKeys)
            {
                if (key != null && key.Equals(name, StringComparison.OrdinalIgnoreCase)) return headers[key];
            }

            return null;
        }

        #endregion
    }
}
