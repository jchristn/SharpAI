namespace Test.Shared
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Classes.Runtime;
    using SharpAI.Engines;
    using SyslogLogging;

    /// <summary>Standalone process probe for library-only backend selection and optional real-model offload.</summary>
    public static class NativeBackendProbe
    {
        /// <summary>Run without referencing or initializing SharpAI.Server.</summary>
        /// <param name="args">Probe flag, expected backend, expected GPU layers, and optional GGUF model path, --missing-metal, or --explicit-cpu scenario.</param>
        /// <returns>Zero when selection, overrides, and optional inference succeed.</returns>
        /// <exception cref="ArgumentException">The expected backend or layer count is missing.</exception>
        /// <exception cref="InvalidOperationException">Observed selection differs from the expected contract.</exception>
        public static async Task<int> RunAsync(string[] args)
        {
            if (args.Length < 3) throw new ArgumentException("Expected backend and GPU layer count are required.", nameof(args));
            LoggingModule logging = new LoggingModule();
            if (args.Length > 3 && args[3] == "--missing-metal")
                NativeBackendInitializer.Initialize(new NativeBackendOptions { ForceBackend = "metal", MetalBackendPath = Path.Combine(AppContext.BaseDirectory, "missing-metal", "libllama.dylib") }, logging);
            if (args.Length > 3 && args[3] == "--explicit-cpu")
                NativeLibraryLoader.Configure(Path.Combine(AppContext.BaseDirectory, "runtimes", "osx-arm64", "native", "libllama.dylib"), "cpu");
            using LlamaSharpEngine engine = new LlamaSharpEngine(logging);
            int layers = engine.GetOptimalGpuLayers();
            if (NativeBackendInfo.SelectedBackend != args[1] || layers != Int32.Parse(args[2]))
                throw new InvalidOperationException($"Expected {args[1]}/{args[2]}, observed {NativeBackendInfo.SelectedBackend}/{layers}.");
            NativeBackendInitializer.Initialize(new NativeBackendOptions { ForceBackend = args[1] == "cpu" ? "metal" : "cpu" }, logging);
            if (engine.GetOptimalGpuLayers() != layers || NativeBackendInfo.SelectedBackend != args[1])
                throw new InvalidOperationException("Later initialization changed the initial backend selection.");
            if (args.Length > 3 && !args[3].StartsWith("--", StringComparison.Ordinal))
            {
                await engine.InitializeAsync(args[3]).ConfigureAwait(false);
                string text = await engine.GenerateTextAsync("Once upon a time", 4, 0, null, null, CancellationToken.None).ConfigureAwait(false);
                if (String.IsNullOrEmpty(text)) throw new InvalidOperationException("The selected backend did not generate output.");
            }
            if (AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "SharpAI.Server"))
                throw new InvalidOperationException("Probe must run without the server assembly.");
            Console.WriteLine(JsonSerializer.Serialize(new { backend = NativeBackendInfo.SelectedBackend, gpu_layers = layers, initialized = NativeBackendInitializer.IsInitialized, library = NativeLibraryLoader.ConfiguredLibraryPath }));
            return 0;
        }
    }
}
