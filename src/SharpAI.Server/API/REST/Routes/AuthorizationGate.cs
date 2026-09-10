namespace SharpAI.Server.API.REST.Routes
{
    using System;

    using SharpAI.Database;
    using SharpAI.Security;
    using SharpAI.Server.Classes.Settings;
    using SyslogLogging;
    using WatsonWebserver.Core;

    /// <summary>
    /// Centralized RBAC enforcement for route handlers, extracted from the composition root so per-feature
    /// registrars share one implementation. When authentication is disabled every request runs as the
    /// implicit system principal and authorization is a no-op (Ollama-parity open access); when enabled,
    /// administrators bypass, every other principal is evaluated against its effective permissions, and a
    /// denial is audited and surfaced as HTTP 403.
    /// </summary>
    public class AuthorizationGate
    {
        #region Private-Members

        private readonly Func<Settings> _Settings;
        private readonly RbacEngine _Rbac;
        private readonly DatabaseDriverBase _Database;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[AuthorizationGate] ";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the authorization gate.
        /// </summary>
        /// <param name="settings">Accessor returning the current settings. Required.</param>
        /// <param name="rbac">RBAC engine. Required.</param>
        /// <param name="database">Database driver (for audit). Required.</param>
        /// <param name="logging">Logging module. Required.</param>
        public AuthorizationGate(Func<Settings> settings, RbacEngine rbac, DatabaseDriverBase database, LoggingModule logging)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Rbac = rbac ?? throw new ArgumentNullException(nameof(rbac));
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Enforce RBAC for a route, throwing 401 to challenge an unauthenticated caller or 403 on denial.
        /// </summary>
        /// <param name="req">API request.</param>
        /// <param name="resourceType">Resource type being accessed.</param>
        /// <param name="operation">Operation being performed.</param>
        /// <param name="resourceGuid">Specific resource GUID, or null.</param>
        public void Authorize(ApiRequest req, string resourceType, OperationTypeEnum operation, string resourceGuid)
        {
            RequestContext context = req.Http.Metadata as RequestContext;

            if (context == null)
            {
                if (!_Settings().Auth.Enabled) return;
                throw new WebserverException(ApiResultEnum.NotAuthorized, "Authentication required.");
            }

            if (context.ShouldChallenge)
                throw new WebserverException(ApiResultEnum.NotAuthorized, "Authentication required.");

            if (context.IsAdmin) return;

            AuthorizationRequest ar = new AuthorizationRequest
            {
                TenantGuid = context.TenantGuid,
                PrincipalType = context.PrincipalType,
                PrincipalGuid = context.PrincipalGuid,
                IsAdmin = context.IsAdmin,
                IsTenantAdmin = context.IsTenantAdmin,
                ResourceType = resourceType,
                Operation = operation,
                ResourceGuid = resourceGuid,
                OwnerUserGuid = context.OwnerUserGuid
            };

            AuthorizationDecision decision = _Rbac.Authorize(ar);
            if (!decision.IsPermitted)
            {
                RecordAuthzDenial(req.Http, context, resourceType, operation, decision);
                throw new WebserverException(ApiResultEnum.Forbidden, decision.Reason);
            }
        }

        /// <summary>
        /// Authorize an effective-permissions inspection: a principal may read its own permissions; otherwise
        /// administrator privileges are required.
        /// </summary>
        /// <param name="req">API request.</param>
        /// <param name="tenantGuid">Tenant scope.</param>
        /// <param name="principalType">Principal type being inspected.</param>
        /// <param name="principalGuid">Principal GUID being inspected.</param>
        public void AuthorizeInspection(ApiRequest req, string tenantGuid, PrincipalTypeEnum principalType, string principalGuid)
        {
            RequestContext context = req.Http.Metadata as RequestContext;

            if (context == null)
            {
                if (!_Settings().Auth.Enabled) return;
                throw new WebserverException(ApiResultEnum.NotAuthorized, "Authentication required.");
            }

            if (context.ShouldChallenge)
                throw new WebserverException(ApiResultEnum.NotAuthorized, "Authentication required.");

            if (context.IsAdmin) return;

            if (context.PrincipalType == principalType
                && !String.IsNullOrEmpty(principalGuid)
                && String.Equals(context.PrincipalGuid, principalGuid, StringComparison.Ordinal)
                && String.Equals(context.TenantGuid, tenantGuid, StringComparison.Ordinal))
            {
                return;
            }

            Authorize(req, ResourceTypes.Admin, OperationTypeEnum.Admin, null);
        }

        #endregion

        #region Private-Methods

        private void RecordAuthzDenial(HttpContextBase ctx, RequestContext context, string resourceType, OperationTypeEnum operation, AuthorizationDecision decision)
        {
            try
            {
                string path = ctx.Request.Url != null ? ctx.Request.Url.RawWithQuery : null;
                if (!String.IsNullOrEmpty(path))
                {
                    int q = path.IndexOf('?');
                    if (q >= 0) path = path.Substring(0, q);
                }

                AuditLogEntry entry = new AuditLogEntry
                {
                    TenantGuid = context.TenantGuid,
                    EventType = "AuthorizationDenied",
                    PrincipalType = context.PrincipalType,
                    PrincipalGuid = context.PrincipalGuid,
                    Method = ctx.Request.Method.ToString(),
                    Path = path,
                    IpAddress = ctx.Request.Source != null ? ctx.Request.Source.IpAddress : null,
                    AuthResult = true,
                    AuthzResult = false,
                    DenialReason = resourceType + ":" + operation + " — " + decision.Reason,
                    StatusCode = 403
                };
                _Database.Audit.Create(entry);
            }
            catch (Exception e)
            {
                _Logging.Warn(_Header + "unable to record authorization denial: " + e.Message);
            }
        }

        #endregion
    }
}
