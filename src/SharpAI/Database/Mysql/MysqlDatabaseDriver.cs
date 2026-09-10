namespace SharpAI.Database.Mysql
{
    using System.Collections.Generic;
    using System.Data.Common;

    using MySqlConnector;

    using SyslogLogging;

    /// <summary>
    /// MySQL / MariaDB database driver. Provides MySQL dialect DDL and paging.
    /// </summary>
    public class MysqlDatabaseDriver : DatabaseDriverBase
    {
        #region Public-Members

        /// <inheritdoc />
        public override DatabaseTypeEnum DatabaseType
        {
            get { return DatabaseTypeEnum.Mysql; }
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the MySQL driver.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <param name="logging">Logging module.</param>
        public MysqlDatabaseDriver(DatabaseSettings settings, LoggingModule logging)
            : base(settings, logging)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override string BuildPaging(int limit, int offset)
        {
            return "LIMIT " + limit + " OFFSET " + offset;
        }

        #endregion

        #region Protected-Methods

        /// <inheritdoc />
        protected override DbConnection CreateConnection()
        {
            int port = Settings.Port > 0 ? Settings.Port : 3306;
            string connectionString =
                "Server=" + Settings.Hostname + ";Port=" + port + ";Database=" + Settings.DatabaseName +
                ";Uid=" + Settings.Username + ";Pwd=" + Settings.Password;
            return new MySqlConnection(connectionString);
        }

        /// <inheritdoc />
        protected override string SchemaMigrationsTableDdl()
        {
            return
                "CREATE TABLE IF NOT EXISTS schema_migrations (" +
                "version INT PRIMARY KEY, description VARCHAR(512), appliedutc VARCHAR(64))";
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
                        "CREATE TABLE IF NOT EXISTS model_files (" +
                        "guid VARCHAR(64) PRIMARY KEY, " +
                        "modelname VARCHAR(256) NOT NULL, " +
                        "parentmodelname VARCHAR(256), " +
                        "modelformat VARCHAR(32), " +
                        "modelfamily VARCHAR(128), " +
                        "contentlength BIGINT NOT NULL, " +
                        "parametercount BIGINT NOT NULL, " +
                        "md5 VARCHAR(64), " +
                        "sha1 VARCHAR(64), " +
                        "sha256 VARCHAR(128), " +
                        "sourceurl VARCHAR(1024), " +
                        "parametersize VARCHAR(32), " +
                        "quantization VARCHAR(32), " +
                        "embeddings INT NOT NULL DEFAULT 0, " +
                        "completions INT NOT NULL DEFAULT 0, " +
                        "modelcreationutc VARCHAR(64), " +
                        "createdutc VARCHAR(64) NOT NULL)",
                        "CREATE INDEX idx_model_files_modelname ON model_files (modelname)"
                    }),
                    new SchemaMigration(2, "Request history schema", new List<string>
                    {
                        "CREATE TABLE IF NOT EXISTS request_history (" +
                        "id VARCHAR(64) PRIMARY KEY, tenantid VARCHAR(64), userid VARCHAR(64), principalname VARCHAR(256), " +
                        "method VARCHAR(16), path VARCHAR(1024), url VARCHAR(2048), statuscode INT, durationms DOUBLE, sourceip VARCHAR(64), " +
                        "requestheadersjson LONGTEXT, requestbody LONGTEXT, requestbodybytes BIGINT, requestbodytruncated INT, " +
                        "responseheadersjson LONGTEXT, responsebody LONGTEXT, responsebodybytes BIGINT, responsebodytruncated INT, " +
                        "createdutc VARCHAR(64) NOT NULL, completedutc VARCHAR(64))",
                        "CREATE INDEX idx_request_history_createdutc ON request_history (createdutc)"
                    }),
                    new SchemaMigration(3, "Authentication schema", new List<string>
                    {
                        "CREATE TABLE IF NOT EXISTS tenants (guid VARCHAR(64) PRIMARY KEY, name VARCHAR(256), active INT NOT NULL DEFAULT 1, isprotected INT NOT NULL DEFAULT 0, createdutc VARCHAR(64) NOT NULL, lastupdateutc VARCHAR(64) NOT NULL)",
                        "CREATE TABLE IF NOT EXISTS users (guid VARCHAR(64) PRIMARY KEY, tenantguid VARCHAR(64), firstname VARCHAR(128), lastname VARCHAR(128), email VARCHAR(256), passwordsha256 VARCHAR(128), isadmin INT NOT NULL DEFAULT 0, istenantadmin INT NOT NULL DEFAULT 0, active INT NOT NULL DEFAULT 1, isprotected INT NOT NULL DEFAULT 0, createdutc VARCHAR(64) NOT NULL, lastupdateutc VARCHAR(64) NOT NULL)",
                        "CREATE INDEX idx_users_tenant_email ON users (tenantguid, email)",
                        "CREATE TABLE IF NOT EXISTS credentials (guid VARCHAR(64) PRIMARY KEY, userguid VARCHAR(64), tenantguid VARCHAR(64), name VARCHAR(256), accesskey VARCHAR(128), secretsha256 VARCHAR(128), active INT NOT NULL DEFAULT 1, isprotected INT NOT NULL DEFAULT 0, createdutc VARCHAR(64) NOT NULL, lastupdateutc VARCHAR(64) NOT NULL, lastusedutc VARCHAR(64), expiresutc VARCHAR(64))",
                        "CREATE INDEX idx_credentials_accesskey ON credentials (accesskey)",
                        "CREATE TABLE IF NOT EXISTS authsessions (guid VARCHAR(64) PRIMARY KEY, userguid VARCHAR(64), tenantguid VARCHAR(64), principaltype VARCHAR(32), active INT NOT NULL DEFAULT 1, isprotected INT NOT NULL DEFAULT 0, createdutc VARCHAR(64) NOT NULL, expiresutc VARCHAR(64) NOT NULL, lastusedutc VARCHAR(64), revokedutc VARCHAR(64), revocationreason VARCHAR(512))",
                        "CREATE TABLE IF NOT EXISTS audit_log (guid VARCHAR(64) PRIMARY KEY, tenantguid VARCHAR(64), eventtype VARCHAR(128), principaltype VARCHAR(32), principalguid VARCHAR(64), method VARCHAR(16), path VARCHAR(1024), ipaddress VARCHAR(64), authresult INT, authzresult INT, denialreason VARCHAR(512), statuscode INT, createdutc VARCHAR(64) NOT NULL)",
                        "CREATE INDEX idx_audit_createdutc ON audit_log (createdutc)"
                    }),
                    new SchemaMigration(4, "RBAC schema", new List<string>
                    {
                        "CREATE TABLE IF NOT EXISTS userroles (guid VARCHAR(64) PRIMARY KEY, tenantguid VARCHAR(64), name VARCHAR(256), isbuiltin INT NOT NULL DEFAULT 0, active INT NOT NULL DEFAULT 1, isprotected INT NOT NULL DEFAULT 0, createdutc VARCHAR(64) NOT NULL, lastupdateutc VARCHAR(64) NOT NULL)",
                        "CREATE INDEX idx_userroles_name ON userroles (name)",
                        "CREATE TABLE IF NOT EXISTS permissions (guid VARCHAR(64) PRIMARY KEY, tenantguid VARCHAR(64), name VARCHAR(256), resourcetypes TEXT, operationtypes TEXT, permissiontype VARCHAR(16), active INT NOT NULL DEFAULT 1, isprotected INT NOT NULL DEFAULT 0, createdutc VARCHAR(64) NOT NULL, lastupdateutc VARCHAR(64) NOT NULL)",
                        "CREATE TABLE IF NOT EXISTS rolepermissionmaps (guid VARCHAR(64) PRIMARY KEY, tenantguid VARCHAR(64), roleguid VARCHAR(64), permissionguid VARCHAR(64), active INT NOT NULL DEFAULT 1, isprotected INT NOT NULL DEFAULT 0, createdutc VARCHAR(64) NOT NULL, lastupdateutc VARCHAR(64) NOT NULL)",
                        "CREATE INDEX idx_rpm_roleguid ON rolepermissionmaps (roleguid)",
                        "CREATE TABLE IF NOT EXISTS userroleassignments (guid VARCHAR(64) PRIMARY KEY, tenantguid VARCHAR(64), userguid VARCHAR(64), roleguid VARCHAR(64), rolename VARCHAR(256), resourcescope VARCHAR(16), resourceguid VARCHAR(64), inheritstochildren INT NOT NULL DEFAULT 1, active INT NOT NULL DEFAULT 1, isprotected INT NOT NULL DEFAULT 0, createdutc VARCHAR(64) NOT NULL, lastupdateutc VARCHAR(64) NOT NULL)",
                        "CREATE INDEX idx_ura_tenant_user ON userroleassignments (tenantguid, userguid)",
                        "CREATE TABLE IF NOT EXISTS credentialscopeassignments (guid VARCHAR(64) PRIMARY KEY, tenantguid VARCHAR(64), credentialguid VARCHAR(64), roleguid VARCHAR(64), rolename VARCHAR(256), resourcescope VARCHAR(16), resourceguid VARCHAR(64), inheritstochildren INT NOT NULL DEFAULT 1, permissions TEXT, resourcetypes TEXT, active INT NOT NULL DEFAULT 1, isprotected INT NOT NULL DEFAULT 0, createdutc VARCHAR(64) NOT NULL, lastupdateutc VARCHAR(64) NOT NULL)",
                        "CREATE INDEX idx_csa_tenant_cred ON credentialscopeassignments (tenantguid, credentialguid)"
                    }),

                    new SchemaMigration(5, "Model presets schema", new List<string>
                    {
                        "CREATE TABLE IF NOT EXISTS model_presets (guid VARCHAR(64) PRIMARY KEY, name VARCHAR(256), modelname VARCHAR(256), systemprompt TEXT, temperature VARCHAR(32), maxtokens INT, topp VARCHAR(32), templateoverride TEXT, stopjson TEXT, createdutc VARCHAR(64) NOT NULL, lastupdateutc VARCHAR(64) NOT NULL)",
                        "CREATE INDEX idx_model_presets_name ON model_presets (name)"
                    })
                };
            }
        }

        #endregion
    }
}
