namespace SharpAI.Classes.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Runtime.Intrinsics.X86;
    using LLama.Native;

    /// <summary>Configures LLamaSharp native loading consistently on .NET 8 and .NET 10.</summary>
    public static class NativeLibraryLoader
    {
        /// <summary>Select an explicit llama library before the first native call. No native library is loaded here. An existing backend identity is preserved; otherwise this overload selects CPU. Use the backend overload for explicit acceleration.</summary>
        /// <remarks>Concurrent configuration is serialized on .NET 8. Configure once during startup before other threads make native calls.</remarks>
        /// <param name="libraryPath">Non-null, nonempty path to the selected backend library, resolved against the current directory.</param>
        /// <exception cref="ArgumentException">The path is empty or whitespace.</exception>
        /// <exception cref="ArgumentNullException">The path is null.</exception>
        /// <exception cref="InvalidOperationException">The native backend has already loaded.</exception>
        public static void Configure(string libraryPath)
        {
            string backend = NativeBackendInfo.SelectedBackend;
            Configure(libraryPath, backend == "metal" || backend == "cuda" ? backend : "cpu");
        }

        /// <summary>Select an explicit native library and its backend before the first native call.</summary>
        /// <param name="libraryPath">Non-null, nonempty library path resolved against the current directory.</param>
        /// <param name="backend">Backend identity: cpu, cuda, or metal. Case-insensitive.</param>
        /// <exception cref="ArgumentException">The path is blank or the backend is invalid.</exception>
        /// <exception cref="ArgumentNullException">The path or backend is null.</exception>
        /// <exception cref="InvalidOperationException">The native backend has already loaded.</exception>
        /// <remarks>Thread-safe. Configure before starting native work. Automatic engine initialization preserves this selection.</remarks>
        public static void Configure(string libraryPath, string backend)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(libraryPath);
            ArgumentException.ThrowIfNullOrWhiteSpace(backend);
            string normalized = backend.ToLowerInvariant();
            if (normalized != "cpu" && normalized != "cuda" && normalized != "metal")
                throw new ArgumentException("Backend must be cpu, cuda, or metal.", nameof(backend));
            string fullPath = Path.GetFullPath(libraryPath);
            lock (_Lock)
            {
#if NET10_0_OR_GREATER
                NativeLibraryConfig.LLama.WithLibrary(fullPath);
#else
                if (_Handles.ContainsKey("llama")) throw new InvalidOperationException("The native backend has already loaded.");
#endif
                _LibraryPath = fullPath;
                NativeBackendInfo.SelectedBackend = normalized;
            }
        }

        /// <summary>Gets the explicit library path, or null before configuration. Access is synchronized.</summary>
        public static string ConfiguredLibraryPath { get { lock (_Lock) { return _LibraryPath; } } }

        private static readonly object _Lock = new object();
        private static string _LibraryPath;
#if !NET10_0_OR_GREATER
        private static readonly Dictionary<string, IntPtr> _Handles = new Dictionary<string, IntPtr>();

        // LLamaSharp 0.30 ships net10.0 and netstandard2.0 assemblies. The latter has no
        // custom native loader: runtime libraries are content files, not native NuGet assets.
        // Register lazily-loading resolution so embedded .NET 8 callers work without a server host.
#pragma warning disable CA2255 // Required to initialize native resolution before any external LLamaSharp call.
        [ModuleInitializer]
        internal static void Register()
        {
            NativeLibrary.SetDllImportResolver(typeof(NativeApi).Assembly, Resolve);
        }
#pragma warning restore CA2255

        private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (name != "llama" && name != "ggml" && name != "ggml-base") return IntPtr.Zero;
            lock (_Lock)
            {
                if (_Handles.TryGetValue(name, out IntPtr handle)) return handle;
                string path = _LibraryPath ?? FindCpuLibrary();
                string directory = Path.GetDirectoryName(path);
                // The macOS binaries refer to versioned install names. Load the packaged
                // unversioned dependencies first, in dependency order, as the net10 loader does.
                foreach (string dependency in new[] { "ggml-base", "ggml-cpu", "ggml-blas", "ggml-metal", "ggml-cuda", "ggml" })
                {
                    string file = Path.Combine(directory, Filename(dependency));
                    if (File.Exists(file) && !_Handles.ContainsKey(dependency)) _Handles[dependency] = NativeLibrary.Load(file);
                }
                if (!_Handles.ContainsKey("llama")) _Handles["llama"] = NativeLibrary.Load(path);
                return _Handles.TryGetValue(name, out handle) ? handle : IntPtr.Zero;
            }
        }

        private static string FindCpuLibrary()
        {
            string os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
            string arch = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
            string root = Path.Combine(AppContext.BaseDirectory, "runtimes", os + "-" + arch, "native");
            List<string> directories = new List<string>();
            if (Avx512F.IsSupported && Avx512BW.IsSupported && Avx512Vbmi.IsSupported) directories.Add(Path.Combine(root, "avx512"));
            if (Avx2.IsSupported) directories.Add(Path.Combine(root, "avx2"));
            if (Avx.IsSupported) directories.Add(Path.Combine(root, "avx"));
            directories.Add(Path.Combine(root, "noavx"));
            directories.Add(root);
            directories.Add(AppContext.BaseDirectory);
            foreach (string directory in directories)
            {
                string path = Path.Combine(directory, Filename("llama"));
                if (File.Exists(path)) return path;
            }
            throw new DllNotFoundException("No compatible LLamaSharp CPU backend was found under " + root + ".");
        }

        private static string Filename(string name)
        {
            return OperatingSystem.IsWindows() ? name + ".dll" : "lib" + name + (OperatingSystem.IsMacOS() ? ".dylib" : ".so");
        }
#endif
    }
}
