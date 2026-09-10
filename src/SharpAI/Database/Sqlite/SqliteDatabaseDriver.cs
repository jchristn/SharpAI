namespace SharpAI.Database.Sqlite
{
    using System.Collections.Generic;
    using System.Data.Common;

    using Microsoft.Data.Sqlite;

    using SyslogLogging;

    /// <summary>
    /// SQLite database driver (file-based, the local default). Provides SQLite dialect DDL and paging.
    /// </summary>
    public class SqliteDatabaseDriver : DatabaseDriverBase
    {
        #region Public-Members

        /// <inheritdoc />
        public override DatabaseTypeEnum DatabaseType
        {
            get { return DatabaseTypeEnum.Sqlite; }
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the SQLite driver.
        /// </summary>
        /// <param name="settings">Database settings (Filename is used).</param>
        /// <param name="logging">Logging module.</param>
        public SqliteDatabaseDriver(DatabaseSettings settings, LoggingModule logging)
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
            return new SqliteConnection("Data Source=" + Settings.Filename);
        }

        /// <inheritdoc />
        protected override string SchemaMigrationsTableDdl()
        {
            return
                "CREATE TABLE IF NOT EXISTS schema_migrations (" +
                "version INTEGER PRIMARY KEY, description TEXT, appliedutc TEXT)";
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
                        "guid TEXT PRIMARY KEY, " +
                        "modelname TEXT NOT NULL, " +
                        "parentmodelname TEXT, " +
                        "modelformat TEXT, " +
                        "modelfamily TEXT, " +
                        "contentlength INTEGER NOT NULL, " +
                        "parametercount INTEGER NOT NULL, " +
                        "md5 TEXT, " +
                        "sha1 TEXT, " +
                        "sha256 TEXT, " +
                        "sourceurl TEXT, " +
                        "parametersize TEXT, " +
                        "quantization TEXT, " +
                        "embeddings INTEGER NOT NULL DEFAULT 0, " +
                        "completions INTEGER NOT NULL DEFAULT 0, " +
                        "modelcreationutc TEXT, " +
                        "createdutc TEXT NOT NULL)",
                        "CREATE INDEX IF NOT EXISTS idx_model_files_modelname ON model_files (modelname)"
                    }),
                    new SchemaMigration(2, "Request history schema", new List<string>
                    {
                        "CREATE TABLE IF NOT EXISTS request_history (" +
                        "id TEXT PRIMARY KEY, tenantid TEXT, userid TEXT, principalname TEXT, " +
                        "method TEXT, path TEXT, url TEXT, statuscode INTEGER, durationms REAL, sourceip TEXT, " +
                        "requestheadersjson TEXT, requestbody TEXT, requestbodybytes INTEGER, requestbodytruncated INTEGER, " +
                        "responseheadersjson TEXT, responsebody TEXT, responsebodybytes INTEGER, responsebodytruncated INTEGER, " +
                        "createdutc TEXT NOT NULL, completedutc TEXT)",
                        "CREATE INDEX IF NOT EXISTS idx_request_history_createdutc ON request_history (createdutc)"
                    }),
                    new SchemaMigration(3, "Authentication schema", new List<string>
                    {
                        "CREATE TABLE IF NOT EXISTS tenants (guid TEXT PRIMARY KEY, name TEXT, active INTEGER NOT NULL DEFAULT 1, isprotected INTEGER NOT NULL DEFAULT 0, createdutc TEXT NOT NULL, lastupdateutc TEXT NOT NULL)",
                        "CREATE TABLE IF NOT EXISTS users (guid TEXT PRIMARY KEY, tenantguid TEXT, firstname TEXT, lastname TEXT, email TEXT, passwordsha256 TEXT, isadmin INTEGER NOT NULL DEFAULT 0, istenantadmin INTEGER NOT NULL DEFAULT 0, active INTEGER NOT NULL DEFAULT 1, isprotected INTEGER NOT NULL DEFAULT 0, createdutc TEXT NOT NULL, lastupdateutc TEXT NOT NULL)",
                        "CREATE INDEX IF NOT EXISTS idx_users_tenant_email ON users (tenantguid, email)",
                        "CREATE TABLE IF NOT EXISTS credentials (guid TEXT PRIMARY KEY, userguid TEXT, tenantguid TEXT, name TEXT, accesskey TEXT, secretsha256 TEXT, active INTEGER NOT NULL DEFAULT 1, isprotected INTEGER NOT NULL DEFAULT 0, createdutc TEXT NOT NULL, lastupdateutc TEXT NOT NULL, lastusedutc TEXT, expiresutc TEXT)",
                        "CREATE INDEX IF NOT EXISTS idx_credentials_accesskey ON credentials (accesskey)",
                        "CREATE TABLE IF NOT EXISTS authsessions (guid TEXT PRIMARY KEY, userguid TEXT, tenantguid TEXT, principaltype TEXT, active INTEGER NOT NULL DEFAULT 1, isprotected INTEGER NOT NULL DEFAULT 0, createdutc TEXT NOT NULL, expiresutc TEXT NOT NULL, lastusedutc TEXT, revokedutc TEXT, revocationreason TEXT)",
                        "CREATE TABLE IF NOT EXISTS audit_log (guid TEXT PRIMARY KEY, tenantguid TEXT, eventtype TEXT, principaltype TEXT, principalguid TEXT, method TEXT, path TEXT, ipaddress TEXT, authresult INTEGER, authzresult INTEGER, denialreason TEXT, statuscode INTEGER, createdutc TEXT NOT NULL)",
                        "CREATE INDEX IF NOT EXISTS idx_audit_createdutc ON audit_log (createdutc)"
                    }),
                    new SchemaMigration(4, "RBAC schema", new List<string>
                    {
                        "CREATE TABLE IF NOT EXISTS userroles (guid TEXT PRIMARY KEY, tenantguid TEXT, name TEXT, isbuiltin INTEGER NOT NULL DEFAULT 0, active INTEGER NOT NULL DEFAULT 1, isprotected INTEGER NOT NULL DEFAULT 0, createdutc TEXT NOT NULL, lastupdateutc TEXT NOT NULL)",
                        "CREATE INDEX IF NOT EXISTS idx_userroles_name ON userroles (name)",
                        "CREATE TABLE IF NOT EXISTS permissions (guid TEXT PRIMARY KEY, tenantguid TEXT, name TEXT, resourcetypes TEXT, operationtypes TEXT, permissiontype TEXT, active INTEGER NOT NULL DEFAULT 1, isprotected INTEGER NOT NULL DEFAULT 0, createdutc TEXT NOT NULL, lastupdateutc TEXT NOT NULL)",
                        "CREATE TABLE IF NOT EXISTS rolepermissionmaps (guid TEXT PRIMARY KEY, tenantguid TEXT, roleguid TEXT, permissionguid TEXT, active INTEGER NOT NULL DEFAULT 1, isprotected INTEGER NOT NULL DEFAULT 0, createdutc TEXT NOT NULL, lastupdateutc TEXT NOT NULL)",
                        "CREATE INDEX IF NOT EXISTS idx_rpm_roleguid ON rolepermissionmaps (roleguid)",
                        "CREATE TABLE IF NOT EXISTS userroleassignments (guid TEXT PRIMARY KEY, tenantguid TEXT, userguid TEXT, roleguid TEXT, rolename TEXT, resourcescope TEXT, resourceguid TEXT, inheritstochildren INTEGER NOT NULL DEFAULT 1, active INTEGER NOT NULL DEFAULT 1, isprotected INTEGER NOT NULL DEFAULT 0, createdutc TEXT NOT NULL, lastupdateutc TEXT NOT NULL)",
                        "CREATE INDEX IF NOT EXISTS idx_ura_tenant_user ON userroleassignments (tenantguid, userguid)",
                        "CREATE TABLE IF NOT EXISTS credentialscopeassignments (guid TEXT PRIMARY KEY, tenantguid TEXT, credentialguid TEXT, roleguid TEXT, rolename TEXT, resourcescope TEXT, resourceguid TEXT, inheritstochildren INTEGER NOT NULL DEFAULT 1, permissions TEXT, resourcetypes TEXT, active INTEGER NOT NULL DEFAULT 1, isprotected INTEGER NOT NULL DEFAULT 0, createdutc TEXT NOT NULL, lastupdateutc TEXT NOT NULL)",
                        "CREATE INDEX IF NOT EXISTS idx_csa_tenant_cred ON credentialscopeassignments (tenantguid, credentialguid)"
                    }),

                    new SchemaMigration(5, "Model presets schema", new List<string>
                    {
                        "CREATE TABLE IF NOT EXISTS model_presets (guid TEXT PRIMARY KEY, name TEXT, modelname TEXT, systemprompt TEXT, temperature TEXT, maxtokens INTEGER, topp TEXT, templateoverride TEXT, stopjson TEXT, createdutc TEXT NOT NULL, lastupdateutc TEXT NOT NULL)",
                        "CREATE INDEX IF NOT EXISTS idx_model_presets_name ON model_presets (name)"
                    })
                };
            }
        }

        #endregion
    }
}
