namespace SharpAI.Serve
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Net;

    internal sealed class ServeOptions
    {
        internal string ModelPath { get; private set; } = String.Empty;
        internal string Host { get; private set; } = "127.0.0.1";
        internal int Port { get; private set; } = 8000;
        internal string ModelName { get; private set; } = String.Empty;
        internal string Api { get; private set; } = "both";
        internal string Backend { get; private set; } = "auto";
        internal int ContextSize { get; private set; }
        internal int MaxTokens { get; private set; } = 2048;
        internal int Threads { get; private set; }
        internal int BatchThreads { get; private set; }
        internal string GpuLayers { get; private set; } = "auto";
        internal int MainGpu { get; private set; }
        internal int Parallel { get; private set; } = 1;
        internal float Temperature { get; private set; } = 0.6f;
        internal int? Seed { get; private set; }
        internal string? ChatTemplatePath { get; private set; }
        internal string? LogFile { get; private set; }
        internal string LogLevel { get; private set; } = "info";
        internal string? NativeLibrary { get; private set; }
        internal bool Quiet { get; private set; }
        internal bool Help { get; private set; }
        internal bool Version { get; private set; }

        internal static ServeOptions Parse(string[] args)
        {
            ServeOptions result = new ServeOptions();
            if (args.Length == 0 || Array.Exists(args, value => value is "--help" or "-h" or "/?") || args.Length == 1 && args[0] == "usage" || args.Length == 2 && args[0] == "serve" && args[1] == "usage")
            { result.Help = true; return result; }
            if (args.Length == 1 && args[0] == "--version") { result.Version = true; return result; }
            if (args[0] != "serve") throw new ArgumentException("Expected 'serve <model.gguf>'. Use 'sharpai --help'.");
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            bool positionalOnly = false;
            for (int index = 1; index < args.Length; index++)
            {
                string argument = args[index];
                if (!positionalOnly && argument == "--") { positionalOnly = true; continue; }
                if (positionalOnly || !argument.StartsWith("-", StringComparison.Ordinal))
                {
                    if (result.ModelPath.Length > 0) throw new ArgumentException("Exactly one model path is required.");
                    result.ModelPath = Path.GetFullPath(argument);
                    continue;
                }
                int equals = argument.IndexOf('=');
                string option = equals < 0 ? argument : argument.Substring(0, equals);
                if (!seen.Add(option)) throw new ArgumentException("Repeated option: " + option);
                if (option == "--quiet" && equals < 0) { result.Quiet = true; continue; }
                string value;
                if (equals >= 0) value = argument.Substring(equals + 1);
                else if (++index < args.Length && !args[index].StartsWith("--", StringComparison.Ordinal)) value = args[index];
                else throw new ArgumentException("A value is required for " + option + ".");
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentException("A nonempty value is required for " + option + ".");
                switch (option)
                {
                    case "--host":
                        if (value != "localhost" && !IPAddress.TryParse(value, out IPAddress? address)) throw new ArgumentException("--host must be an IP address or localhost.");
                        result.Host = value == "localhost" ? "127.0.0.1" : value; break;
                    case "--port": result.Port = Number(option, value, 1, 65535); break;
                    case "--served-model-name": result.ModelName = value; break;
                    case "--api": result.Api = Choice(option, value, "ollama", "openai", "both"); break;
                    case "--backend": result.Backend = Choice(option, value, "auto", "cpu", "cuda", "metal"); break;
                    case "--context-size": result.ContextSize = Number(option, value, 1, Int32.MaxValue); break;
                    case "--max-tokens": result.MaxTokens = Number(option, value, 1, Int32.MaxValue); break;
                    case "--threads": result.Threads = Number(option, value, 1, Int32.MaxValue); break;
                    case "--batch-threads": result.BatchThreads = Number(option, value, 1, Int32.MaxValue); break;
                    case "--gpu-layers": result.GpuLayers = value == "all" ? "-1" : Number(option, value, 0, Int32.MaxValue).ToString(CultureInfo.InvariantCulture); break;
                    case "--main-gpu": result.MainGpu = Number(option, value, 0, Int32.MaxValue); break;
                    case "--parallel": result.Parallel = Number(option, value, 1, 1024); break;
                    case "--temperature":
                        if (!Single.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float temperature) || !Single.IsFinite(temperature) || temperature < 0 || temperature > 2) throw new ArgumentException("--temperature must be between 0 and 2.");
                        result.Temperature = temperature; break;
                    case "--seed": result.Seed = Number(option, value, Int32.MinValue, Int32.MaxValue); break;
                    case "--chat-template": result.ChatTemplatePath = Path.GetFullPath(value); break;
                    case "--native-library": result.NativeLibrary = Path.GetFullPath(value); break;
                    case "--log-file": result.LogFile = Path.GetFullPath(value); break;
                    case "--log-level": result.LogLevel = Choice(option, value, "debug", "info", "warning", "error"); break;
                    default: throw new ArgumentException("Unknown option: " + option);
                }
            }
            if (result.ModelPath.Length == 0) throw new ArgumentException("A local GGUF model path is required.");
            if (!File.Exists(result.ModelPath)) throw new ArgumentException("Model file does not exist: " + result.ModelPath);
            if (result.ModelName.Length == 0) result.ModelName = Path.GetFileNameWithoutExtension(result.ModelPath);
            if (result.ModelName.Length > 256 || Array.Exists(result.ModelName.ToCharArray(), Char.IsControl)) throw new ArgumentException("Model name must be at most 256 characters without control separators.");
            if (result.ChatTemplatePath != null && !File.Exists(result.ChatTemplatePath)) throw new ArgumentException("Chat template file does not exist: " + result.ChatTemplatePath);
            if (result.NativeLibrary != null && !File.Exists(result.NativeLibrary)) throw new ArgumentException("Native library file does not exist: " + result.NativeLibrary);
            if (result.NativeLibrary != null && result.Backend == "auto") throw new ArgumentException("--native-library requires an explicit --backend.");
            return result;
        }

        private static int Number(string option, string value, int minimum, int maximum)
        {
            if (!Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) || number < minimum || number > maximum) throw new ArgumentException(option + " must be an integer from " + minimum + " through " + maximum + ".");
            return number;
        }
        private static string Choice(string option, string value, params string[] choices)
        {
            if (Array.IndexOf(choices, value) < 0) throw new ArgumentException(option + " must be one of: " + String.Join(", ", choices) + ".");
            return value;
        }
    }
}
