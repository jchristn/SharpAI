namespace SharpAI.Database
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;

    using SharpAI.Database.Implementations;
    using SharpAI.Database.Interfaces;

    using SyslogLogging;

    /// <summary>
    /// Abstract base for provider-specific database drivers. Provides connection-agnostic execution
    /// helpers, a versioned/tracked migration runner, and the domain method interfaces. All access is
    /// serialized with a semaphore, which is required for SQLite and harmless (registry traffic is low)
    /// for server databases. Provider subclasses supply the connection and the dialect-specific DDL.
    /// </summary>
    public abstract class DatabaseDriverBase : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Database provider type.
        /// </summary>
        public abstract DatabaseTypeEnum DatabaseType { get; }

        /// <summary>
        /// Model registry data-access methods.
        /// </summary>
        public IModelRegistryMethods Models { get; }

        /// <summary>
        /// Model-preset (Modelfile-equivalent) data-access methods. Never null.
        /// </summary>
        public IModelPresetMethods Presets { get; }

        /// <summary>
        /// Request-history data-access methods.
        /// </summary>
        public IRequestHistoryMethods RequestHistory { get; }

        /// <summary>
        /// Tenant data-access methods.
        /// </summary>
        public ITenantMethods Tenants { get; }

        /// <summary>
        /// User data-access methods.
        /// </summary>
        public IUserMethods Users { get; }

        /// <summary>
        /// Credential data-access methods.
        /// </summary>
        public ICredentialMethods Credentials { get; }

        /// <summary>
        /// Authentication session data-access methods.
        /// </summary>
        public IAuthSessionMethods Sessions { get; }

        /// <summary>
        /// Security audit-log data-access methods.
        /// </summary>
        public IAuditMethods Audit { get; }

        /// <summary>
        /// RBAC role data-access methods.
        /// </summary>
        public IRoleMethods Roles { get; }

        /// <summary>
        /// RBAC permission data-access methods.
        /// </summary>
        public IPermissionMethods Permissions { get; }

        /// <summary>
        /// RBAC role↔permission mapping data-access methods.
        /// </summary>
        public IRolePermissionMapMethods RolePermissionMaps { get; }

        /// <summary>
        /// RBAC user role assignment data-access methods.
        /// </summary>
        public IUserRoleAssignmentMethods UserRoleAssignments { get; }

        /// <summary>
        /// RBAC credential scope assignment data-access methods.
        /// </summary>
        public ICredentialScopeAssignmentMethods CredentialScopeAssignments { get; }

        /// <summary>
        /// Whether the driver has been initialized (schema created and migrations applied).
        /// </summary>
        public bool IsInitialized
        {
            get { return _IsInitialized; }
        }

        #endregion

        #region Private-Members

        private readonly string _Header = "[Database] ";
        private readonly SemaphoreSlim _Lock = new SemaphoreSlim(1, 1);
        private bool _IsInitialized = false;
        private bool _Disposed = false;

        #endregion

        #region Protected-Members

        /// <summary>
        /// Database settings.
        /// </summary>
        protected DatabaseSettings Settings { get; }

        /// <summary>
        /// Logging module.
        /// </summary>
        protected LoggingModule Logging { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the base driver.
        /// </summary>
        /// <param name="settings">Database settings. May not be null.</param>
        /// <param name="logging">Logging module. May not be null.</param>
        protected DatabaseDriverBase(DatabaseSettings settings, LoggingModule logging)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            Models = new ModelRegistryMethods(this);
            Presets = new ModelPresetMethods(this);
            RequestHistory = new RequestHistoryMethods(this);
            Tenants = new TenantMethods(this);
            Users = new UserMethods(this);
            Credentials = new CredentialMethods(this);
            Sessions = new AuthSessionMethods(this);
            Audit = new AuditMethods(this);
            Roles = new RoleMethods(this);
            Permissions = new PermissionMethods(this);
            RolePermissionMaps = new RolePermissionMapMethods(this);
            UserRoleAssignments = new UserRoleAssignmentMethods(this);
            CredentialScopeAssignments = new CredentialScopeAssignmentMethods(this);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Initialize the schema: create the migrations tracking table and apply any pending migrations.
        /// Safe to call repeatedly.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task InitializeAsync(CancellationToken token = default)
        {
            await Task.Run(() => RunMigrations(), token).ConfigureAwait(false);
            _IsInitialized = true;
            Logging.Debug(_Header + DatabaseType + " initialized");
        }

        /// <summary>
        /// Provider-specific paging clause appended to a SELECT (for example "LIMIT n OFFSET m").
        /// </summary>
        /// <param name="limit">Maximum rows.</param>
        /// <param name="offset">Rows to skip.</param>
        /// <returns>Paging clause.</returns>
        public abstract string BuildPaging(int limit, int offset);

        /// <summary>
        /// Dispose the driver.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;

            try { _Lock?.Dispose(); } catch { }
        }

        #endregion

        #region Internal-Methods

        internal DataTable Query(string sql, Dictionary<string, object> parameters)
        {
            _Lock.Wait();
            try
            {
                using (DbConnection connection = CreateConnection())
                {
                    connection.Open();
                    using (DbCommand command = connection.CreateCommand())
                    {
                        command.CommandText = sql;
                        AddParameters(command, parameters);

                        DataTable table = new DataTable();
                        using (DbDataReader reader = command.ExecuteReader())
                        {
                            table.Load(reader);
                        }
                        return table;
                    }
                }
            }
            finally
            {
                _Lock.Release();
            }
        }

        internal int NonQuery(string sql, Dictionary<string, object> parameters)
        {
            _Lock.Wait();
            try
            {
                using (DbConnection connection = CreateConnection())
                {
                    connection.Open();
                    using (DbCommand command = connection.CreateCommand())
                    {
                        command.CommandText = sql;
                        AddParameters(command, parameters);
                        return command.ExecuteNonQuery();
                    }
                }
            }
            finally
            {
                _Lock.Release();
            }
        }

        internal object Scalar(string sql, Dictionary<string, object> parameters)
        {
            _Lock.Wait();
            try
            {
                using (DbConnection connection = CreateConnection())
                {
                    connection.Open();
                    using (DbCommand command = connection.CreateCommand())
                    {
                        command.CommandText = sql;
                        AddParameters(command, parameters);
                        return command.ExecuteScalar();
                    }
                }
            }
            finally
            {
                _Lock.Release();
            }
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Create a new, unopened provider connection.
        /// </summary>
        /// <returns>Connection.</returns>
        protected abstract DbConnection CreateConnection();

        /// <summary>
        /// Provider-specific DDL that idempotently creates the <c>schema_migrations</c> tracking table.
        /// </summary>
        /// <returns>DDL statement.</returns>
        protected abstract string SchemaMigrationsTableDdl();

        /// <summary>
        /// Ordered set of migrations for this provider (dialect-specific DDL).
        /// </summary>
        protected abstract IReadOnlyList<SchemaMigration> Migrations { get; }

        #endregion

        #region Private-Methods

        private void RunMigrations()
        {
            NonQuery(SchemaMigrationsTableDdl(), null);

            HashSet<int> applied = new HashSet<int>();
            DataTable existing = Query("SELECT version FROM schema_migrations", null);
            foreach (DataRow row in existing.Rows)
            {
                applied.Add(Convert.ToInt32(row["version"]));
            }

            foreach (SchemaMigration migration in Migrations)
            {
                if (applied.Contains(migration.Version)) continue;

                foreach (string statement in migration.Statements)
                {
                    NonQuery(statement, null);
                }

                Dictionary<string, object> parameters = new Dictionary<string, object>
                {
                    { "@version", migration.Version },
                    { "@description", migration.Description },
                    { "@appliedutc", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ") }
                };
                NonQuery("INSERT INTO schema_migrations (version, description, appliedutc) VALUES (@version, @description, @appliedutc)", parameters);

                Logging.Info(_Header + "applied migration v" + migration.Version + " (" + migration.Description + ")");
            }
        }

        private void AddParameters(DbCommand command, Dictionary<string, object> parameters)
        {
            if (parameters == null) return;

            foreach (KeyValuePair<string, object> parameter in parameters)
            {
                DbParameter dbParameter = command.CreateParameter();
                dbParameter.ParameterName = parameter.Key;
                dbParameter.Value = parameter.Value ?? DBNull.Value;
                command.Parameters.Add(dbParameter);
            }
        }

        #endregion
    }
}
