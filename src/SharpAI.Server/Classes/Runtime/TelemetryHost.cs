namespace SharpAI.Server.Classes.Runtime
{
    using System;

    using Radiant;

    using SharpAI.Server.Classes.Settings;
    using SharpAI.Telemetry;

    using SyslogLogging;

    /// <summary>
    /// Hosts the in-process OpenTelemetry pipeline for the server using Radiant. Subscribes to the core
    /// <see cref="SharpAITelemetry"/> meters and activity source (and to Watson's telemetry source when the
    /// web server emits it) and exports over OTLP, optionally serving an in-process Prometheus endpoint.
    ///
    /// Startup failures never propagate: if the telemetry host cannot start, the error is logged and the
    /// server continues without telemetry. Thread-safe for construction and disposal; the underlying host
    /// is created once and disposed once.
    /// </summary>
    public sealed class TelemetryHost : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Whether the telemetry host started successfully and is actively exporting.
        /// </summary>
        public bool IsActive
        {
            get { return _Host != null; }
        }

        #endregion

        #region Private-Members

        private readonly string _Header = "[TelemetryHost] ";
        private readonly LoggingModule _Logging;
        private RadiantHost _Host = null;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate and start the telemetry host from the provided settings. Environment variables of the
        /// form <c>SHARPAI_TELEMETRY_*</c> override the settings values.
        /// </summary>
        /// <param name="settings">Telemetry settings. May not be null.</param>
        /// <param name="logging">Logging module. May not be null.</param>
        public TelemetryHost(TelemetrySettings settings, LoggingModule logging)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));

            bool enable = EnvBool("SHARPAI_TELEMETRY_ENABLE", settings.Enable);
            if (!enable)
            {
                SharpAI.Logging.JsonLog.Debug(_Logging, _Header + "telemetry disabled");
                return;
            }

            try
            {
                string serviceName = EnvString("SHARPAI_TELEMETRY_SERVICE_NAME", settings.ServiceName);
                string otlpEndpoint = EnvString("SHARPAI_TELEMETRY_OTLP_ENDPOINT", settings.OtlpEndpoint);
                string otlpProtocol = EnvString("SHARPAI_TELEMETRY_OTLP_PROTOCOL", settings.OtlpProtocol);
                bool prometheusEnable = EnvBool("SHARPAI_TELEMETRY_PROMETHEUS_ENABLE", settings.PrometheusEnable);
                string prometheusPath = EnvString("SHARPAI_TELEMETRY_PROMETHEUS_PATH", settings.PrometheusPath);
                int prometheusPort = EnvInt("SHARPAI_TELEMETRY_PROMETHEUS_PORT", settings.PrometheusPort);

                RadiantSettings radiant = new RadiantSettings(serviceName);
                radiant.Enable = true;

                radiant.Metrics.Enable = settings.EnableMetrics;
                radiant.Traces.Enable = settings.EnableTraces;
                radiant.Logs.Enable = settings.EnableLogs;

                radiant.Otlp.Enable = true;
                radiant.Otlp.Endpoint = otlpEndpoint;
                radiant.Otlp.Protocol = otlpProtocol.Equals("httpprotobuf", StringComparison.OrdinalIgnoreCase)
                    ? OtlpProtocolEnum.HttpProtobuf
                    : OtlpProtocolEnum.Grpc;

                radiant.Prometheus.Enable = prometheusEnable;
                radiant.Prometheus.Hostname = settings.PrometheusHostname;
                radiant.Prometheus.Port = prometheusPort;
                radiant.Prometheus.Path = prometheusPath;

                radiant.Sources.AddMeter(SharpAITelemetry.MeterName);
                radiant.Sources.AddMeter(SharpAITelemetry.ModelsMeterName);
                radiant.Sources.AddActivitySource(SharpAITelemetry.ActivitySourceName);

                // Watson emits its own "Watson" meter/source starting in 7.1; subscribing is harmless on
                // earlier versions where nothing is produced.
                radiant.Sources.AddActivitySource("Watson");
                radiant.Sources.AddMeter("Watson");

                _Host = RadiantHost.Start(radiant);

                SharpAI.Logging.JsonLog.Info(_Logging, _Header + "telemetry started; service=" + serviceName + " otlp=" + otlpEndpoint
                    + (prometheusEnable ? " prometheus=" + settings.PrometheusHostname + ":" + prometheusPort + prometheusPath : ""));
            }
            catch (Exception ex)
            {
                _Host = null;
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "telemetry failed to start; continuing without it:" + Environment.NewLine + ex.ToString());
            }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Dispose the telemetry host, flushing exporters and releasing any Prometheus port. Safe to call
        /// multiple times.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;

            try
            {
                _Host?.Dispose();
            }
            catch (Exception ex)
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "exception disposing telemetry host:" + Environment.NewLine + ex.ToString());
            }
            finally
            {
                _Host = null;
            }
        }

        #endregion

        #region Private-Methods

        private static string EnvString(string name, string fallback)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return String.IsNullOrEmpty(value) ? fallback : value;
        }

        private static bool EnvBool(string name, bool fallback)
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (String.IsNullOrEmpty(value)) return fallback;
            if (Boolean.TryParse(value, out bool parsed)) return parsed;
            return value == "1" || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        private static int EnvInt(string name, int fallback)
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (String.IsNullOrEmpty(value)) return fallback;
            return Int32.TryParse(value, out int parsed) ? parsed : fallback;
        }

        #endregion
    }
}
