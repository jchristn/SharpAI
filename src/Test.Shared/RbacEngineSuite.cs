namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using SharpAI.Database;
    using SharpAI.Database.Sqlite;
    using SharpAI.Security;

    using SyslogLogging;

    using Touchstone.Core;

    /// <summary>
    /// Contract suite for <see cref="RbacEngine"/> — the authorization evaluator. It seeds the built-in roles
    /// and exercises admin/tenant-admin bypass, built-in role resolution by name, explicit-deny-wins,
    /// implicit denial, tenant vs resource scope, <c>InheritsToChildren</c>, and the credential owner-ceiling,
    /// all against embedded SQLite.
    /// </summary>
    public static class RbacEngineSuite
    {
        #region Private-Members

        private static readonly object _Lock = new object();
        private static SqliteDatabaseDriver _Driver = null!;
        private static string _DbPath = null!;
        private static RbacEngine _Engine = null!;
        private static string _TenantGuid = null!;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build the RBAC engine suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor("Rbac", "Seed_BuiltIns", "Built-in roles seed idempotently",
                    ct =>
                    {
                        EnsureBase();
                        int again = RbacSeeder.Seed(Db());
                        TestAssert.Equal(0, again);
                        TestAssert.True(Db().Roles.GetBuiltInByName(BuiltInRoles.Viewer) != null, "Viewer built-in should exist");
                        TestAssert.True(Db().Roles.GetBuiltInByName(BuiltInRoles.TenantAdmin) != null, "TenantAdmin built-in should exist");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Rbac", "Admin_Bypass", "Global admin bypasses RBAC",
                    ct =>
                    {
                        EnsureBase();
                        AuthorizationRequest req = Req(PrincipalTypeEnum.User, "usr_none", ResourceTypes.Model, OperationTypeEnum.Delete);
                        req.IsAdmin = true;
                        AuthorizationDecision d = _Engine.Authorize(req);
                        TestAssert.True(d.IsPermitted && d.BypassReason == "IsAdmin", "admin should bypass");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Rbac", "TenantAdmin_Bypass", "Tenant admin bypasses RBAC",
                    ct =>
                    {
                        EnsureBase();
                        AuthorizationRequest req = Req(PrincipalTypeEnum.User, "usr_none", ResourceTypes.Settings, OperationTypeEnum.Write);
                        req.IsTenantAdmin = true;
                        AuthorizationDecision d = _Engine.Authorize(req);
                        TestAssert.True(d.IsPermitted && d.BypassReason == "IsTenantAdmin", "tenant admin should bypass");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Rbac", "Implicit_Deny", "User with no assignments is denied implicitly",
                    ct =>
                    {
                        EnsureBase();
                        string user = NewUser();
                        AuthorizationDecision d = _Engine.Authorize(Req(PrincipalTypeEnum.User, user, ResourceTypes.Model, OperationTypeEnum.Read));
                        TestAssert.True(d.Result == AuthorizationResultEnum.DeniedImplicit, "no assignments should deny implicitly");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Rbac", "Viewer_ReadOnly", "Viewer permits Read but denies Write",
                    ct =>
                    {
                        EnsureBase();
                        string user = NewUser();
                        AssignRoleByName(user, BuiltInRoles.Viewer, ResourceScopeEnum.Tenant, null, true);

                        AuthorizationDecision read = _Engine.Authorize(Req(PrincipalTypeEnum.User, user, ResourceTypes.Model, OperationTypeEnum.Read));
                        AuthorizationDecision write = _Engine.Authorize(Req(PrincipalTypeEnum.User, user, ResourceTypes.Model, OperationTypeEnum.Update));
                        TestAssert.True(read.IsPermitted, "Viewer should permit Read");
                        TestAssert.True(!write.IsPermitted, "Viewer should deny Update");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Rbac", "Editor_Write", "Editor permits Write via Write→Update expansion",
                    ct =>
                    {
                        EnsureBase();
                        string user = NewUser();
                        AssignRoleByName(user, BuiltInRoles.Editor, ResourceScopeEnum.Tenant, null, true);
                        AuthorizationDecision d = _Engine.Authorize(Req(PrincipalTypeEnum.User, user, ResourceTypes.Model, OperationTypeEnum.Update));
                        TestAssert.True(d.IsPermitted, "Editor's Write grant should cover Update");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Rbac", "Explicit_Deny_Wins", "An explicit deny overrides a permit",
                    ct =>
                    {
                        EnsureBase();
                        string user = NewUser();
                        AssignRoleByName(user, BuiltInRoles.Viewer, ResourceScopeEnum.Tenant, null, true);

                        // A tenant-scoped custom role that explicitly denies Read on Model.
                        UserRole denyRole = Db().Roles.Create(new UserRole { TenantGuid = _TenantGuid, Name = "DenyModelRead-" + Guid.NewGuid().ToString("N") });
                        Permission denyPerm = Db().Permissions.Create(new Permission
                        {
                            TenantGuid = _TenantGuid,
                            Name = "deny model read",
                            ResourceTypes = new List<string> { ResourceTypes.Model },
                            OperationTypes = new List<string> { OperationTypeEnum.Read.ToString() },
                            Effect = PermissionEffectEnum.Deny
                        });
                        Db().RolePermissionMaps.Create(new RolePermissionMap { TenantGuid = _TenantGuid, RoleGuid = denyRole.Guid, PermissionGuid = denyPerm.Guid });
                        AssignRoleByGuid(user, denyRole.Guid, ResourceScopeEnum.Tenant, null, true);

                        AuthorizationDecision d = _Engine.Authorize(Req(PrincipalTypeEnum.User, user, ResourceTypes.Model, OperationTypeEnum.Read));
                        TestAssert.True(d.Result == AuthorizationResultEnum.DeniedExplicit, "explicit deny should win over the Viewer permit");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Rbac", "Resource_Scope", "Resource-scoped grant applies only to its resource",
                    ct =>
                    {
                        EnsureBase();
                        string user = NewUser();
                        AssignRoleByName(user, BuiltInRoles.Viewer, ResourceScopeEnum.Resource, "mdl_scoped", true);

                        AuthorizationRequest onScoped = Req(PrincipalTypeEnum.User, user, ResourceTypes.Model, OperationTypeEnum.Read);
                        onScoped.ResourceGuid = "mdl_scoped";
                        AuthorizationRequest onOther = Req(PrincipalTypeEnum.User, user, ResourceTypes.Model, OperationTypeEnum.Read);
                        onOther.ResourceGuid = "mdl_other";

                        TestAssert.True(_Engine.Authorize(onScoped).IsPermitted, "grant should apply to its own resource");
                        TestAssert.True(!_Engine.Authorize(onOther).IsPermitted, "grant should not apply to a different resource");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Rbac", "Inherits_To_Children", "Tenant grant without inheritance excludes resource ops",
                    ct =>
                    {
                        EnsureBase();
                        string user = NewUser();
                        AssignRoleByName(user, BuiltInRoles.Viewer, ResourceScopeEnum.Tenant, null, false);

                        AuthorizationRequest tenantLevel = Req(PrincipalTypeEnum.User, user, ResourceTypes.Model, OperationTypeEnum.Read);
                        AuthorizationRequest resourceLevel = Req(PrincipalTypeEnum.User, user, ResourceTypes.Model, OperationTypeEnum.Read);
                        resourceLevel.ResourceGuid = "mdl_child";

                        TestAssert.True(_Engine.Authorize(tenantLevel).IsPermitted, "tenant-level op should be permitted");
                        TestAssert.True(!_Engine.Authorize(resourceLevel).IsPermitted, "resource op should be denied without inheritance");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Rbac", "Credential_Owner_Ceiling", "Credential cannot exceed its owner's access",
                    ct =>
                    {
                        EnsureBase();
                        string owner = NewUser();
                        AssignRoleByName(owner, BuiltInRoles.Viewer, ResourceScopeEnum.Tenant, null, true); // owner: read only

                        string credential = "crd_" + Guid.NewGuid().ToString("N");
                        AssignCredentialRoleByName(credential, BuiltInRoles.Editor, ResourceScopeEnum.Tenant, null, true); // credential: write

                        AuthorizationRequest write = Req(PrincipalTypeEnum.Credential, credential, ResourceTypes.Model, OperationTypeEnum.Update);
                        write.OwnerUserGuid = owner;
                        AuthorizationRequest read = Req(PrincipalTypeEnum.Credential, credential, ResourceTypes.Model, OperationTypeEnum.Read);
                        read.OwnerUserGuid = owner;

                        TestAssert.True(!_Engine.Authorize(write).IsPermitted, "owner ceiling should block the credential's write");
                        TestAssert.True(_Engine.Authorize(read).IsPermitted, "read is within both credential and owner grants");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Rbac", "Credential_Direct_Grant", "Role-less credential grant is honored",
                    ct =>
                    {
                        EnsureBase();
                        string owner = NewUser();
                        AssignRoleByName(owner, BuiltInRoles.Viewer, ResourceScopeEnum.Tenant, null, true);

                        string credential = "crd_" + Guid.NewGuid().ToString("N");
                        Db().CredentialScopeAssignments.Create(new CredentialScopeAssignment
                        {
                            TenantGuid = _TenantGuid,
                            CredentialGuid = credential,
                            Permissions = new List<string> { OperationTypeEnum.Read.ToString() },
                            ResourceTypes = new List<string> { ResourceTypes.Model }
                        });

                        AuthorizationRequest read = Req(PrincipalTypeEnum.Credential, credential, ResourceTypes.Model, OperationTypeEnum.Read);
                        read.OwnerUserGuid = owner;
                        TestAssert.True(_Engine.Authorize(read).IsPermitted, "a role-less direct grant should permit Read");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Rbac", "Owner_Admin_Ceiling_Unbounded", "Tenant-admin owner lifts the ceiling",
                    ct =>
                    {
                        EnsureBase();
                        User owner = Db().Users.Create(new User
                        {
                            TenantGuid = _TenantGuid,
                            Email = "ta-" + Guid.NewGuid().ToString("N") + "@sharpai.local",
                            PasswordSha256 = PasswordHasher.Hash("x"),
                            IsTenantAdmin = true
                        });

                        string credential = "crd_" + Guid.NewGuid().ToString("N");
                        AssignCredentialRoleByName(credential, BuiltInRoles.Editor, ResourceScopeEnum.Tenant, null, true);

                        AuthorizationRequest write = Req(PrincipalTypeEnum.Credential, credential, ResourceTypes.Model, OperationTypeEnum.Update);
                        write.OwnerUserGuid = owner.Guid;
                        TestAssert.True(_Engine.Authorize(write).IsPermitted, "a tenant-admin owner should not cap the credential");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Rbac", "Cross_Tenant_Isolation", "A grant in one tenant does not apply in another",
                    ct =>
                    {
                        EnsureBase();
                        string user = NewUser();
                        AssignRoleByName(user, BuiltInRoles.Viewer, ResourceScopeEnum.Tenant, null, true);

                        // Same principal, but evaluated against a different tenant: assignments are tenant-scoped,
                        // so no grants resolve and access is implicitly denied.
                        AuthorizationRequest otherTenant = new AuthorizationRequest
                        {
                            TenantGuid = "ten_" + Guid.NewGuid().ToString("N"),
                            PrincipalType = PrincipalTypeEnum.User,
                            PrincipalGuid = user,
                            ResourceType = ResourceTypes.Model,
                            Operation = OperationTypeEnum.Read
                        };
                        TestAssert.True(!_Engine.Authorize(otherTenant).IsPermitted, "a grant must not cross tenant boundaries");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Rbac", "Unresolvable_Role", "An assignment to a non-existent role yields no grants",
                    ct =>
                    {
                        EnsureBase();
                        string user = NewUser();
                        AssignRoleByName(user, "NoSuchRole-" + Guid.NewGuid().ToString("N"), ResourceScopeEnum.Tenant, null, true);
                        AuthorizationDecision d = _Engine.Authorize(Req(PrincipalTypeEnum.User, user, ResourceTypes.Model, OperationTypeEnum.Read));
                        TestAssert.True(d.Result == AuthorizationResultEnum.DeniedImplicit, "an unresolvable role should grant nothing");
                        return Task.CompletedTask;
                    })
            };

            return new TestSuiteDescriptor("Rbac", "RBAC engine (SQLite contract)", cases, BeforeSuiteAsync, AfterSuiteAsync);
        }

        #endregion

        #region Private-Methods

        private static ValueTask BeforeSuiteAsync(CancellationToken token)
        {
            EnsureBase();
            return new ValueTask();
        }

        private static ValueTask AfterSuiteAsync(CancellationToken token)
        {
            return new ValueTask();
        }

        private static SqliteDatabaseDriver Db()
        {
            lock (_Lock)
            {
                if (_Driver == null)
                {
                    _DbPath = Path.Combine(Path.GetTempPath(), "sharpai-rbac-" + Guid.NewGuid().ToString("N") + ".db");
                    _Driver = new SqliteDatabaseDriver(new DatabaseSettings(_DbPath), new LoggingModule());
                    _Driver.InitializeAsync().GetAwaiter().GetResult();
                    _Engine = new RbacEngine(_Driver);
                }
                return _Driver;
            }
        }

        private static void EnsureBase()
        {
            lock (_Lock)
            {
                Db();
                RbacSeeder.Seed(_Driver);
                if (_TenantGuid == null)
                {
                    Tenant tenant = Db().Tenants.Create(new Tenant { Name = "rbac-tenant" });
                    _TenantGuid = tenant.Guid;
                }
            }
        }

        private static string NewUser()
        {
            User user = Db().Users.Create(new User
            {
                TenantGuid = _TenantGuid,
                Email = "u-" + Guid.NewGuid().ToString("N") + "@sharpai.local",
                PasswordSha256 = PasswordHasher.Hash("x")
            });
            return user.Guid;
        }

        private static void AssignRoleByName(string userGuid, string roleName, ResourceScopeEnum scope, string? resourceGuid, bool inherits)
        {
            Db().UserRoleAssignments.Create(new UserRoleAssignment
            {
                TenantGuid = _TenantGuid,
                UserGuid = userGuid,
                RoleName = roleName,
                ResourceScope = scope,
                ResourceGuid = resourceGuid,
                InheritsToChildren = inherits
            });
        }

        private static void AssignRoleByGuid(string userGuid, string roleGuid, ResourceScopeEnum scope, string? resourceGuid, bool inherits)
        {
            Db().UserRoleAssignments.Create(new UserRoleAssignment
            {
                TenantGuid = _TenantGuid,
                UserGuid = userGuid,
                RoleGuid = roleGuid,
                ResourceScope = scope,
                ResourceGuid = resourceGuid,
                InheritsToChildren = inherits
            });
        }

        private static void AssignCredentialRoleByName(string credentialGuid, string roleName, ResourceScopeEnum scope, string? resourceGuid, bool inherits)
        {
            Db().CredentialScopeAssignments.Create(new CredentialScopeAssignment
            {
                TenantGuid = _TenantGuid,
                CredentialGuid = credentialGuid,
                RoleName = roleName,
                ResourceScope = scope,
                ResourceGuid = resourceGuid,
                InheritsToChildren = inherits
            });
        }

        private static AuthorizationRequest Req(PrincipalTypeEnum type, string principalGuid, string resourceType, OperationTypeEnum operation)
        {
            return new AuthorizationRequest
            {
                TenantGuid = _TenantGuid,
                PrincipalType = type,
                PrincipalGuid = principalGuid,
                ResourceType = resourceType,
                Operation = operation
            };
        }

        #endregion
    }
}
