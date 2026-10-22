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
        /// <summary>Select an explicit llama library before the first native call. No native library is loaded here.</summary>
        /// <remarks>Concurrent configuration is serialized on .NET 8. Configure once during startup before other threads make native calls.</remarks>
        /// <param name="libraryPath">Non-null, nonempty path to the selected backend library, resolved against the current directory.</param>
        /// <exception cref="ArgumentException">The path is empty or whitespace.</exception>
        /// <exception cref="ArgumentNullException">The path is null.</exception>
        /// <exception cref="InvalidOperationException">The native backend has already loaded.</exception>
        public static void Configure(string libraryPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(libraryPath);
            string fullPath = Path.GetFullPath(libraryPath);
#if NET10_0_OR_GREATER
            NativeLibraryConfig.LLama.WithLibrary(fullPath);
#else
            lock (_Lock)
            {
                if (_Handles.ContainsKey("llama")) throw new InvalidOperationException("The native backend has already loaded.");
                _LibraryPath = fullPath;
            }
#endif
        }

#if !NET10_0_OR_GREATER
        private static readonly object _Lock = new object();
        private static readonly Dictionary<string, IntPtr> _Handles = new Dictionary<string, IntPtr>();
        private static string _LibraryPath;

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
