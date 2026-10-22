namespace SharpAI.Server.Classes.Settings
{
    using System;
    using System.Collections.Generic;
    using SharpAI.Database;
    using WatsonWebserver.Core;

    /// <summary>
    /// Settings.
    /// </summary>
    public class Settings
    {
        #region Public-Members

        /// <summary>
        /// Timestamp from creation, in UTC time.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Configuration schema version, used to migrate settings across major releases. Defaults to the
        /// current schema version when absent from a loaded file.
        /// </summary>
        public string SchemaVersion
        {
            get
            {
                return _SchemaVersion;
            }
            set
            {
                _SchemaVersion = String.IsNullOrEmpty(value) ? "2.0.0" : value;
            }
        }

        /// <summary>
        /// Software version.
        /// </summary>
        public string SoftwareVersion
        {
            get
            {
                return _SoftwareVersion;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(SoftwareVersion));
                _SoftwareVersion = value;
            }
        }

        /// <summary>
        /// Logging settings.
        /// </summary>
        public LoggingSettings Logging
        {
            get
            {
                return _Logging;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Logging));
                _Logging = value;
            }
        }

        /// <summary>
        /// Storage settings.
        /// </summary>
        public StorageSettings Storage
        {
            get
            {
                return _Storage;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Storage));
                _Storage = value;
            }
        }

        /// <summary>
        /// Database settings.
        /// </summary>
        public DatabaseSettings Database
        {
            get
            {
                return _Database;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Database));
                _Database = value;
            }
        }

        /// <summary>
        /// HuggingFace settings.
        /// </summary>
        public HuggingFaceSettings HuggingFace
        {
            get
            {
                return _HuggingFace;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(HuggingFace));
                _HuggingFace = value;
            }
        }

        /// <summary>
        /// REST settings.
        /// </summary>
        public WebserverSettings Rest
        {
            get
            {
                return _Rest;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Rest));
                _Rest = value;
            }
        }

        /// <summary>
        /// Debug settings.
        /// </summary>
        public DebugSettings Debug
        {
            get => _Debug;
            set => _Debug = (value != null ? value : new DebugSettings());
        }

        /// <summary>
        /// Quantization priority.  If null or empty, Ollama quantization priority will be used.
        /// </summary>
        public Dictionary<string, int> QuantizationPriority
        {
            get
            {
                return _QuantizationPriority;
            }
            set
            {
                if (value == null) value = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
                _QuantizationPriority = value;
            }
        }

        /// <summary>
        /// Runtime settings for native library backend configuration.
        /// </summary>
        public RuntimeSettings Runtime
        {
            get
            {
                return _Runtime;
            }
            set
            {
                if (value == null) value = new RuntimeSettings();
                _Runtime = value;
            }
        }

        /// <summary>
        /// Telemetry settings for OpenTelemetry metrics, traces, and logs.
        /// </summary>
        public TelemetrySettings Telemetry
        {
            get
            {
                return _Telemetry;
            }
            set
            {
                if (value == null) value = new TelemetrySettings();
                _Telemetry = value;
            }
        }

        /// <summary>
        /// Request-history capture settings.
        /// </summary>
        public RequestHistorySettings RequestHistory
        {
            get
            {
                return _RequestHistory;
            }
            set
            {
                if (value == null) value = new RequestHistorySettings();
                _RequestHistory = value;
            }
        }

        /// <summary>
        /// Authentication settings. Disabled by default (open server, Ollama parity).
        /// </summary>
        public AuthSettings Auth
        {
            get
            {
                return _Auth;
            }
            set
            {
                if (value == null) value = new AuthSettings();
                _Auth = value;
            }
        }

        #endregion

        #region Private-Members

        private string _SchemaVersion = "2.0.0";
        private string _SoftwareVersion = "4.0.1";
        private LoggingSettings _Logging = new LoggingSettings();
        private StorageSettings _Storage = new StorageSettings();
        private DatabaseSettings _Database = new DatabaseSettings(Constants.DatabaseFile);
        private HuggingFaceSettings _HuggingFace = new HuggingFaceSettings();
        private WebserverSettings _Rest = new WebserverSettings();
        private DebugSettings _Debug = new DebugSettings();
        private Dictionary<string, int> _QuantizationPriority = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
        private RuntimeSettings _Runtime = new RuntimeSettings();
        private TelemetrySettings _Telemetry = new TelemetrySettings();
        private RequestHistorySettings _RequestHistory = new RequestHistorySettings();
        private AuthSettings _Auth = new AuthSettings();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public Settings()
        {
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}