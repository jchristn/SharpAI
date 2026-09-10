namespace SharpAI.Server.API.REST.Routes
{
    using System;

    using SharpAI.Database;
    using SharpAI.Serialization;
    using SharpAI.Server.Classes.Runtime;
    using SharpAI.Server.Classes.Settings;
    using SharpAI.Services;
    using WatsonWebserver;

    /// <summary>
    /// Dependency carrier passed to each per-feature route registrar (W7.T2/T3). This is the seam that lets
    /// route registration move out of the monolithic composition root: a registrar receives everything it
    /// needs through this context rather than reaching into server-global static state.
    /// <para>
    /// Settings are supplied through an accessor delegate rather than a captured instance because the live
    /// settings object is replaced wholesale when settings are updated at runtime; a captured reference
    /// would go stale. Services that are created once at startup are passed as instances.
    /// </para>
    /// </summary>
    public class RouteContext
    {
        #region Public-Members

        /// <summary>
        /// The webserver on which routes are registered. Never null.
        /// </summary>
        public Webserver Server
        {
            get { return _Server; }
        }

        /// <summary>
        /// Server software version string.
        /// </summary>
        public string Version
        {
            get { return _Version; }
        }

        /// <summary>
        /// Returns the current, live settings instance. Always call through this rather than caching, since
        /// the settings object is replaced when configuration is updated at runtime.
        /// </summary>
        /// <returns>The current settings.</returns>
        public Settings Settings()
        {
            return _SettingsAccessor();
        }

        /// <summary>
        /// Provider-neutral database driver, or null before initialization.
        /// </summary>
        public DatabaseDriverBase Database
        {
            get { return _Database; }
        }

        /// <summary>
        /// Model file (registry) service.
        /// </summary>
        public ModelFileService ModelFiles
        {
            get { return _ModelFiles; }
        }

        /// <summary>
        /// Model engine service.
        /// </summary>
        public ModelEngineService ModelEngines
        {
            get { return _ModelEngines; }
        }

        /// <summary>
        /// Telemetry host, or null when telemetry is disabled.
        /// </summary>
        public TelemetryHost Telemetry
        {
            get { return _Telemetry; }
        }

        /// <summary>
        /// RBAC authorization gate. May be null in minimal (general-routes-only) contexts.
        /// </summary>
        public AuthorizationGate Auth
        {
            get { return _Auth; }
        }

        /// <summary>
        /// JSON serializer. May be null in minimal contexts.
        /// </summary>
        public Serializer Serializer
        {
            get { return _Serializer; }
        }

        /// <summary>
        /// Replace the live settings instance (used by the settings PUT route). May be null.
        /// </summary>
        public Action<Settings> ReplaceSettings
        {
            get { return _ReplaceSettings; }
        }

        /// <summary>
        /// Path to the settings file on disk. May be null in minimal contexts.
        /// </summary>
        public string SettingsFilePath
        {
            get { return _SettingsFilePath; }
        }

        #endregion

        #region Private-Members

        private Webserver _Server = null;
        private string _Version = null;
        private Func<Settings> _SettingsAccessor = null;
        private DatabaseDriverBase _Database = null;
        private ModelFileService _ModelFiles = null;
        private ModelEngineService _ModelEngines = null;
        private TelemetryHost _Telemetry = null;
        private AuthorizationGate _Auth = null;
        private Serializer _Serializer = null;
        private Action<Settings> _ReplaceSettings = null;
        private string _SettingsFilePath = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create a route context.
        /// </summary>
        /// <param name="server">Webserver on which routes are registered. Required.</param>
        /// <param name="version">Server version string. Required.</param>
        /// <param name="settingsAccessor">Accessor returning the current live settings. Required.</param>
        /// <param name="database">Database driver. May be null before initialization.</param>
        /// <param name="modelFiles">Model file service. May be null.</param>
        /// <param name="modelEngines">Model engine service. May be null.</param>
        /// <param name="telemetry">Telemetry host. May be null when telemetry is disabled.</param>
        public RouteContext(
            Webserver server,
            string version,
            Func<Settings> settingsAccessor,
            DatabaseDriverBase database,
            ModelFileService modelFiles,
            ModelEngineService modelEngines,
            TelemetryHost telemetry)
        {
            _Server = server ?? throw new ArgumentNullException(nameof(server));
            _Version = !String.IsNullOrEmpty(version) ? version : throw new ArgumentNullException(nameof(version));
            _SettingsAccessor = settingsAccessor ?? throw new ArgumentNullException(nameof(settingsAccessor));
            _Database = database;
            _ModelFiles = modelFiles;
            _ModelEngines = modelEngines;
            _Telemetry = telemetry;
        }

        /// <summary>
        /// Attach control-plane dependencies (authorization, serialization, settings mutation) needed by the
        /// settings/request-history/management registrars. Returns this instance for chaining.
        /// </summary>
        /// <param name="auth">Authorization gate.</param>
        /// <param name="serializer">JSON serializer.</param>
        /// <param name="replaceSettings">Delegate that replaces the live settings instance.</param>
        /// <param name="settingsFilePath">Path to the settings file on disk.</param>
        /// <returns>This instance.</returns>
        public RouteContext ConfigureControlPlane(
            AuthorizationGate auth,
            Serializer serializer,
            Action<Settings> replaceSettings,
            string settingsFilePath)
        {
            _Auth = auth;
            _Serializer = serializer;
            _ReplaceSettings = replaceSettings;
            _SettingsFilePath = settingsFilePath;
            return this;
        }

        #endregion
    }
}
