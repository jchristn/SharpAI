namespace SharpAI.Tui
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Client;
    using SharpAI.Tui.Services;
    using TUIKit.Terminal;

    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            if (Array.IndexOf(args, "--help") >= 0)
            {
                Console.WriteLine("sharpai-tui [--server http(s)://host:port] [--locale en] [--profile-directory path]\nF1 help, F2 navigation, Ctrl+P commands, Ctrl+Q quit. Credentials are entered interactively, held in memory, and never accepted as arguments.");
                return 0;
            }
            if (Array.IndexOf(args, "--version") >= 0) { Console.WriteLine("sharpai-tui 4.0.1"); return 0; }
            if (Console.IsInputRedirected || Console.IsOutputRedirected)
            {
                Console.Error.WriteLine("The dashboard requires an interactive terminal. Use --help for command-line options."); return 2;
            }
            try
            {
                string? directory = Option(args, "--profile-directory");
                ProfileStore store = new ProfileStore(directory);
                ConnectionProfile profile = store.Load();
                profile.Server = Option(args, "--server") ?? profile.Server;
                profile.Locale = Option(args, "--locale") ?? profile.Locale;
                using ClientTelemetry telemetry = new ClientTelemetry(Path.Combine(directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sharpai"), "logs"), profile.TelemetryEnabled, profile.OtlpEndpoint);
                using ServerClient client = new ServerClient(profile.Server);
                if (!string.IsNullOrWhiteSpace(profile.TokenFile))
                {
                    if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(profile.TokenFile) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite)) != 0) throw new IOException("The selected token file must have owner-only permissions.");
                    client.Token = File.ReadAllText(profile.TokenFile).Trim();
                }
                using ConsoleBackend backend = new ConsoleBackend();
                using SharpAITuiApp application = new SharpAITuiApp(client, backend, profile, store, telemetry);
                await application.RunAsync(CancellationToken.None).ConfigureAwait(false);
                return 0;
            }
            catch (Exception error) when (error is ArgumentException || error is IOException || error is System.Text.Json.JsonException || error is InvalidOperationException)
            {
                Console.Error.WriteLine("SharpAI terminal stopped: " + SafeText.Clean(error.Message));
                return 1;
            }
        }
        private static string? Option(string[] args, string name)
        {
            int index = Array.IndexOf(args, name);
            if (index < 0) return null;
            if (index + 1 == args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException("Missing value for " + name);
            return args[index + 1];
        }
    }
}
