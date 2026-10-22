namespace SharpAI.Server.API.REST.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using SharpAI.Database;
    using SharpAI.Models;
    using SharpAI.Security;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>Tenant-scoped administration workflows and dashboard authorization context.</summary>
    public static class AdministrationRoutes
    {
        /// <summary>Register administration endpoints.</summary>
        /// <param name="ctx">Route dependencies.</param><param name="rbac">Permission evaluator.</param>
        public static void Register(RouteContext ctx, RbacEngine rbac)
        {
            ctx.Server.Get("/v1.0/auth/context", async req =>
            {
                RequestContext principal = req.Http.Metadata as RequestContext;
                if (ctx.Settings().Auth.Enabled && (principal == null || principal.ShouldChallenge))
                    throw new WebserverException(ApiResultEnum.NotAuthorized, "Authentication required.");
                bool platform = !ctx.Settings().Auth.Enabled || principal?.IsAdmin == true;
                Dictionary<string, object> capabilities = new Dictionary<string, object>();
                foreach (string resource in new[] { "Tenant", "User", "Credential", "Role", "Permission", "Assignment", "Session", "Audit", "Admin" })
                {
                    bool Can(OperationTypeEnum operation) => platform || (resource != "Tenant" && rbac.Authorize(new AuthorizationRequest
                    {
                        TenantGuid = principal?.TenantGuid, PrincipalType = principal?.PrincipalType ?? PrincipalTypeEnum.None,
                        PrincipalGuid = principal?.PrincipalGuid, OwnerUserGuid = principal?.OwnerUserGuid,
                        IsTenantAdmin = principal?.IsTenantAdmin == true, ResourceType = resource, Operation = operation
                    }).IsPermitted);
                    capabilities[resource] = new { read = Can(OperationTypeEnum.Read), write = Can(OperationTypeEnum.Admin) };
                }
                return new { authEnabled = ctx.Settings().Auth.Enabled, isAdmin = platform, isTenantAdmin = principal?.IsTenantAdmin == true,
                    tenantId = principal?.TenantGuid, tenantName = String.IsNullOrEmpty(principal?.TenantGuid) ? null : ctx.Database.Tenants.Read(principal.TenantGuid)?.Name, userId = principal?.PrincipalGuid, capabilities };
            }, api => Meta(api, "Read dashboard authorization context"));

            ctx.Server.Put<UpdateRequest>("/v1.0/tenants/{tenantGuid}", async req =>
            {
                ctx.Auth.RequirePlatformAdministrator(req);
                Tenant item = ctx.Database.Tenants.Read(Param(req, "tenantGuid"));
                if (item == null) throw Missing();
                UpdateRequest body = req.GetData<UpdateRequest>();
                if (item.IsProtected) throw Protected();
                if (String.IsNullOrWhiteSpace(body.Name)) throw Bad("A name is required.");
                Tenant duplicate = ctx.Database.Tenants.GetByName(body.Name.Trim());
                if (duplicate != null && duplicate.Guid != item.Guid) throw new WebserverException(ApiResultEnum.Conflict, "Tenant name already exists.");
                item.Name = body.Name.Trim(); item.Active = body.Active ?? item.Active;
                ctx.Database.Tenants.Update(item); Audit(ctx, req, item.Guid, "TenantUpdated"); return item;
            }, api => Meta(api, "Update a tenant"));

            ctx.Server.Put<UpdateRequest>("/v1.0/tenants/{tenantGuid}/users/{userGuid}", async req =>
            {
                string tenant = TenantScope(ctx, req, ResourceTypes.User);
                User item = ctx.Database.Users.Read(Param(req, "userGuid"));
                if (item == null || item.TenantGuid != tenant) throw Missing();
                UpdateRequest body = req.GetData<UpdateRequest>();
                if (item.IsProtected) throw Protected();
                if (item.IsAdmin || body.IsAdmin == true) ctx.Auth.RequirePlatformAdministrator(req);
                if (String.IsNullOrWhiteSpace(body.Email)) throw Bad("An email is required.");
                User duplicate = ctx.Database.Users.GetByEmail(tenant, body.Email.Trim());
                if (duplicate != null && duplicate.Guid != item.Guid) throw new WebserverException(ApiResultEnum.Conflict, "Email already exists in this tenant.");
                item.Email = body.Email.Trim(); item.FirstName = body.FirstName ?? item.FirstName; item.LastName = body.LastName ?? item.LastName;
                item.IsAdmin = body.IsAdmin ?? item.IsAdmin; item.IsTenantAdmin = body.IsTenantAdmin ?? item.IsTenantAdmin; item.Active = body.Active ?? item.Active;
                ctx.Database.Users.Update(item); item.PasswordSha256 = null; Audit(ctx, req, tenant, "UserUpdated"); return item;
            }, api => Meta(api, "Update a user"));

            ctx.Server.Put<UpdateRequest>("/v1.0/tenants/{tenantGuid}/users/{userGuid}/password", async req =>
            {
                string tenant = TenantScope(ctx, req, ResourceTypes.User);
                User item = ctx.Database.Users.Read(Param(req, "userGuid"));
                if (item == null || item.TenantGuid != tenant) throw Missing();
                if (item.IsAdmin || item.IsProtected) ctx.Auth.RequirePlatformAdministrator(req);
                string password = req.GetData<UpdateRequest>().Password;
                if (String.IsNullOrWhiteSpace(password)) throw Bad("A password is required.");
                item.PasswordSha256 = PasswordHasher.Hash(password); ctx.Database.Users.Update(item);
                EnumerationResult<AuthSession> sessions = ctx.Database.Sessions.Enumerate(tenant, new EnumerationQuery { PageSize = 1000 });
                // Revoke every session, including pages beyond the first, after a password reset.
                int page = 1;
                while (true)
                {
                    foreach (AuthSession session in sessions.Objects.Where(s => s.UserGuid == item.Guid)) ctx.Database.Sessions.Revoke(session.Guid, "Password reset.");
                    if (sessions.EndOfResults) break;
                    sessions = ctx.Database.Sessions.Enumerate(tenant, new EnumerationQuery { PageSize = 1000, PageNumber = ++page });
                }
                Audit(ctx, req, tenant, "PasswordReset"); return new { updated = true };
            }, api => Meta(api, "Reset a user's password and revoke sessions"));

            ctx.Server.Put<UpdateRequest>("/v1.0/tenants/{tenantGuid}/credentials/{credentialGuid}", async req =>
            {
                string tenant = TenantScope(ctx, req, ResourceTypes.Credential);
                Credential item = ctx.Database.Credentials.Read(Param(req, "credentialGuid"));
                if (item == null || item.TenantGuid != tenant) throw Missing();
                if (item.IsProtected) throw Protected();
                UpdateRequest body = req.GetData<UpdateRequest>();
                if (String.IsNullOrWhiteSpace(body.Name)) throw Bad("A name is required.");
                item.Name = body.Name.Trim(); item.Active = body.Active ?? item.Active;
                if (body.ClearExpiration) item.ExpiresUtc = null; else if (body.ExpiresUtc.HasValue) item.ExpiresUtc = body.ExpiresUtc;
                ctx.Database.Credentials.Update(item); item.SecretSha256 = null; Audit(ctx, req, tenant, "CredentialUpdated"); return item;
            }, api => Meta(api, "Update credential metadata, expiration, or active status"));

            ctx.Server.Put<UpdateRequest>("/v1.0/tenants/{tenantGuid}/roles/{roleGuid}", async req =>
            {
                string tenant = TenantScope(ctx, req, ResourceTypes.Role);
                UserRole role = OwnRole(ctx.Database, tenant, Param(req, "roleGuid"), true);
                UpdateRequest body = req.GetData<UpdateRequest>();
                if (String.IsNullOrWhiteSpace(body.Name)) throw Bad("A name is required.");
                role.Name = body.Name.Trim(); role.Active = body.Active ?? role.Active;
                ctx.Database.Roles.Update(role); Audit(ctx, req, tenant, "RoleUpdated"); return role;
            }, api => Meta(api, "Update a custom role"));

            ctx.Server.Get("/v1.0/tenants/{tenantGuid}/roles/{roleGuid}/permissions", async req =>
            {
                string tenant = TenantScope(ctx, req, ResourceTypes.Role, OperationTypeEnum.Read);
                UserRole role = OwnRole(ctx.Database, tenant, Param(req, "roleGuid"), false);
                return ctx.Database.RolePermissionMaps.GetByRole(role.Guid).Select(map => ctx.Database.Permissions.Read(map.PermissionGuid)).Where(permission => permission != null && (permission.TenantGuid == null || permission.TenantGuid == tenant)).ToList();
            }, api => Meta(api, "List a role's mapped permissions"));

            ctx.Server.Delete("/v1.0/tenants/{tenantGuid}/roles/{roleGuid}/permissions/{permissionGuid}", async req =>
            {
                string tenant = TenantScope(ctx, req, ResourceTypes.Role);
                UserRole role = OwnRole(ctx.Database, tenant, Param(req, "roleGuid"), true);
                foreach (RolePermissionMap map in ctx.Database.RolePermissionMaps.GetByRole(role.Guid).Where(m => m.PermissionGuid == Param(req, "permissionGuid")))
                {
                    if (map.IsProtected) throw Protected();
                    ctx.Database.RolePermissionMaps.Delete(map.Guid);
                }
                Audit(ctx, req, tenant, "RolePermissionRemoved"); return new { deleted = true };
            }, api => Meta(api, "Remove a custom role's permission mapping"));

            ctx.Server.Put<Permission>("/v1.0/tenants/{tenantGuid}/permissions/{permissionGuid}", async req =>
            {
                string tenant = TenantScope(ctx, req, ResourceTypes.Permission);
                Permission item = ctx.Database.Permissions.Read(Param(req, "permissionGuid"));
                if (item == null || item.TenantGuid != tenant) throw Missing();
                if (item.IsProtected) throw Protected();
                Permission body = req.GetData<Permission>();
                ValidatePermission(body);
                item.Name = body.Name; item.ResourceTypes = body.ResourceTypes; item.OperationTypes = body.OperationTypes; item.Effect = body.Effect; item.Active = body.Active;
                ctx.Database.Permissions.Update(item); Audit(ctx, req, tenant, "PermissionUpdated"); return item;
            }, api => Meta(api, "Update a custom permission"));

            ctx.Server.Delete("/v1.0/tenants/{tenantGuid}/permissions/{permissionGuid}", async req =>
            {
                string tenant = TenantScope(ctx, req, ResourceTypes.Permission);
                Permission item = ctx.Database.Permissions.Read(Param(req, "permissionGuid"));
                if (item == null || item.TenantGuid != tenant) throw Missing();
                if (item.IsProtected) throw Protected();
                ctx.Database.Permissions.Delete(item.Guid); Audit(ctx, req, tenant, "PermissionDeleted"); return new { deleted = true };
            }, api => Meta(api, "Delete a custom permission"));

            ctx.Server.Get("/v1.0/tenants/{tenantGuid}/credentials/{credentialGuid}/assignments", async req =>
            {
                string tenant = TenantScope(ctx, req, ResourceTypes.Assignment, OperationTypeEnum.Read);
                Credential item = ctx.Database.Credentials.Read(Param(req, "credentialGuid"));
                if (item == null || item.TenantGuid != tenant) throw Missing();
                return ctx.Database.CredentialScopeAssignments.GetForCredential(tenant, item.Guid);
            }, api => Meta(api, "List a credential's scope assignments"));

            ctx.Server.Post<CredentialScopeAssignment>("/v1.0/tenants/{tenantGuid}/credentials/{credentialGuid}/assignments", async req =>
            {
                string tenant = TenantScope(ctx, req, ResourceTypes.Assignment);
                Credential credential = ctx.Database.Credentials.Read(Param(req, "credentialGuid"));
                if (credential == null || credential.TenantGuid != tenant) throw Missing();
                CredentialScopeAssignment body = req.GetData<CredentialScopeAssignment>();
                ValidateAssignment(ctx.Database, tenant, body.RoleGuid, body.RoleName, body.ResourceScope, body.ResourceGuid);
                CredentialScopeAssignment item = new CredentialScopeAssignment
                {
                    TenantGuid = tenant, CredentialGuid = credential.Guid, RoleGuid = body.RoleGuid, RoleName = body.RoleName,
                    ResourceScope = body.ResourceScope, ResourceGuid = body.ResourceGuid, InheritsToChildren = body.InheritsToChildren
                };
                ctx.Database.CredentialScopeAssignments.Create(item); Audit(ctx, req, tenant, "CredentialScopeGranted"); return item;
            }, api => Meta(api, "Grant a role-scoped credential assignment"));

            ctx.Server.Delete("/v1.0/tenants/{tenantGuid}/credentials/{credentialGuid}/assignments/{assignmentGuid}", async req =>
            {
                string tenant = TenantScope(ctx, req, ResourceTypes.Assignment);
                CredentialScopeAssignment item = ctx.Database.CredentialScopeAssignments.Read(Param(req, "assignmentGuid"));
                if (item == null || item.TenantGuid != tenant || item.CredentialGuid != Param(req, "credentialGuid")) throw Missing();
                if (item.IsProtected) throw Protected();
                ctx.Database.CredentialScopeAssignments.Delete(item.Guid); Audit(ctx, req, tenant, "CredentialScopeRevoked"); return new { deleted = true };
            }, api => Meta(api, "Revoke a credential scope assignment"));

            ctx.Server.Get("/v1.0/tenants/{tenantGuid}/sessions", async req =>
            {
                string tenant = TenantScope(ctx, req, ResourceTypes.Session, OperationTypeEnum.Read);
                return ctx.Database.Sessions.Enumerate(tenant, RouteHelpers.ParseEnumQuery(req));
            }, api => Meta(api, "List tenant sessions without token material"));
            ctx.Server.Delete("/v1.0/tenants/{tenantGuid}/sessions/{sessionGuid}", async req =>
            {
                string tenant = TenantScope(ctx, req, ResourceTypes.Session);
                AuthSession session = ctx.Database.Sessions.Read(Param(req, "sessionGuid"));
                if (session == null || session.TenantGuid != tenant) throw Missing();
                if (session.IsProtected) throw Protected();
                ctx.Database.Sessions.Revoke(session.Guid, "Revoked by administrator."); Audit(ctx, req, tenant, "SessionRevoked"); return new { revoked = true };
            }, api => Meta(api, "Revoke a tenant session"));
        }

        /// <summary>Validate referenced role ownership and resource scope before publishing a grant.</summary>
        /// <param name="db">Database.</param><param name="tenant">Tenant.</param><param name="roleGuid">Role ID.</param>
        /// <param name="roleName">Built-in role name fallback.</param><param name="scope">Scope.</param><param name="resource">Resource ID.</param>
        public static void ValidateAssignment(DatabaseDriverBase db, string tenant, string roleGuid, string roleName, ResourceScopeEnum scope, string resource)
        {
            UserRole role = !String.IsNullOrEmpty(roleGuid) ? db.Roles.Read(roleGuid) : db.Roles.GetBuiltInByName(roleName ?? String.Empty);
            if (role == null || !role.Active || (role.TenantGuid != null && role.TenantGuid != tenant)) throw Bad("The role is not available in this tenant.");
            if (!Enum.IsDefined(typeof(ResourceScopeEnum), scope) || (scope == ResourceScopeEnum.Resource && String.IsNullOrWhiteSpace(resource))) throw Bad("A resource ID is required for resource scope.");
        }

        /// <summary>Validate supported operations and permission effects.</summary>
        /// <param name="permission">Requested permission.</param>
        public static void ValidatePermission(Permission permission)
        {
            if (permission == null || permission.ResourceTypes.Count == 0 || permission.OperationTypes.Count == 0 ||
                permission.ResourceTypes.Any(String.IsNullOrWhiteSpace) || !Enum.IsDefined(typeof(PermissionEffectEnum), permission.Effect)) throw Bad("Valid resource types, operations, and effect are required.");
            foreach (string operation in permission.OperationTypes)
                if (!Enum.TryParse(operation, true, out OperationTypeEnum parsed) || !Enum.IsDefined(typeof(OperationTypeEnum), parsed)) throw Bad("Unknown permission operation.");
        }

        private static string Param(ApiRequest req, string name) => RouteHelpers.RouteParam(req, name);
        private static WebserverException Missing() => new WebserverException(ApiResultEnum.NotFound, "The requested record was not found in this tenant.");
        private static WebserverException Protected() => new WebserverException(ApiResultEnum.Forbidden, "This record is protected or built-in.");
        private static WebserverException Bad(string message) => new WebserverException(ApiResultEnum.BadRequest, message);
        private static string TenantScope(RouteContext ctx, ApiRequest req, string resource, OperationTypeEnum operation = OperationTypeEnum.Admin)
        {
            string tenant = Param(req, "tenantGuid");
            RequestContext principal = req.Http.Metadata as RequestContext;
            ctx.Auth.RequireTenant(req, tenant);
            ctx.Auth.Authorize(req, resource, operation, null);
            if (ctx.Database.Tenants.Read(tenant) == null) throw Missing();
            return tenant;
        }
        private static UserRole OwnRole(DatabaseDriverBase db, string tenant, string guid, bool mutate)
        {
            UserRole role = db.Roles.Read(guid);
            if (role == null || (role.TenantGuid != null && role.TenantGuid != tenant)) throw Missing();
            if (mutate && (role.IsBuiltIn || role.IsProtected || role.TenantGuid == null)) throw Protected();
            return role;
        }
        private static OpenApiRouteMetadata Meta(OpenApiRouteMetadata api, string summary) => RouteHelpers.Describe(api, "Administration", summary)
            .WithResponse(200, OpenApiResponseMetadata.Json(summary, OpenApiSchemaMetadata.Create("object")))
            .WithResponse(400, OpenApiResponseMetadata.BadRequest()).WithResponse(403, OpenApiResponseMetadata.Forbidden()).WithResponse(404, OpenApiResponseMetadata.NotFound());
        internal static void RecordChange(RouteContext ctx, ApiRequest req, string tenant, string action) => Audit(ctx, req, tenant, action);

        private static void Audit(RouteContext ctx, ApiRequest req, string tenant, string action)
        {
            RequestContext principal = req.Http.Metadata as RequestContext;
            ctx.Database.Audit.Create(new AuditLogEntry { TenantGuid = tenant, EventType = action, PrincipalGuid = principal?.PrincipalGuid,
                PrincipalType = principal?.PrincipalType ?? PrincipalTypeEnum.None, Method = req.Http.Request.Method.ToString(), Path = req.Http.Request.Url.RawWithQuery, AuthResult = true, AuthzResult = true, StatusCode = 200 });
        }
    }

    /// <summary>Editable metadata only. Immutable IDs and protection flags are never accepted.</summary>
    public sealed class UpdateRequest
    {
        /// <summary>Display name.</summary>
        public string Name { get; set; }
        /// <summary>Email address.</summary>
        public string Email { get; set; }
        /// <summary>First name.</summary>
        public string FirstName { get; set; }
        /// <summary>Last name.</summary>
        public string LastName { get; set; }
        /// <summary>New password, used only by the password reset route.</summary>
        public string Password { get; set; }
        /// <summary>Platform administrator flag.</summary>
        public bool? IsAdmin { get; set; }
        /// <summary>Tenant administrator flag.</summary>
        public bool? IsTenantAdmin { get; set; }
        /// <summary>Whether the record is active.</summary>
        public bool? Active { get; set; }
        /// <summary>Credential expiration.</summary>
        public DateTime? ExpiresUtc { get; set; }
        /// <summary>Explicitly clear a credential expiration.</summary>
        public bool ClearExpiration { get; set; }
    }
}
