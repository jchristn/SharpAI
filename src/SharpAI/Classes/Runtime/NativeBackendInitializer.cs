namespace SharpAI.Classes.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Runtime.Intrinsics.X86;
    using LLama.Native;
    using SyslogLogging;

    /// <summary>
    /// Native library bootstrapper for LlamaSharp.
    /// Shared by standalone SharpAI engines and the server. Detects GPU availability and configures native loading once per process.
    /// </summary>
    public static class NativeBackendInitializer
    {
        /// <summary>
        /// Gets a value indicating whether the bootstrapper has been initialized.
        /// </summary>
        public static bool IsInitialized
        {
            get
            {
                return _IsInitialized;
            }
        }

        /// <summary>
        /// Gets the backend that was selected during initialization.
        /// </summary>
        public static string SelectedBackend
        {
            get
            {
                return _SelectedBackend;
            }
        }

        /// <summary>Initialize automatic backend selection with environment overrides and default library paths.</summary>
        /// <param name="logging">Optional logging module. Null creates a default module.</param>
        /// <remarks>Thread-safe and idempotent. Explicit configuration must precede the first native operation.</remarks>
        public static void Initialize(LoggingModule logging = null)
        {
            Initialize(new NativeBackendOptions(), logging ?? new LoggingModule());
        }

        private static bool _IsInitialized = false;
        private static string _SelectedBackend = "unknown";
        private static Exception _InitializationFailure;
        private static readonly object _InitializationLock = new object();

        /// <summary>
        /// Initialize the native library configuration.
        /// Call before the first native operation. SharpAI engines call this automatically; explicit host configuration should be supplied before creating engines.
        /// </summary>
        /// <param name="settings">Native backend configuration.</param>
        /// <param name="logging">Non-null logging module.</param>
        /// <exception cref="ArgumentNullException">Options or logging is null.</exception>
        /// <remarks>Initialization is serialized across threads. Later calls preserve the first selection.</remarks>
        public static void Initialize(NativeBackendOptions settings, LoggingModule logging)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (logging == null) throw new ArgumentNullException(nameof(logging));

            lock (_InitializationLock)
            {
                if (_IsInitialized)
                {
                    SharpAI.Logging.JsonLog.Debug(logging, "[NativeBackendInitializer] already initialized, skipping");
                    return;
                }

                if (_InitializationFailure != null)
                    throw new InvalidOperationException("Native backend initialization previously failed. Restart the process after correcting its configuration.", _InitializationFailure);

                // Preserve a host's explicit selection while still validating native loading.
                string configuredPath = NativeLibraryLoader.ConfiguredLibraryPath;
                string backend = configuredPath != null ? NativeBackendInfo.SelectedBackend : DetermineBackend(settings, logging);
                bool requireBackend = SharpAIEnvironment.GetBool(SharpAIEnvironment.RequireBackend, false);
                try
                {
                    try
                    {
                        ConfigureBackend(backend, settings, logging, configuredPath, configuredPath != null);
                    }
                    catch (Exception ex) when (!requireBackend && configuredPath == null && backend != "cpu")
                    {
                        SharpAI.Logging.JsonLog.Warn(logging, $"[NativeBackendInitializer] {backend} initialization failed, trying CPU: {ex}");
                        ConfigureBackend("cpu", settings, logging);
                    }
                    _IsInitialized = true;
                }
                catch (Exception ex)
                {
                    _InitializationFailure = ex;
                    _SelectedBackend = "unknown";
                    NativeBackendInfo.SelectedBackend = "unknown";
                    throw;
                }
            }
        }

        private static void ConfigureBackend(string backend, NativeBackendOptions settings, LoggingModule logging, string libraryPath = null, bool preserveConfiguration = false)
        {
            libraryPath = libraryPath ?? GetLibraryPath(backend, settings, logging);
            if (String.IsNullOrEmpty(libraryPath) || !File.Exists(libraryPath))
                throw new FileNotFoundException($"The {backend} backend library was not found.", libraryPath);
            string directory = Path.GetDirectoryName(libraryPath);
            OSPlatform platform = GetCurrentPlatform();
            if (platform == OSPlatform.Linux) PreLoadLinuxDependencies(directory, logging);
            else if (platform == OSPlatform.OSX) PreLoadMacOSDependencies(directory, logging);
            if (!preserveConfiguration) NativeLibraryLoader.Configure(libraryPath, backend);
            long deviceCount = NativeApi.llama_max_devices();
            _SelectedBackend = backend;
            ConfigureNativeLogging(settings, logging);
            SharpAI.Logging.JsonLog.Info(logging, $"[NativeBackendInitializer] configured {backend} backend: {libraryPath}; native device capacity={deviceCount}");
        }

        private static void PreLoadLinuxDependencies(string libraryDir, LoggingModule logging)
        {
            if (GetCurrentPlatform() != OSPlatform.Linux) return;

            SharpAI.Logging.JsonLog.Debug(logging, $"[NativeBackendInitializer] pre-loading Linux dependencies from: {libraryDir}");

            // List of dependencies in load order (base libraries first)
            string[] dependencies = new string[]
            {
                "libggml-base.so",
                "libggml-cpu.so",
                "libggml-cuda.so",
                "libggml.so",
                "libmtmd.so"
            };

            foreach (string dep in dependencies)
            {
                string depPath = Path.Combine(libraryDir, dep);
                if (File.Exists(depPath))
                {
                    try
                    {
                        IntPtr handle = NativeLibrary.Load(depPath);
                        SharpAI.Logging.JsonLog.Debug(logging, $"[NativeBackendInitializer] pre-loaded dependency: {dep}");
                    }
                    catch (Exception ex)
                    {
                        SharpAI.Logging.JsonLog.Warn(logging, $"[NativeBackendInitializer] failed to pre-load {dep}:" + Environment.NewLine + ex.ToString());
                    }
                }
                else
                {
                    SharpAI.Logging.JsonLog.Debug(logging, $"[NativeBackendInitializer] dependency not found: {depPath}");
                }
            }
        }

        private static void PreLoadMacOSDependencies(string libraryDir, LoggingModule logging)
        {
            if (GetCurrentPlatform() != OSPlatform.OSX) return;

            SharpAI.Logging.JsonLog.Debug(logging, $"[NativeBackendInitializer] pre-loading macOS dependencies from: {libraryDir}");

            // List of dependencies in load order.
            // libggml.dylib depends on libggml-blas.dylib and libggml-metal.dylib,
            // so those must be loaded first.
            string[] dependencies = new string[]
            {
                "libggml-base.dylib",
                "libggml-cpu.dylib",
                "libggml-blas.dylib",
                "libggml-metal.dylib",
                "libggml.dylib"
            };

            foreach (string dep in dependencies)
            {
                string depPath = Path.Combine(libraryDir, dep);
                if (File.Exists(depPath))
                {
                    try
                    {
                        IntPtr handle = NativeLibrary.Load(depPath);
                        SharpAI.Logging.JsonLog.Debug(logging, $"[NativeBackendInitializer] pre-loaded dependency: {dep}");
                    }
                    catch (Exception ex)
                    {
                        SharpAI.Logging.JsonLog.Warn(logging, $"[NativeBackendInitializer] failed to pre-load {dep}:" + Environment.NewLine + ex.ToString());
                    }
                }
                else
                {
                    SharpAI.Logging.JsonLog.Debug(logging, $"[NativeBackendInitializer] dependency not found: {depPath}");
                }
            }
        }

        private static bool DetectMetalAvailability(string baseDirectory)
        {
            foreach (string directory in new[]
            {
                Path.Combine(baseDirectory, "runtimes", "osx-arm64", "native"),
                Path.Combine(baseDirectory, "runtimes", "metal")
            })
            {
                if (File.Exists(Path.Combine(directory, "libllama.dylib")) && File.Exists(Path.Combine(directory, "libggml-metal.dylib"))) return true;
            }
            return false;
        }

        private static void ConfigureNativeLogging(NativeBackendOptions settings, LoggingModule logging)
        {
            bool enableLogging = SharpAIEnvironment.GetBool(
                SharpAIEnvironment.EnableNativeLogging,
                settings.EnableNativeLogging);

            try
            {
                if (enableLogging)
                {
                    // Redirect native logging to our logging module
                    NativeLogConfig.LLamaLogCallback logCallback = (LLamaLogLevel level, string message) =>
                    {
                        if (String.IsNullOrEmpty(message)) return;
                        string trimmed = message.TrimEnd('\r', '\n', ' ');
                        if (String.IsNullOrEmpty(trimmed)) return;

                        // Skip progress indicator dots and other noise
                        if (trimmed == "." || trimmed == ".." || trimmed == "..." || trimmed.All(c => c == '.'))
                            return;

                        switch (level)
                        {
                            case LLamaLogLevel.Error:
                                SharpAI.Logging.JsonLog.Warn(logging, "[llama.cpp] " + trimmed);
                                break;
                            case LLamaLogLevel.Warning:
                                SharpAI.Logging.JsonLog.Warn(logging, "[llama.cpp] " + trimmed);
                                break;
                            case LLamaLogLevel.Info:
                                SharpAI.Logging.JsonLog.Info(logging, "[llama.cpp] " + trimmed);
                                break;
                            default:
                                SharpAI.Logging.JsonLog.Debug(logging, "[llama.cpp] " + trimmed);
                                break;
                        }
                    };

                    NativeLogConfig.llama_log_set(logCallback);
                    SharpAI.Logging.JsonLog.Debug(logging, "[NativeBackendInitializer] native library logging enabled and redirected to log file");
                }
                else
                {
                    // Disable native logging by setting a no-op callback that discards all messages
                    NativeLogConfig.LLamaLogCallback noOpCallback = (LLamaLogLevel level, string message) =>
                    {
                        // Discard all log messages by doing nothing
                    };

                    NativeLogConfig.llama_log_set(noOpCallback);
                    SharpAI.Logging.JsonLog.Debug(logging, "[NativeBackendInitializer] native library logging disabled");
                }
            }
            catch (Exception ex)
            {
                SharpAI.Logging.JsonLog.Debug(logging, $"[NativeBackendInitializer] failed to configure native logging:{Environment.NewLine}{ex.ToString()}");
            }
        }

        private static string DetermineBackend(NativeBackendOptions settings, LoggingModule logging)
        {
            // Check for environment variable override (highest priority)
            string envBackend = SharpAIEnvironment.GetString(SharpAIEnvironment.ForceBackend);
            if (!String.IsNullOrEmpty(envBackend))
            {
                string forced = envBackend.ToLowerInvariant();
                if (!forced.Equals("auto", StringComparison.OrdinalIgnoreCase))
                {
                    if (IsValidBackend(forced))
                    {
                        SharpAI.Logging.JsonLog.Info(logging, $"[NativeBackendInitializer] backend forced by environment variable to: {forced}");
                        return forced;
                    }

                    SharpAI.Logging.JsonLog.Warn(logging, $"[NativeBackendInitializer] ignoring invalid {SharpAIEnvironment.ForceBackend} value: {envBackend}");
                }
            }

            // Check for forced backend setting
            if (!String.IsNullOrEmpty(settings.ForceBackend))
            {
                string forced = settings.ForceBackend.ToLowerInvariant();
                if (!forced.Equals("auto", StringComparison.OrdinalIgnoreCase))
                {
                    if (IsValidBackend(forced))
                    {
                        SharpAI.Logging.JsonLog.Info(logging, $"[NativeBackendInitializer] backend forced by settings to: {forced}");
                        return forced;
                    }

                    SharpAI.Logging.JsonLog.Warn(logging, $"[NativeBackendInitializer] ignoring invalid Runtime.ForceBackend value: {settings.ForceBackend}");
                }
            }

            OSPlatform platform = GetCurrentPlatform();
            Architecture architecture = RuntimeInformation.ProcessArchitecture;

            SharpAI.Logging.JsonLog.Debug(logging, $"[NativeBackendInitializer] detected platform {platform} architecture {architecture}");

            bool gpuAvailable = platform != OSPlatform.OSX && DetectGpuAvailability(platform, logging);
            string selected = SelectAutomaticBackend(platform, architecture, AppContext.BaseDirectory, gpuAvailable);
            SharpAI.Logging.JsonLog.Info(logging, $"[NativeBackendInitializer] automatic backend selection: {selected}");
            return selected;
        }

        internal static string SelectAutomaticBackend(OSPlatform platform, Architecture architecture, string baseDirectory, bool cudaAvailable)
        {
            if (platform == OSPlatform.OSX)
                return architecture == Architecture.Arm64 && DetectMetalAvailability(baseDirectory) ? "metal" : "cpu";
            return cudaAvailable ? "cuda" : "cpu";
        }

        private static bool DetectGpuAvailability(OSPlatform platform, LoggingModule logging)
        {
            // Method 1: Check for NVIDIA driver file (Linux)
            if (platform == OSPlatform.Linux)
            {
                if (File.Exists("/proc/driver/nvidia/version"))
                {
                    SharpAI.Logging.JsonLog.Debug(logging, "[NativeBackendInitializer] NVIDIA driver detected via /proc/driver/nvidia/version");
                    return true;
                }
            }

            // Method 2: Check environment variable set by NVIDIA Docker runtime
            string nvidiaVisible = Environment.GetEnvironmentVariable("NVIDIA_VISIBLE_DEVICES");
            if (!String.IsNullOrEmpty(nvidiaVisible)
                && !nvidiaVisible.Equals("void", StringComparison.OrdinalIgnoreCase)
                && !nvidiaVisible.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                SharpAI.Logging.JsonLog.Debug(logging, $"[NativeBackendInitializer] NVIDIA_VISIBLE_DEVICES detected: {nvidiaVisible}");
                return true;
            }

            // Method 3: Try executing nvidia-smi
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = platform == OSPlatform.Windows ? "nvidia-smi.exe" : "nvidia-smi",
                    Arguments = "--query-gpu=name --format=csv,noheader",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(psi))
                {
                    if (process != null)
                    {
                        string output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit(5000);

                        if (process.ExitCode == 0 && !String.IsNullOrWhiteSpace(output))
                        {
                            SharpAI.Logging.JsonLog.Debug(logging, $"[NativeBackendInitializer] nvidia-smi detected GPU: {output.Trim()}");
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SharpAI.Logging.JsonLog.Debug(logging, $"[NativeBackendInitializer] nvidia-smi check failed: {ex.Message}");
            }

            // Method 4: Check for CUDA library files
            if (platform == OSPlatform.Linux)
            {
                string[] cudaPaths = new string[]
                {
                    "/usr/lib/x86_64-linux-gnu/libcuda.so.1",
                    "/usr/lib64/libcuda.so.1",
                    "/usr/local/cuda/lib64/libcuda.so.1"
                };

                foreach (string path in cudaPaths)
                {
                    if (File.Exists(path))
                    {
                        SharpAI.Logging.JsonLog.Debug(logging, $"[NativeBackendInitializer] CUDA library detected: {path}");
                        return true;
                    }
                }
            }
            else if (platform == OSPlatform.Windows)
            {
                string systemRoot = Environment.GetEnvironmentVariable("SystemRoot");
                if (!String.IsNullOrEmpty(systemRoot))
                {
                    string cudaDll = Path.Combine(systemRoot, "System32", "nvcuda.dll");
                    if (File.Exists(cudaDll))
                    {
                        SharpAI.Logging.JsonLog.Debug(logging, $"[NativeBackendInitializer] CUDA library detected: {cudaDll}");
                        return true;
                    }
                }
            }

            SharpAI.Logging.JsonLog.Debug(logging, "[NativeBackendInitializer] no GPU detected via any method");
            return false;
        }

        private static string GetLibraryPath(string backend, NativeBackendOptions settings, LoggingModule logging)
        {
            // Check for explicit path in settings
            if (backend.Equals("cpu", StringComparison.OrdinalIgnoreCase))
            {
                if (!String.IsNullOrEmpty(settings.CpuBackendPath))
                {
                    string expandedPath = Environment.ExpandEnvironmentVariables(settings.CpuBackendPath);
                    SharpAI.Logging.JsonLog.Debug(logging, $"[NativeBackendInitializer] using CPU backend path from settings: {expandedPath}");
                    return expandedPath;
                }
            }
            else if (backend.Equals("cuda", StringComparison.OrdinalIgnoreCase))
            {
                if (!String.IsNullOrEmpty(settings.GpuBackendPath))
                {
                    string expandedPath = Environment.ExpandEnvironmentVariables(settings.GpuBackendPath);
                    SharpAI.Logging.JsonLog.Debug(logging, $"[NativeBackendInitializer] using GPU backend path from settings: {expandedPath}");
                    return expandedPath;
                }
            }
            else if (backend.Equals("metal", StringComparison.OrdinalIgnoreCase))
            {
                if (!String.IsNullOrEmpty(settings.MetalBackendPath))
                {
                    string expandedPath = Environment.ExpandEnvironmentVariables(settings.MetalBackendPath);
                    SharpAI.Logging.JsonLog.Debug(logging, $"[NativeBackendInitializer] using Metal backend path from settings: {expandedPath}");
                    return expandedPath;
                }
            }

            OSPlatform platform = GetCurrentPlatform();
            string baseDirectory = AppContext.BaseDirectory;
            string libraryName = GetNativeLibraryName(platform);

            // Try custom Docker structure first (for containers)
            string customPath = Path.Combine(baseDirectory, "runtimes", backend, libraryName);
            if (File.Exists(customPath))
            {
                SharpAI.Logging.JsonLog.Debug(logging, $"[NativeBackendInitializer] found library at custom path: {customPath}");
                return customPath;
            }

            // Try standard NuGet runtime structure (for local development)
            string nugetPath = GetNuGetRuntimePath(backend, platform, baseDirectory, libraryName, logging);
            if (!String.IsNullOrEmpty(nugetPath) && File.Exists(nugetPath))
            {
                SharpAI.Logging.JsonLog.Debug(logging, $"[NativeBackendInitializer] found library at NuGet path: {nugetPath}");
                return nugetPath;
            }

            // Return custom path as fallback (will log file not found later)
            SharpAI.Logging.JsonLog.Debug(logging, $"[NativeBackendInitializer] library not found, returning default path: {customPath}");
            return customPath;
        }

        private static string GetNuGetRuntimePath(
            string backend, 
            OSPlatform platform, 
            string baseDirectory, 
            string libraryName, 
            LoggingModule logging)
        {
            string rid = GetRuntimeIdentifier(platform);
            Architecture arch = RuntimeInformation.ProcessArchitecture;

            if (backend.Equals("cuda", StringComparison.OrdinalIgnoreCase))
            {
                // CUDA backend: try cuda12 subdirectory first
                string cudaPath = Path.Combine(baseDirectory, "runtimes", rid, "native", "cuda12", libraryName);
                if (File.Exists(cudaPath))
                {
                    return cudaPath;
                }

                // Try without subdirectory
                cudaPath = Path.Combine(baseDirectory, "runtimes", rid, "native", libraryName);
                if (File.Exists(cudaPath))
                {
                    return cudaPath;
                }
            }
            else if (backend.Equals("metal", StringComparison.OrdinalIgnoreCase))
            {
                // Metal uses the same libllama.dylib as CPU on osx-arm64.
                // GPU offload is activated by requesting GpuLayerCount > 0 at model load time.
                string metalPath = Path.Combine(baseDirectory, "runtimes", rid, "native", libraryName);
                if (File.Exists(metalPath))
                {
                    SharpAI.Logging.JsonLog.Debug(logging, "[NativeBackendInitializer] using Metal backend (osx-arm64 native)");
                    return metalPath;
                }
            }
            else if (backend.Equals("cpu", StringComparison.OrdinalIgnoreCase))
            {
                // For ARM64 (Apple Silicon, ARM Linux), no AVX variants exist
                if (arch == Architecture.Arm64)
                {
                    // Try direct path first
                    string armPath = Path.Combine(baseDirectory, "runtimes", rid, "native", libraryName);
                    if (File.Exists(armPath))
                    {
                        SharpAI.Logging.JsonLog.Debug(logging, "[NativeBackendInitializer] using ARM64 CPU backend");
                        return armPath;
                    }
                }
                else
                {
                    // For x64, select the fastest variant supported by the current host CPU.
                    string[] avxVariants = GetCpuVariantOrder(logging);
                    foreach (string variant in avxVariants)
                    {
                        string avxPath = Path.Combine(baseDirectory, "runtimes", rid, "native", variant, libraryName);
                        if (File.Exists(avxPath))
                        {
                            SharpAI.Logging.JsonLog.Debug(logging, $"[NativeBackendInitializer] using {variant} CPU variant");
                            return avxPath;
                        }
                    }

                    // Try without subdirectory as fallback
                    string fallbackPath = Path.Combine(baseDirectory, "runtimes", rid, "native", libraryName);
                    if (File.Exists(fallbackPath))
                    {
                        return fallbackPath;
                    }
                }
            }

            return null;
        }

        private static bool IsValidBackend(string backend)
        {
            if (String.IsNullOrWhiteSpace(backend)) return false;

            return backend.Equals("cpu", StringComparison.OrdinalIgnoreCase)
                || backend.Equals("cuda", StringComparison.OrdinalIgnoreCase)
                || backend.Equals("metal", StringComparison.OrdinalIgnoreCase);
        }

        private static string[] GetCpuVariantOrder(LoggingModule logging)
        {
            string requested = SharpAIEnvironment.GetString(SharpAIEnvironment.CpuVariant);
            string[] automatic = GetAutomaticCpuVariantOrder();

            if (!String.IsNullOrEmpty(requested) && !requested.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                string normalized = NormalizeCpuVariant(requested);
                if (String.IsNullOrEmpty(normalized))
                {
                    SharpAI.Logging.JsonLog.Warn(logging, $"[NativeBackendInitializer] ignoring invalid {SharpAIEnvironment.CpuVariant} value: {requested}");
                    return automatic;
                }

                if (!IsCpuVariantSupported(normalized))
                {
                    SharpAI.Logging.JsonLog.Warn(logging, $"[NativeBackendInitializer] requested CPU variant '{normalized}' is not supported by this CPU, using automatic selection");
                    return automatic;
                }

                SharpAI.Logging.JsonLog.Debug(logging, $"[NativeBackendInitializer] CPU variant forced by environment variable to: {normalized}");
                return MoveVariantFirst(automatic, normalized);
            }

            SharpAI.Logging.JsonLog.Debug(logging, "[NativeBackendInitializer] CPU variant order: " + String.Join(", ", automatic));
            return automatic;
        }

        private static string[] GetAutomaticCpuVariantOrder()
        {
            List<string> variants = new List<string>();

            if (IsCpuVariantSupported("avx512")) variants.Add("avx512");
            if (IsCpuVariantSupported("avx2")) variants.Add("avx2");
            if (IsCpuVariantSupported("avx")) variants.Add("avx");

            variants.Add("noavx");
            return variants.ToArray();
        }

        private static string NormalizeCpuVariant(string variant)
        {
            if (String.IsNullOrWhiteSpace(variant)) return null;

            string normalized = variant.Trim().ToLowerInvariant().Replace("-", "");
            if (normalized.Equals("auto", StringComparison.OrdinalIgnoreCase)) return "auto";
            if (normalized.Equals("avx512", StringComparison.OrdinalIgnoreCase)) return "avx512";
            if (normalized.Equals("avx2", StringComparison.OrdinalIgnoreCase)) return "avx2";
            if (normalized.Equals("avx", StringComparison.OrdinalIgnoreCase)) return "avx";
            if (normalized.Equals("noavx", StringComparison.OrdinalIgnoreCase)) return "noavx";

            return null;
        }

        private static bool IsCpuVariantSupported(string variant)
        {
            if (String.IsNullOrWhiteSpace(variant)) return false;

            if (variant.Equals("noavx", StringComparison.OrdinalIgnoreCase)) return true;
            if (variant.Equals("avx", StringComparison.OrdinalIgnoreCase)) return Avx.IsSupported || CpuFeatureFlagPresent("avx");
            if (variant.Equals("avx2", StringComparison.OrdinalIgnoreCase)) return Avx2.IsSupported || CpuFeatureFlagPresent("avx2");
            if (variant.Equals("avx512", StringComparison.OrdinalIgnoreCase)) return CpuFeatureFlagPresent("avx512f");

            return false;
        }

        private static bool CpuFeatureFlagPresent(string flag)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && File.Exists("/proc/cpuinfo"))
                {
                    string cpuInfo = File.ReadAllText("/proc/cpuinfo");
                    return cpuInfo.IndexOf(flag, StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch
            {
            }

            return false;
        }

        private static string[] MoveVariantFirst(string[] variants, string requested)
        {
            List<string> ordered = new List<string>();
            ordered.Add(requested);

            foreach (string variant in variants)
            {
                if (!variant.Equals(requested, StringComparison.OrdinalIgnoreCase))
                {
                    ordered.Add(variant);
                }
            }

            return ordered.ToArray();
        }

        private static string GetRuntimeIdentifier(OSPlatform platform)
        {
            Architecture arch = RuntimeInformation.ProcessArchitecture;

            if (platform == OSPlatform.Windows)
            {
                return arch == Architecture.X64 ? "win-x64" : "win-arm64";
            }
            else if (platform == OSPlatform.Linux)
            {
                return arch == Architecture.X64 ? "linux-x64" : "linux-arm64";
            }
            else if (platform == OSPlatform.OSX)
            {
                return arch == Architecture.X64 ? "osx-x64" : "osx-arm64";
            }

            return "unknown";
        }

        private static string GetNativeLibraryName(OSPlatform platform)
        {
            if (platform == OSPlatform.Windows)
            {
                return "llama.dll";
            }
            else if (platform == OSPlatform.Linux)
            {
                return "libllama.so";
            }
            else if (platform == OSPlatform.OSX)
            {
                return "libllama.dylib";
            }
            else
            {
                return "libllama.so"; // fallback
            }
        }

        private static OSPlatform GetCurrentPlatform()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return OSPlatform.Windows;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return OSPlatform.Linux;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return OSPlatform.OSX;
            }
            else
            {
                return OSPlatform.Linux; // fallback
            }
        }
    }
}
