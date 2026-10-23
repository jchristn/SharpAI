namespace SharpAI.Serve
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Classes.Runtime;
    using SyslogLogging;

    internal static class Program
    {
        private static bool RequestsJson(string[] args)
        {
            for (int index = 0; index < args.Length; index++)
            {
                if (args[index] == "--") break;
                if (args[index] == "--log-format=json" || args[index] == "--log-format" && index + 1 < args.Length && args[index + 1] == "json") return true;
            }
            return false;
        }
        private static void WriteError(bool json, string message, int exitCode)
        {
            Console.Error.WriteLine(json ? JsonSerializer.Serialize(new { schema_version = 1, timestamp = DateTime.UtcNow, severity = "Error", service = "sharpai", version = ServeVersion.Current, @event = "startup.failed", message, exit_code = exitCode }) : "sharpai: " + message);
        }
        private static async Task<int> Main(string[] args)
        {
            ServeOptions options;
            try { options = ServeOptions.Parse(args); }
            catch (Exception error) when (error is ArgumentException || error is NotSupportedException || error is IOException)
            { WriteError(RequestsJson(args), error.Message, 2); return 2; }
            if (options.Help) { Console.WriteLine(ServeUsage.Text); return 0; }
            if (options.Version) { Console.WriteLine("SharpAI.Serve " + ServeVersion.Current); return 0; }
            using CancellationTokenSource shutdown = new CancellationTokenSource();
            ConsoleCancelEventHandler interrupt = (sender, input) => { input.Cancel = true; shutdown.Cancel(); };
            Console.CancelKeyPress += interrupt;
            using PosixSignalRegistration? terminate = OperatingSystem.IsWindows() ? null : PosixSignalRegistration.Create(PosixSignal.SIGTERM, signal => { signal.Cancel = true; shutdown.Cancel(); });
            try
            {
                using LoggingModule logging = new LoggingModule();
                LoggingSettings settings = new LoggingSettings();
                settings.DetectConsole = false;
                settings.EnableConsole = !options.Quiet && options.LogFormat == "text";
                settings.EnableColors = false;
                settings.UseUtcTime = true;
                settings.HeaderFormat = "{ts} {sev}";
                settings.TimestampFormat = "yyyy-MM-dd HH:mm:ss.ffffff";
                settings.FileLogging = options.LogFile == null || options.LogFormat == "json" ? FileLoggingMode.Disabled : FileLoggingMode.SingleLogFile;
                settings.MinimumSeverity = options.LogLevel switch { "debug" => Severity.Debug, "warning" => Severity.Warn, "error" => Severity.Error, _ => Severity.Info };
                if (options.LogFile != null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(options.LogFile)!);
                    settings.LogFilename = options.LogFile;
                }
                logging.Settings = settings;
                using ServeLogOutput output = new ServeLogOutput(logging, options);
                ServeBanner.Write(logging);
                logging.Log(Severity.Info, "Loading " + options.ModelName + "...");
                ServeEnvironment.Apply(options);
                if (options.NativeLibrary != null) NativeLibraryLoader.Configure(options.NativeLibrary, options.Backend);
                NativeBackendInitializer.Initialize(new NativeBackendOptions { ForceBackend = options.Backend, EnableNativeLogging = false }, logging);
                using SingleModel model = new SingleModel(options, logging);
                await model.InitializeAsync(shutdown.Token).ConfigureAwait(false);
                using ServeHost host = new ServeHost(options, model, logging);
                await host.RunAsync(shutdown.Token).ConfigureAwait(false);
                return 0;
            }
            catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { return 0; }
            catch (Exception error)
            {
                WriteError(options.LogFormat == "json", error.Message, 1);
                return 1;
            }
            finally { Console.CancelKeyPress -= interrupt; }
        }
    }
}
