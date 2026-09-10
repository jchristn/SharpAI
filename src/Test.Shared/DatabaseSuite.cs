namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using SharpAI.Database;
    using SharpAI.Database.Sqlite;
    using SharpAI.Models;
    using SharpAI.Security;

    using SyslogLogging;

    using Touchstone.Core;

    /// <summary>
    /// Contract suite for the handwritten-SQL database layer, exercised against SQLite (embedded, so it
    /// runs in CI without any external server). The same <see cref="SharpAI.Database.Interfaces.IModelRegistryMethods"/>
    /// contract is honored by the MySQL, PostgreSQL, and SQL Server drivers, which run under the provider
    /// matrix when those servers are available.
    /// </summary>
    public static class DatabaseSuite
    {
        #region Private-Members

        private static readonly object _Lock = new object();
        private static SqliteDatabaseDriver _Driver = null!;
        private static string _DbPath = null!;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build the database contract suite.
        /// </summary>
        /// <returns>Database suite.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor("Database", "Migrations_Idempotent", "Initialize is idempotent (schema_migrations tracked)",
                    async ct => { Db(); await _Driver.InitializeAsync(ct).ConfigureAwait(false); return; }),

                new TestCaseDescriptor("Database", "Add_Get", "Add then get by GUID and name",
                    ct =>
                    {
                        ModelFile mf = Sample("model-add");
                        Db().Models.Add(mf);
                        ModelFile byGuid = Db().Models.GetByGuid(mf.GUID);
                        ModelFile byName = Db().Models.GetByName(mf.Name);
                        TestAssert.True(byGuid != null && byGuid.Name == mf.Name, "GetByGuid should return the added model");
                        TestAssert.True(byName != null && byName.GUID == mf.GUID, "GetByName should return the added model");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Database", "Add_Duplicate_Name", "Adding a duplicate name returns the existing record",
                    ct =>
                    {
                        ModelFile a = Sample("dup");
                        Db().Models.Add(a);
                        ModelFile b = Sample("dup");
                        b.Name = a.Name; // force the same name to exercise the duplicate path
                        ModelFile stored = Db().Models.Add(b);
                        TestAssert.True(stored.GUID == a.GUID, "duplicate add should return the first record");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Database", "Exists_And_All", "ExistsByGuid and All reflect inserts",
                    ct =>
                    {
                        ModelFile mf = Sample("model-exists");
                        Db().Models.Add(mf);
                        TestAssert.True(Db().Models.ExistsByGuid(mf.GUID), "ExistsByGuid should be true");
                        List<ModelFile> listed = Db().Models.Enumerate(new EnumerationQuery { PageSize = 1000 }).Objects;
                        bool found = false;
                        foreach (ModelFile m in listed) { if (m.GUID == mf.GUID) { found = true; break; } }
                        TestAssert.True(found, "Enumerate should include the inserted model");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Database", "Update_RoundTrips", "Update persists changed fields",
                    ct =>
                    {
                        ModelFile mf = Sample("model-update");
                        Db().Models.Add(mf);
                        mf.Quantization = "Q5_K_M";
                        mf.Embeddings = true;
                        Db().Models.Update(mf);
                        ModelFile reread = Db().Models.GetByGuid(mf.GUID);
                        TestAssert.Equal("Q5_K_M", reread.Quantization);
                        TestAssert.True(reread.Embeddings, "updated Embeddings flag should persist");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Database", "GetMany", "GetMany returns the requested records",
                    ct =>
                    {
                        ModelFile a = Sample("many-a");
                        ModelFile b = Sample("many-b");
                        Db().Models.Add(a);
                        Db().Models.Add(b);
                        List<ModelFile> many = Db().Models.GetMany(new List<Guid> { a.GUID, b.GUID });
                        TestAssert.Equal(2, many.Count);
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Database", "Enumerate_Paging", "Enumerate returns a page with totals",
                    ct =>
                    {
                        Db().Models.Add(Sample("enum-1"));
                        Db().Models.Add(Sample("enum-2"));
                        EnumerationResult<ModelFile> page = Db().Models.Enumerate(new EnumerationQuery { PageNumber = 1, PageSize = 1, Order = EnumerationOrderEnum.NameAscending });
                        TestAssert.Equal(1, page.Objects.Count);
                        TestAssert.True(page.TotalRecords >= 2, "total should count all records");
                        TestAssert.True(page.RecordsRemaining >= 1, "records remaining should reflect more pages");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Database", "Enumerate_Filter", "Enumerate honors an equality filter",
                    ct =>
                    {
                        ModelFile mf = Sample("filter-model");
                        Db().Models.Add(mf);
                        EnumerationResult<ModelFile> page = Db().Models.Enumerate(new EnumerationQuery { Name = mf.Name });
                        TestAssert.Equal(1, page.Objects.Count);
                        TestAssert.True(page.Objects[0].GUID == mf.GUID, "filtered enumerate should return the matching model");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Database", "Delete", "Delete removes the record and re-delete throws",
                    ct =>
                    {
                        ModelFile mf = Sample("model-delete");
                        Db().Models.Add(mf);
                        Db().Models.Delete(mf.GUID);
                        TestAssert.True(!Db().Models.ExistsByGuid(mf.GUID), "record should be gone after delete");

                        bool threw = false;
                        try { Db().Models.Delete(mf.GUID); }
                        catch (KeyNotFoundException) { threw = true; }
                        TestAssert.True(threw, "deleting a missing record should throw KeyNotFoundException");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Database", "RH_Create_Read", "Request history create then read returns bodies",
                    ct =>
                    {
                        RequestHistoryEntry e = SampleEntry("GET", 200);
                        e.RequestBody = "req-body";
                        e.ResponseBody = "resp-body";
                        Db().RequestHistory.Create(e);
                        RequestHistoryEntry read = Db().RequestHistory.Read(e.Id);
                        TestAssert.True(read != null && read.Method == "GET", "read should return the entry");
                        TestAssert.Equal("resp-body", read.ResponseBody);
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Database", "RH_Enumerate_OmitsBodies", "Request history enumerate omits bodies",
                    ct =>
                    {
                        RequestHistoryEntry e = SampleEntry("POST", 201);
                        e.ResponseBody = "should-not-appear";
                        Db().RequestHistory.Create(e);
                        EnumerationResult<RequestHistoryEntry> page = Db().RequestHistory.Enumerate(new RequestHistoryQuery { Method = "POST" });
                        TestAssert.True(page.TotalRecords >= 1, "enumerate should count the entry");
                        bool anyBody = false;
                        foreach (RequestHistoryEntry x in page.Objects) { if (x.ResponseBody != null) anyBody = true; }
                        TestAssert.True(!anyBody, "list results must not include bodies");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Database", "RH_Summarize", "Request history summary buckets the range",
                    ct =>
                    {
                        DateTime now = DateTime.UtcNow;
                        Db().RequestHistory.Create(SampleEntry("GET", 200));
                        Db().RequestHistory.Create(SampleEntry("GET", 500));
                        RequestHistorySummary summary = Db().RequestHistory.Summarize(new RequestHistoryQuery
                        {
                            FromUtc = now.AddMinutes(-30),
                            ToUtc = now.AddMinutes(1),
                            BucketMinutes = 15
                        });
                        TestAssert.True(summary.TotalCount >= 2, "summary should count entries in range");
                        TestAssert.True(summary.Buckets.Count >= 1, "summary should emit buckets");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Database", "RH_Delete_Prune", "Request history delete and prune remove rows",
                    ct =>
                    {
                        RequestHistoryEntry e = SampleEntry("DELETE", 204);
                        Db().RequestHistory.Create(e);
                        TestAssert.True(Db().RequestHistory.Delete(e.Id), "delete should return true");
                        TestAssert.True(Db().RequestHistory.Read(e.Id) == null, "entry should be gone after delete");

                        RequestHistoryEntry old = SampleEntry("GET", 200);
                        old.CreatedUtc = DateTime.UtcNow.AddDays(-40);
                        Db().RequestHistory.Create(old);
                        int pruned = Db().RequestHistory.Prune(DateTime.UtcNow.AddDays(-30));
                        TestAssert.True(pruned >= 1, "prune should remove the old entry");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Database", "Auth_Tenant_User", "Tenant + user create/lookup, password verify",
                    ct =>
                    {
                        Tenant tenant = new Tenant { Name = "tenant-" + Guid.NewGuid().ToString("N") };
                        Db().Tenants.Create(tenant);
                        TestAssert.True(Db().Tenants.Read(tenant.Guid) != null, "tenant should be readable");

                        string email = Guid.NewGuid().ToString("N") + "@example.com";
                        User user = new User { TenantGuid = tenant.Guid, Email = email, PasswordSha256 = PasswordHasher.Hash("hunter2"), IsAdmin = true };
                        Db().Users.Create(user);

                        User byEmail = Db().Users.GetByEmail(tenant.Guid, email);
                        TestAssert.True(byEmail != null && byEmail.IsAdmin, "user should be readable by email and be admin");
                        TestAssert.True(PasswordHasher.Verify("hunter2", byEmail.PasswordSha256), "password should verify");
                        TestAssert.True(!PasswordHasher.Verify("wrong", byEmail.PasswordSha256), "wrong password should not verify");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Database", "Auth_Credential", "Credential create + lookup by access key",
                    ct =>
                    {
                        string accessKey = CredentialKeyGenerator.GenerateAccessKey();
                        Credential cred = new Credential { TenantGuid = "ten_x", UserGuid = "usr_x", Name = "ci", AccessKey = accessKey, SecretSha256 = PasswordHasher.Hash("secret_val") };
                        Db().Credentials.Create(cred);
                        Credential found = Db().Credentials.GetByAccessKey(accessKey);
                        TestAssert.True(found != null && found.Guid == cred.Guid, "credential should be found by access key");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Database", "Auth_Session_Token", "Session create/read/revoke + token round-trip",
                    ct =>
                    {
                        AuthSession session = new AuthSession { UserGuid = "usr_x", TenantGuid = "ten_x", ExpiresUtc = DateTime.UtcNow.AddMinutes(30) };
                        Db().Sessions.Create(session);

                        SessionTokenService tokens = new SessionTokenService("test-signing-key");
                        string token = tokens.Encrypt(session.Guid);
                        TestAssert.Equal(session.Guid, tokens.Decrypt(token));
                        TestAssert.True(tokens.Decrypt("not-a-token") == null, "garbage token decrypts to null");

                        AuthSession read = Db().Sessions.Read(session.Guid);
                        TestAssert.True(read != null && read.Active, "session should be active");
                        Db().Sessions.Revoke(session.Guid, "test");
                        TestAssert.True(!Db().Sessions.Read(session.Guid).Active, "session should be inactive after revoke");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Database", "Auth_Audit", "Audit entry create + tenant-scoped enumerate",
                    ct =>
                    {
                        string tenantGuid = "ten_" + Guid.NewGuid().ToString("N");
                        AuditLogEntry entry = new AuditLogEntry { TenantGuid = tenantGuid, EventType = "AuthorizationDenied", DenialReason = "no permission", StatusCode = 403 };
                        Db().Audit.Create(entry);
                        EnumerationResult<AuditLogEntry> page = Db().Audit.Enumerate(tenantGuid, new EnumerationQuery());
                        TestAssert.True(page.TotalRecords >= 1, "audit enumerate should return the entry");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Database", "Rbac_DataLayer", "Roles/permissions/assignments CRUD, join, and enumerate",
                    ct =>
                    {
                        string tenant = "ten_" + Guid.NewGuid().ToString("N");
                        UserRole role = Db().Roles.Create(new UserRole { TenantGuid = tenant, Name = "custom-" + Guid.NewGuid().ToString("N") });
                        Permission perm = Db().Permissions.Create(new Permission
                        {
                            TenantGuid = tenant,
                            Name = "p",
                            ResourceTypes = new List<string> { "Model" },
                            OperationTypes = new List<string> { "Read" },
                            Effect = PermissionEffectEnum.Permit
                        });
                        Db().RolePermissionMaps.Create(new RolePermissionMap { TenantGuid = tenant, RoleGuid = role.Guid, PermissionGuid = perm.Guid });

                        List<Permission> forRole = Db().Permissions.GetForRole(role.Guid);
                        TestAssert.True(forRole.Count == 1 && forRole[0].ResourceTypes.Contains("Model"), "GetForRole should join the mapped permission and round-trip the JSON array");

                        string user = "usr_" + Guid.NewGuid().ToString("N");
                        UserRoleAssignment assignment = Db().UserRoleAssignments.Create(new UserRoleAssignment { TenantGuid = tenant, UserGuid = user, RoleGuid = role.Guid });
                        TestAssert.True(Db().UserRoleAssignments.GetForUser(tenant, user).Count == 1, "GetForUser should return the assignment");

                        EnumerationResult<UserRole> roles = Db().Roles.Enumerate(tenant, new EnumerationQuery());
                        EnumerationResult<Permission> perms = Db().Permissions.Enumerate(tenant, new EnumerationQuery());
                        EnumerationResult<UserRoleAssignment> asns = Db().UserRoleAssignments.Enumerate(tenant, user, new EnumerationQuery());
                        TestAssert.True(roles.TotalRecords >= 1 && perms.TotalRecords >= 1 && asns.TotalRecords == 1, "enumerate should report the created records");

                        Db().UserRoleAssignments.Delete(assignment.Guid);
                        TestAssert.True(Db().UserRoleAssignments.GetForUser(tenant, user).Count == 0, "assignment should be gone after delete");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Database", "RequestHistory_Filters", "Enumerate honors method/status/path filters",
                    ct =>
                    {
                        string tag = "/rhf-" + Guid.NewGuid().ToString("N");
                        Db().RequestHistory.Create(new RequestHistoryEntry { Method = "GET", Path = tag + "/a", StatusCode = 200, CreatedUtc = DateTime.UtcNow });
                        Db().RequestHistory.Create(new RequestHistoryEntry { Method = "POST", Path = tag + "/b", StatusCode = 500, CreatedUtc = DateTime.UtcNow });
                        Db().RequestHistory.Create(new RequestHistoryEntry { Method = "GET", Path = tag + "/a2", StatusCode = 404, CreatedUtc = DateTime.UtcNow });

                        EnumerationResult<RequestHistoryEntry> all = Db().RequestHistory.Enumerate(new RequestHistoryQuery { PathContains = tag, PageSize = 100 });
                        TestAssert.True(all.TotalRecords == 3, "path filter returns the three tagged entries");

                        EnumerationResult<RequestHistoryEntry> gets = Db().RequestHistory.Enumerate(new RequestHistoryQuery { PathContains = tag, Method = "GET", PageSize = 100 });
                        TestAssert.True(gets.TotalRecords == 2, "method filter narrows to GET");

                        EnumerationResult<RequestHistoryEntry> errs = Db().RequestHistory.Enumerate(new RequestHistoryQuery { PathContains = tag, StatusCode = 500, PageSize = 100 });
                        TestAssert.True(errs.TotalRecords == 1, "status filter narrows to 500");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Database", "RequestHistory_Category_Inference", "category=inference restricts to inference/embeddings endpoints",
                    ct =>
                    {
                        Db().RequestHistory.Create(new RequestHistoryEntry { Method = "POST", Path = "/api/chat", StatusCode = 200, CreatedUtc = DateTime.UtcNow });
                        Db().RequestHistory.Create(new RequestHistoryEntry { Method = "POST", Path = "/v1/chat/completions", StatusCode = 200, CreatedUtc = DateTime.UtcNow });
                        Db().RequestHistory.Create(new RequestHistoryEntry { Method = "POST", Path = "/api/embed", StatusCode = 200, CreatedUtc = DateTime.UtcNow });
                        Db().RequestHistory.Create(new RequestHistoryEntry { Method = "GET", Path = "/api/tags", StatusCode = 200, CreatedUtc = DateTime.UtcNow });
                        Db().RequestHistory.Create(new RequestHistoryEntry { Method = "GET", Path = "/api/version", StatusCode = 200, CreatedUtc = DateTime.UtcNow });

                        EnumerationResult<RequestHistoryEntry> inf = Db().RequestHistory.Enumerate(new RequestHistoryQuery { Category = "inference", PageSize = 1000 });
                        foreach (RequestHistoryEntry e in inf.Objects)
                        {
                            bool isInference = e.Path.Contains("/chat") || e.Path.Contains("/generate") || e.Path.Contains("/embed") || e.Path.Contains("/completions");
                            TestAssert.True(isInference, "non-inference path leaked into category=inference: " + e.Path);
                        }
                        TestAssert.True(inf.TotalRecords >= 3, "the three inference entries are included");

                        // The summary honors the category too.
                        RequestHistorySummary summary = Db().RequestHistory.Summarize(new RequestHistoryQuery { Category = "inference", BucketMinutes = 60 });
                        TestAssert.True(summary.Buckets != null && summary.Buckets.Count >= 1, "summary returns buckets for the inference category");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Database", "Model_Enumerate_Filters", "Enumerate honors family + quantization filters",
                    ct =>
                    {
                        string family = "fam-" + Guid.NewGuid().ToString("N");
                        ModelFile a = Sample("filter-a"); a.Family = family; a.Quantization = "Q4_K_M";
                        ModelFile b = Sample("filter-b"); b.Family = family; b.Quantization = "Q8_0";
                        ModelFile c = Sample("filter-c"); c.Family = "other-" + Guid.NewGuid().ToString("N"); c.Quantization = "Q4_K_M";
                        Db().Models.Add(a);
                        Db().Models.Add(b);
                        Db().Models.Add(c);

                        EnumerationResult<ModelFile> byFamily = Db().Models.Enumerate(new EnumerationQuery { Family = family, PageSize = 1000 });
                        TestAssert.True(byFamily.TotalRecords == 2, "family filter returns the two in that family");

                        EnumerationResult<ModelFile> byFamilyQuant = Db().Models.Enumerate(new EnumerationQuery { Family = family, Quantization = "Q8_0", PageSize = 1000 });
                        TestAssert.True(byFamilyQuant.TotalRecords == 1, "family + quantization narrows to one");

                        EnumerationResult<ModelFile> byName = Db().Models.Enumerate(new EnumerationQuery { Name = a.Name, PageSize = 1000 });
                        TestAssert.True(byName.TotalRecords == 1 && byName.Objects[0].GUID == a.GUID, "exact-name filter returns the one model");
                        return Task.CompletedTask;
                    })
            };

            return new TestSuiteDescriptor("Database", "Database layer (SQLite contract)", cases, BeforeSuiteAsync, AfterSuiteAsync);
        }

        #endregion

        #region Private-Methods

        private static ValueTask BeforeSuiteAsync(CancellationToken token)
        {
            Db();
            return new ValueTask();
        }

        private static SqliteDatabaseDriver Db()
        {
            lock (_Lock)
            {
                if (_Driver == null)
                {
                    _DbPath = Path.Combine(Path.GetTempPath(), "sharpai-test-" + Guid.NewGuid().ToString("N") + ".db");
                    _Driver = new SqliteDatabaseDriver(new DatabaseSettings(_DbPath), new LoggingModule());
                    _Driver.InitializeAsync().GetAwaiter().GetResult();
                }
                return _Driver;
            }
        }

        private static ValueTask AfterSuiteAsync(CancellationToken token)
        {
            // Intentionally do not dispose the shared driver here. The same suite descriptor is consumed by
            // the console, fact, and per-case (theory / TestCaseSource) runners in a single process; disposing
            // the driver in one runner's teardown would break another runner's cases that share the static
            // instance. The lazily-created temporary SQLite file is left for the OS to reclaim.
            return new ValueTask();
        }

        private static RequestHistoryEntry SampleEntry(string method, int statusCode)
        {
            RequestHistoryEntry entry = new RequestHistoryEntry
            {
                Method = method,
                Path = "/api/test",
                Url = "http://127.0.0.1/api/test",
                StatusCode = statusCode,
                DurationMs = 12.5,
                SourceIp = "127.0.0.1",
                CreatedUtc = DateTime.UtcNow
            };
            entry.RequestHeaders["Content-Type"] = "application/json";
            entry.ResponseHeaders["Content-Type"] = "application/json";
            return entry;
        }

        private static ModelFile Sample(string name)
        {
            return new ModelFile
            {
                GUID = Guid.NewGuid(),
                Name = name + "-" + Guid.NewGuid().ToString("N"),
                Family = "llama",
                Format = "gguf",
                ContentLength = 123456,
                ParameterCount = 7000000,
                MD5Hash = "abc",
                SHA256Hash = "def",
                Quantization = "Q4_K_M",
                Embeddings = false,
                Completions = true,
                CreatedUtc = DateTime.UtcNow
            };
        }

        #endregion
    }
}
