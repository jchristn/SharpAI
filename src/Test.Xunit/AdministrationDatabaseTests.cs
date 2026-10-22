namespace Test.Xunit
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Threading.Tasks;
    using SharpAI.Database;
    using SharpAI.Database.Sqlite;
    using SharpAI.Models;
    using SharpAI.Security;
    using SyslogLogging;
    using Xunit;

    /// <summary>Security forms use named enums, portable permission updates, and tenant-isolated session pages.</summary>
    public sealed class AdministrationDatabaseTests
    {
        /// <summary>Named scope and effect values must be accepted by the HTTP serializer.</summary>
        [Fact]
        public void SecurityEnumsRoundTripThroughNamedJson()
        {
            Permission permission = JsonSerializer.Deserialize<Permission>("{\"Effect\":\"Deny\"}")!;
            CredentialScopeAssignment assignment = JsonSerializer.Deserialize<CredentialScopeAssignment>("{\"ResourceScope\":\"Resource\"}")!;
            Assert.Equal(PermissionEffectEnum.Deny, permission.Effect);
            Assert.Equal(ResourceScopeEnum.Resource, assignment.ResourceScope);
            Assert.Contains("\"Deny\"", JsonSerializer.Serialize(permission));
        }

        /// <summary>Session totals, page boundaries, and permission updates preserve tenant ownership.</summary>
        [Fact]
        public async Task SessionPagesAndPermissionEditsAreTenantScoped()
        {
            string filename = Path.Combine(Path.GetTempPath(), "sharpai-admin-" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                using (LoggingModule logging = new LoggingModule())
                using (SqliteDatabaseDriver db = new SqliteDatabaseDriver(new DatabaseSettings { Filename = filename }, logging))
                {
                    await db.InitializeAsync();
                    Tenant a = db.Tenants.Create(new Tenant { Name = "A" });
                    Tenant b = db.Tenants.Create(new Tenant { Name = "B" });
                    db.Sessions.Create(new AuthSession { TenantGuid = a.Guid, UserGuid = "user-a" });
                    db.Sessions.Create(new AuthSession { TenantGuid = a.Guid, UserGuid = "user-a" });
                    db.Sessions.Create(new AuthSession { TenantGuid = b.Guid, UserGuid = "user-b" });
                    EnumerationResult<AuthSession> first = db.Sessions.Enumerate(a.Guid, new EnumerationQuery { PageSize = 1 });
                    EnumerationResult<AuthSession> second = db.Sessions.Enumerate(a.Guid, new EnumerationQuery { PageSize = 1, PageNumber = 2 });
                    Assert.Equal(2, first.TotalRecords);
                    Assert.False(first.EndOfResults);
                    Assert.True(second.EndOfResults);
                    Assert.Equal(a.Guid, Assert.Single(first.Objects).TenantGuid);
                    Assert.NotEqual(first.Objects[0].Guid, Assert.Single(second.Objects).Guid);
                    Permission rule = db.Permissions.Create(new Permission { TenantGuid = a.Guid, Name = "Rule" });
                    rule.Name = "Updated"; rule.Effect = PermissionEffectEnum.Deny; rule.Active = false;
                    db.Permissions.Update(rule);
                    Permission loaded = db.Permissions.Read(rule.Guid);
                    Assert.Equal(a.Guid, loaded.TenantGuid);
                    Assert.Equal("Updated", loaded.Name);
                    Assert.Equal(PermissionEffectEnum.Deny, loaded.Effect);
                    Assert.False(loaded.Active);
                    UserRole foreignRole = db.Roles.Create(new UserRole { TenantGuid = b.Guid, Name = "Foreign" });
                    Permission foreignPermission = db.Permissions.Create(new Permission { TenantGuid = b.Guid, Name = "Foreign permit", ResourceTypes = new System.Collections.Generic.List<string> { "All" }, OperationTypes = new System.Collections.Generic.List<string> { "All" } });
                    db.RolePermissionMaps.Create(new RolePermissionMap { TenantGuid = b.Guid, RoleGuid = foreignRole.Guid, PermissionGuid = foreignPermission.Guid });
                    db.UserRoleAssignments.Create(new UserRoleAssignment { TenantGuid = a.Guid, UserGuid = "user-a", RoleGuid = foreignRole.Guid });
                    RbacEngine rbac = new RbacEngine(db);
                    Assert.Empty(rbac.GetEffectivePermissions(PrincipalTypeEnum.User, "user-a", a.Guid));
                    UserRole localRole = db.Roles.Create(new UserRole { TenantGuid = a.Guid, Name = "Local" });
                    db.RolePermissionMaps.Create(new RolePermissionMap { TenantGuid = a.Guid, RoleGuid = localRole.Guid, PermissionGuid = foreignPermission.Guid });
                    db.UserRoleAssignments.Create(new UserRoleAssignment { TenantGuid = a.Guid, UserGuid = "user-a", RoleGuid = localRole.Guid });
                    Assert.Empty(rbac.GetEffectivePermissions(PrincipalTypeEnum.User, "user-a", a.Guid));
                }
            }
            finally
            {
                foreach (string suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(filename + suffix)) File.Delete(filename + suffix);
            }
        }
    }
}
