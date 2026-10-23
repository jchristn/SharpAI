namespace SharpAI.Serve
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Classes.Runtime;
    using SyslogLogging;

    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            ServeOptions options;
            try { options = ServeOptions.Parse(args); }
            catch (Exception error) when (error is ArgumentException || error is NotSupportedException || error is IOException)
            { Console.Error.WriteLine("sharpai: " + error.Message); return 2; }
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
                settings.EnableConsole = !options.Quiet;
                settings.EnableColors = false;
                settings.UseUtcTime = true;
                settings.HeaderFormat = "{ts} {sev}";
                settings.TimestampFormat = "yyyy-MM-dd HH:mm:ss.ffffff";
                settings.FileLogging = options.LogFile == null ? FileLoggingMode.Disabled : FileLoggingMode.SingleLogFile;
                settings.MinimumSeverity = options.LogLevel switch { "debug" => Severity.Debug, "warning" => Severity.Warn, "error" => Severity.Error, _ => Severity.Info };
                if (options.LogFile != null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(options.LogFile)!);
                    settings.LogFilename = options.LogFile;
                }
                logging.Settings = settings;
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
                Console.Error.WriteLine("sharpai: " + error.Message);
                return 1;
            }
            finally { Console.CancelKeyPress -= interrupt; }
        }
    }
}
