namespace SharpAI.Database.SqlServer
{
    using System.Collections.Generic;
    using System.Data.Common;

    using Microsoft.Data.SqlClient;

    using SyslogLogging;

    /// <summary>
    /// Microsoft SQL Server database driver. Provides SQL Server dialect DDL (existence-guarded, since it
    /// has no CREATE TABLE IF NOT EXISTS) and OFFSET/FETCH paging.
    /// </summary>
    public class SqlServerDatabaseDriver : DatabaseDriverBase
    {
        #region Public-Members

        /// <inheritdoc />
        public override DatabaseTypeEnum DatabaseType
        {
            get { return DatabaseTypeEnum.SqlServer; }
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the SQL Server driver.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <param name="logging">Logging module.</param>
        public SqlServerDatabaseDriver(DatabaseSettings settings, LoggingModule logging)
            : base(settings, logging)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override string BuildPaging(int limit, int offset)
        {
            return "OFFSET " + offset + " ROWS FETCH NEXT " + limit + " ROWS ONLY";
        }

        #endregion

        #region Protected-Methods

        /// <inheritdoc />
        protected override DbConnection CreateConnection()
        {
            int port = Settings.Port > 0 ? Settings.Port : 1433;
            string connectionString =
                "Server=" + Settings.Hostname + "," + port + ";Database=" + Settings.DatabaseName +
                ";User Id=" + Settings.Username + ";Password=" + Settings.Password + ";TrustServerCertificate=true";
            return new SqlConnection(connectionString);
        }

        /// <inheritdoc />
        protected override string SchemaMigrationsTableDdl()
        {
            return
                "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'schema_migrations') " +
                "CREATE TABLE schema_migrations (version INT PRIMARY KEY, description NVARCHAR(512), appliedutc NVARCHAR(64))";
        }

        /// <inheritdoc />
        protected override IReadOnlyList<SchemaMigration> Migrations
        {
            get
            {
                return new List<SchemaMigration>
                {
                    new SchemaMigration(1, "Initial model registry schema", new List<string>
                    {
                        "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'model_files') " +
                        "CREATE TABLE model_files (" +
                        "guid NVARCHAR(64) PRIMARY KEY, " +
                        "modelname NVARCHAR(256) NOT NULL, " +
                        "parentmodelname NVARCHAR(256), " +
                        "modelformat NVARCHAR(32), " +
                        "modelfamily NVARCHAR(128), " +
                        "contentlength BIGINT NOT NULL, " +
                        "parametercount BIGINT NOT NULL, " +
                        "md5 NVARCHAR(64), " +
                        "sha1 NVARCHAR(64), " +
                        "sha256 NVARCHAR(128), " +
                        "sourceurl NVARCHAR(1024), " +
                        "parametersize NVARCHAR(32), " +
                        "quantization NVARCHAR(32), " +
                        "embeddings INT NOT NULL DEFAULT 0, " +
                        "completions INT NOT NULL DEFAULT 0, " +
                        "modelcreationutc NVARCHAR(64), " +
                        "createdutc NVARCHAR(64) NOT NULL)",
                        "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_model_files_modelname') " +
                        "CREATE INDEX idx_model_files_modelname ON model_files (modelname)"
                    }),
                    new SchemaMigration(2, "Request history schema", new List<string>
                    {
                        "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'request_history') " +
                        "CREATE TABLE request_history (" +
                        "id NVARCHAR(64) PRIMARY KEY, tenantid NVARCHAR(64), userid NVARCHAR(64), principalname NVARCHAR(256), " +
                        "method NVARCHAR(16), path NVARCHAR(1024), url NVARCHAR(2048), statuscode INT, durationms FLOAT, sourceip NVARCHAR(64), " +
                        "requestheadersjson NVARCHAR(MAX), requestbody NVARCHAR(MAX), requestbodybytes BIGINT, requestbodytruncated INT, " +
                        "responseheadersjson NVARCHAR(MAX), responsebody NVARCHAR(MAX), responsebodybytes BIGINT, responsebodytruncated INT, " +
                        "createdutc NVARCHAR(64) NOT NULL, completedutc NVARCHAR(64))",
                        "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_request_history_createdutc') " +
                        "CREATE INDEX idx_request_history_createdutc ON request_history (createdutc)"
                    }),
                    new SchemaMigration(3, "Authentication schema", new List<string>
                    {
                        "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tenants') CREATE TABLE tenants (guid NVARCHAR(64) PRIMARY KEY, name NVARCHAR(256), active INT NOT NULL DEFAULT 1, isprotected INT NOT NULL DEFAULT 0, createdutc NVARCHAR(64) NOT NULL, lastupdateutc NVARCHAR(64) NOT NULL)",
                        "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'users') CREATE TABLE users (guid NVARCHAR(64) PRIMARY KEY, tenantguid NVARCHAR(64), firstname NVARCHAR(128), lastname NVARCHAR(128), email NVARCHAR(256), passwordsha256 NVARCHAR(128), isadmin INT NOT NULL DEFAULT 0, istenantadmin INT NOT NULL DEFAULT 0, active INT NOT NULL DEFAULT 1, isprotected INT NOT NULL DEFAULT 0, createdutc NVARCHAR(64) NOT NULL, lastupdateutc NVARCHAR(64) NOT NULL)",
                        "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_users_tenant_email') CREATE INDEX idx_users_tenant_email ON users (tenantguid, email)",
                        "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'credentials') CREATE TABLE credentials (guid NVARCHAR(64) PRIMARY KEY, userguid NVARCHAR(64), tenantguid NVARCHAR(64), name NVARCHAR(256), accesskey NVARCHAR(128), secretsha256 NVARCHAR(128), active INT NOT NULL DEFAULT 1, isprotected INT NOT NULL DEFAULT 0, createdutc NVARCHAR(64) NOT NULL, lastupdateutc NVARCHAR(64) NOT NULL, lastusedutc NVARCHAR(64), expiresutc NVARCHAR(64))",
                        "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_credentials_accesskey') CREATE INDEX idx_credentials_accesskey ON credentials (accesskey)",
                        "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'authsessions') CREATE TABLE authsessions (guid NVARCHAR(64) PRIMARY KEY, userguid NVARCHAR(64), tenantguid NVARCHAR(64), principaltype NVARCHAR(32), active INT NOT NULL DEFAULT 1, isprotected INT NOT NULL DEFAULT 0, createdutc NVARCHAR(64) NOT NULL, expiresutc NVARCHAR(64) NOT NULL, lastusedutc NVARCHAR(64), revokedutc NVARCHAR(64), revocationreason NVARCHAR(512))",
                        "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'audit_log') CREATE TABLE audit_log (guid NVARCHAR(64) PRIMARY KEY, tenantguid NVARCHAR(64), eventtype NVARCHAR(128), principaltype NVARCHAR(32), principalguid NVARCHAR(64), method NVARCHAR(16), path NVARCHAR(1024), ipaddress NVARCHAR(64), authresult INT, authzresult INT, denialreason NVARCHAR(512), statuscode INT, createdutc NVARCHAR(64) NOT NULL)",
                        "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_audit_createdutc') CREATE INDEX idx_audit_createdutc ON audit_log (createdutc)"
                    }),
                    new SchemaMigration(4, "RBAC schema", new List<string>
                    {
                        "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'userroles') CREATE TABLE userroles (guid NVARCHAR(64) PRIMARY KEY, tenantguid NVARCHAR(64) NULL, name NVARCHAR(256), isbuiltin INT NOT NULL DEFAULT 0, active INT NOT NULL DEFAULT 1, isprotected INT NOT NULL DEFAULT 0, createdutc NVARCHAR(64) NOT NULL, lastupdateutc NVARCHAR(64) NOT NULL)",
                        "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_userroles_name') CREATE INDEX idx_userroles_name ON userroles (name)",
                        "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'permissions') CREATE TABLE permissions (guid NVARCHAR(64) PRIMARY KEY, tenantguid NVARCHAR(64) NULL, name NVARCHAR(256), resourcetypes NVARCHAR(MAX), operationtypes NVARCHAR(MAX), permissiontype NVARCHAR(16), active INT NOT NULL DEFAULT 1, isprotected INT NOT NULL DEFAULT 0, createdutc NVARCHAR(64) NOT NULL, lastupdateutc NVARCHAR(64) NOT NULL)",
                        "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'rolepermissionmaps') CREATE TABLE rolepermissionmaps (guid NVARCHAR(64) PRIMARY KEY, tenantguid NVARCHAR(64) NULL, roleguid NVARCHAR(64), permissionguid NVARCHAR(64), active INT NOT NULL DEFAULT 1, isprotected INT NOT NULL DEFAULT 0, createdutc NVARCHAR(64) NOT NULL, lastupdateutc NVARCHAR(64) NOT NULL)",
                        "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_rpm_roleguid') CREATE INDEX idx_rpm_roleguid ON rolepermissionmaps (roleguid)",
                        "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'userroleassignments') CREATE TABLE userroleassignments (guid NVARCHAR(64) PRIMARY KEY, tenantguid NVARCHAR(64), userguid NVARCHAR(64), roleguid NVARCHAR(64) NULL, rolename NVARCHAR(256) NULL, resourcescope NVARCHAR(16), resourceguid NVARCHAR(64) NULL, inheritstochildren INT NOT NULL DEFAULT 1, active INT NOT NULL DEFAULT 1, isprotected INT NOT NULL DEFAULT 0, createdutc NVARCHAR(64) NOT NULL, lastupdateutc NVARCHAR(64) NOT NULL)",
                        "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_ura_tenant_user') CREATE INDEX idx_ura_tenant_user ON userroleassignments (tenantguid, userguid)",
                        "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'credentialscopeassignments') CREATE TABLE credentialscopeassignments (guid NVARCHAR(64) PRIMARY KEY, tenantguid NVARCHAR(64), credentialguid NVARCHAR(64), roleguid NVARCHAR(64) NULL, rolename NVARCHAR(256) NULL, resourcescope NVARCHAR(16), resourceguid NVARCHAR(64) NULL, inheritstochildren INT NOT NULL DEFAULT 1, permissions NVARCHAR(MAX), resourcetypes NVARCHAR(MAX), active INT NOT NULL DEFAULT 1, isprotected INT NOT NULL DEFAULT 0, createdutc NVARCHAR(64) NOT NULL, lastupdateutc NVARCHAR(64) NOT NULL)",
                        "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_csa_tenant_cred') CREATE INDEX idx_csa_tenant_cred ON credentialscopeassignments (tenantguid, credentialguid)"
                    }),

                    new SchemaMigration(5, "Model presets schema", new List<string>
                    {
                        "IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'model_presets') CREATE TABLE model_presets (guid NVARCHAR(64) PRIMARY KEY, name NVARCHAR(256), modelname NVARCHAR(256), systemprompt NVARCHAR(MAX), temperature NVARCHAR(32), maxtokens INT, topp NVARCHAR(32), templateoverride NVARCHAR(MAX), stopjson NVARCHAR(MAX), createdutc NVARCHAR(64) NOT NULL, lastupdateutc NVARCHAR(64) NOT NULL)",
                        "IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_model_presets_name') CREATE INDEX idx_model_presets_name ON model_presets (name)"
                    })
                };
            }
        }

        #endregion
    }
}
