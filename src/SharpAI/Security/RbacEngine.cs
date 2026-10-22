namespace SharpAI.Security
{
    using System;
    using System.Collections.Generic;

    using SharpAI.Database;

    /// <summary>
    /// The RBAC authorization evaluator. Given a resolved principal and a requested (resource, operation),
    /// it computes the effective permission set and applies explicit-deny-wins evaluation with tenant and
    /// resource scoping. It honors the administrator bypass rules, tenant/resource scope with
    /// <see cref="UserRoleAssignment.InheritsToChildren"/>, and the credential owner-ceiling. It is free of
    /// any HTTP framework dependency so it can be unit-tested directly against a database driver.
    /// </summary>
    public class RbacEngine
    {
        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private bool _EnforceOwnerCeiling = true;

        #endregion

        #region Public-Members

        /// <summary>
        /// Whether a credential's effective access is capped at its owning user's access (least privilege).
        /// Default true.
        /// </summary>
        public bool EnforceOwnerCeiling
        {
            get
            {
                return _EnforceOwnerCeiling;
            }
            set
            {
                _EnforceOwnerCeiling = value;
            }
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Initialized database driver. May not be null.</param>
        public RbacEngine(DatabaseDriverBase database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Evaluate an authorization request. Administrators and tenant administrators bypass RBAC (with an
        /// accounted bypass reason); otherwise the principal's effective permissions are evaluated with
        /// explicit deny overriding permit, and no matching permission yields an implicit denial.
        /// </summary>
        /// <param name="request">Authorization request. May not be null.</param>
        /// <returns>The decision. Never null.</returns>
        public AuthorizationDecision Authorize(AuthorizationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            AuthorizationDecision unconditional = new AuthorizationDecision
            {
                ResourceType = request.ResourceType,
                Operation = request.Operation
            };

            if (request.IsAdmin)
            {
                Fill(unconditional, AuthorizationResultEnum.Permitted, "Permitted by global administrator bypass.", "IsAdmin");
                return unconditional;
            }

            if (request.IsTenantAdmin)
            {
                Fill(unconditional, AuthorizationResultEnum.Permitted, "Permitted by tenant administrator bypass.", "IsTenantAdmin");
                return unconditional;
            }

            if (request.PrincipalType == PrincipalTypeEnum.Credential)
            {
                return AuthorizeCredential(request);
            }

            List<EffectivePermission> permissions = GetEffectivePermissions(PrincipalTypeEnum.User, request.PrincipalGuid, request.TenantGuid);
            return Decide(Evaluate(permissions, request), request);
        }

        /// <summary>
        /// Compute the effective permission set for a principal within a tenant. For credentials this is the
        /// credential's own grants (before the owner-ceiling is applied at decision time).
        /// </summary>
        /// <param name="principalType">Principal type (User or Credential).</param>
        /// <param name="principalGuid">Principal identifier.</param>
        /// <param name="tenantGuid">Tenant identifier.</param>
        /// <returns>Effective permissions; never null.</returns>
        public List<EffectivePermission> GetEffectivePermissions(PrincipalTypeEnum principalType, string principalGuid, string tenantGuid)
        {
            List<EffectivePermission> effective = new List<EffectivePermission>();
            if (String.IsNullOrEmpty(principalGuid) || String.IsNullOrEmpty(tenantGuid)) return effective;

            if (principalType == PrincipalTypeEnum.Credential)
            {
                List<CredentialScopeAssignment> assignments = _Database.CredentialScopeAssignments.GetForCredential(tenantGuid, principalGuid);
                foreach (CredentialScopeAssignment assignment in assignments)
                {
                    ExpandRole(effective, assignment.RoleGuid, assignment.RoleName, tenantGuid, assignment.ResourceScope, assignment.ResourceGuid, assignment.InheritsToChildren);
                    ExpandDirect(effective, assignment);
                }
            }
            else
            {
                List<UserRoleAssignment> assignments = _Database.UserRoleAssignments.GetForUser(tenantGuid, principalGuid);
                foreach (UserRoleAssignment assignment in assignments)
                {
                    ExpandRole(effective, assignment.RoleGuid, assignment.RoleName, tenantGuid, assignment.ResourceScope, assignment.ResourceGuid, assignment.InheritsToChildren);
                }
            }

            return effective;
        }

        #endregion

        #region Private-Methods

        private AuthorizationDecision AuthorizeCredential(AuthorizationRequest request)
        {
            List<EffectivePermission> credentialPermissions = GetEffectivePermissions(PrincipalTypeEnum.Credential, request.PrincipalGuid, request.TenantGuid);
            AuthorizationResultEnum credentialResult = Evaluate(credentialPermissions, request);

            if (credentialResult != AuthorizationResultEnum.Permitted)
            {
                return Decide(credentialResult, request);
            }

            if (!_EnforceOwnerCeiling || String.IsNullOrEmpty(request.OwnerUserGuid))
            {
                return Decide(AuthorizationResultEnum.Permitted, request);
            }

            User owner = _Database.Users.Read(request.OwnerUserGuid);
            if (owner != null && (owner.IsAdmin || owner.IsTenantAdmin))
            {
                return Decide(AuthorizationResultEnum.Permitted, request);
            }

            List<EffectivePermission> ownerPermissions = GetEffectivePermissions(PrincipalTypeEnum.User, request.OwnerUserGuid, request.TenantGuid);
            AuthorizationResultEnum ownerResult = Evaluate(ownerPermissions, request);
            if (ownerResult == AuthorizationResultEnum.Permitted)
            {
                return Decide(AuthorizationResultEnum.Permitted, request);
            }

            AuthorizationDecision ceiling = new AuthorizationDecision
            {
                Result = AuthorizationResultEnum.DeniedImplicit,
                Reason = "Denied by credential owner ceiling (the owning user lacks this permission).",
                ResourceType = request.ResourceType,
                Operation = request.Operation
            };
            return ceiling;
        }

        private void ExpandRole(
            List<EffectivePermission> sink,
            string roleGuid,
            string roleName,
            string tenantGuid,
            ResourceScopeEnum scope,
            string resourceGuid,
            bool inheritsToChildren)
        {
            UserRole role = ResolveRole(roleGuid, roleName, tenantGuid);
            if (role == null || !role.Active || (role.TenantGuid != null && role.TenantGuid != tenantGuid)) return;

            List<Permission> permissions = _Database.Permissions.GetForRole(role.Guid);
            foreach (Permission permission in permissions)
            {
                if (!permission.Active || (permission.TenantGuid != null && permission.TenantGuid != tenantGuid)) continue;

                foreach (string resourceType in permission.ResourceTypes)
                {
                    foreach (string operationName in permission.OperationTypes)
                    {
                        if (!Enum.TryParse<OperationTypeEnum>(operationName, true, out OperationTypeEnum operation)) continue;

                        sink.Add(new EffectivePermission
                        {
                            ResourceType = resourceType,
                            Operation = operation,
                            Effect = permission.Effect,
                            ResourceScope = scope,
                            ResourceGuid = resourceGuid,
                            InheritsToChildren = inheritsToChildren
                        });
                    }
                }
            }
        }

        private static void ExpandDirect(List<EffectivePermission> sink, CredentialScopeAssignment assignment)
        {
            if (assignment.ResourceTypes.Count == 0 || assignment.Permissions.Count == 0) return;

            foreach (string resourceType in assignment.ResourceTypes)
            {
                foreach (string operationName in assignment.Permissions)
                {
                    if (!Enum.TryParse<OperationTypeEnum>(operationName, true, out OperationTypeEnum operation)) continue;

                    sink.Add(new EffectivePermission
                    {
                        ResourceType = resourceType,
                        Operation = operation,
                        Effect = PermissionEffectEnum.Permit,
                        ResourceScope = assignment.ResourceScope,
                        ResourceGuid = assignment.ResourceGuid,
                        InheritsToChildren = assignment.InheritsToChildren
                    });
                }
            }
        }

        private UserRole ResolveRole(string roleGuid, string roleName, string tenantGuid)
        {
            if (!String.IsNullOrEmpty(roleGuid))
            {
                UserRole byGuid = _Database.Roles.Read(roleGuid);
                if (byGuid != null) return byGuid;
            }

            if (!String.IsNullOrEmpty(roleName))
            {
                UserRole byName = _Database.Roles.GetByName(tenantGuid, roleName);
                if (byName != null) return byName;
                return _Database.Roles.GetBuiltInByName(roleName);
            }

            return null;
        }

        private static AuthorizationResultEnum Evaluate(List<EffectivePermission> permissions, AuthorizationRequest request)
        {
            bool permitted = false;

            foreach (EffectivePermission permission in permissions)
            {
                if (!Applies(permission, request)) continue;
                if (permission.Effect == PermissionEffectEnum.Deny) return AuthorizationResultEnum.DeniedExplicit;
                permitted = true;
            }

            return permitted ? AuthorizationResultEnum.Permitted : AuthorizationResultEnum.DeniedImplicit;
        }

        private static bool Applies(EffectivePermission permission, AuthorizationRequest request)
        {
            if (!ResourceTypeMatches(permission.ResourceType, request.ResourceType)) return false;
            if (!OperationMatches(permission.Operation, request.Operation)) return false;
            return ScopeMatches(permission, request);
        }

        private static bool ResourceTypeMatches(string granted, string requested)
        {
            if (String.Equals(granted, ResourceTypes.All, StringComparison.OrdinalIgnoreCase)) return true;
            return String.Equals(granted, requested, StringComparison.OrdinalIgnoreCase);
        }

        private static bool OperationMatches(OperationTypeEnum granted, OperationTypeEnum requested)
        {
            if (granted == OperationTypeEnum.All) return true;
            if (granted == requested) return true;
            if (granted == OperationTypeEnum.Write &&
                (requested == OperationTypeEnum.Create || requested == OperationTypeEnum.Update || requested == OperationTypeEnum.Delete))
            {
                return true;
            }
            return false;
        }

        private static bool ScopeMatches(EffectivePermission permission, AuthorizationRequest request)
        {
            if (permission.ResourceScope == ResourceScopeEnum.Resource)
            {
                return !String.IsNullOrEmpty(request.ResourceGuid) &&
                       String.Equals(permission.ResourceGuid, request.ResourceGuid, StringComparison.Ordinal);
            }

            // Tenant scope: tenant-level operations (no target resource) always apply; operations that target
            // a specific resource apply only when the grant inherits to child resources.
            if (String.IsNullOrEmpty(request.ResourceGuid)) return true;
            return permission.InheritsToChildren;
        }

        private static AuthorizationDecision Decide(AuthorizationResultEnum result, AuthorizationRequest request)
        {
            AuthorizationDecision decision = new AuthorizationDecision
            {
                Result = result,
                ResourceType = request.ResourceType,
                Operation = request.Operation
            };

            switch (result)
            {
                case AuthorizationResultEnum.Permitted:
                    decision.Reason = "Permitted by a matching grant.";
                    break;
                case AuthorizationResultEnum.DeniedExplicit:
                    decision.Reason = "Denied by an explicit deny permission.";
                    break;
                default:
                    decision.Reason = "Denied: no permission grants the requested operation on this resource.";
                    break;
            }

            return decision;
        }

        private static void Fill(AuthorizationDecision decision, AuthorizationResultEnum result, string reason, string bypassReason)
        {
            decision.Result = result;
            decision.Reason = reason;
            decision.BypassReason = bypassReason;
        }

        #endregion
    }
}
