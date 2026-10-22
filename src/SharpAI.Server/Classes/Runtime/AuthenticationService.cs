namespace SharpAI.Server.Classes.Runtime
{
    using System;

    using SharpAI.Database;
    using SharpAI.Security;
    using SharpAI.Server.Classes.Settings;

    using SyslogLogging;

    using WatsonWebserver.Core;

    /// <summary>
    /// Resolves bearer authentication for every server request. Only exact health/readiness probes
    /// and the login operation are anonymous. PreRouting sends authentication challenges before routing;
    /// guarded routes apply RBAC to the attached principal. Legacy key headers are not accepted.
    /// </summary>
    public class AuthenticationService
    {
        #region Private-Members

        private readonly string _Header = "[Auth] ";
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
            ArgumentNullException.ThrowIfNull(settings);
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
        /// actual 401 is sent by PreRouting).
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
            // Only exact infrastructure probes and the login operation are anonymous.
            bool anonymous = path == "/health" || path == "/ready"
                || (path == "/v1.0/token" && ctx.Request.Method.ToString() == "POST");
            if (anonymous) return new RequestContext { IsAnonymousEndpoint = true };
            // Resolve against a protected path so the library's legacy anonymous endpoints cannot
            // expose server API documentation, metrics, or token/session operations.
            return _Engine.Authenticate(true, "/__authenticated", null, null, ExtractBearer(ctx), null, null);
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
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "unable to record authentication denial: " + e.Message);
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

            return null;
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
