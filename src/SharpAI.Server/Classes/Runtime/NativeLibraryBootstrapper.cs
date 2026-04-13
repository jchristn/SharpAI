namespace SharpAI.Server.Classes.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Intrinsics.X86;
    using LLama.Native;
    using SharpAI.Classes.Runtime;
    using SharpAI.Server.Classes.Settings;
    using SyslogLogging;

    /// <summary>
    /// Native library bootstrapper for LlamaSharp.
    /// Detects GPU availability and configures the appropriate native library before LlamaSharp static initialization.
    /// </summary>
    public static class NativeLibraryBootstrapper
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

        private static bool _IsInitialized = false;
        private static string _SelectedBackend = "unknown";
        private static readonly object _InitializationLock = new object();

        /// <summary>
        /// Initialize the native library configuration.
        /// This MUST be called before any LlamaSharp types are referenced.
        /// </summary>
        /// <param name="settings">Server settings containing backend configuration.</param>
        /// <param name="logging">Logging module.</param>
        public static void Initialize(Settings settings, LoggingModule logging)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (logging == null) throw new ArgumentNullException(nameof(logging));

            lock (_InitializationLock)
            {
                if (_IsInitialized)
                {
                    logging.Debug("[NativeLibraryBootstrapper] already initialized, skipping");
                    return;
                }

                string backend = DetermineBackend(settings, logging);
                _SelectedBackend = backend;
                NativeBackendInfo.SelectedBackend = backend;

                string libraryPath = GetLibraryPath(backend, settings, logging);
                bool requireBackend = SharpAIEnvironment.GetBool(SharpAIEnvironment.RequireBackend, false);

                if (!String.IsNullOrEmpty(libraryPath))
                {
                    if (File.Exists(libraryPath))
                    {
                        logging.Info($"[NativeLibraryBootstrapper] configuring {backend} backend: {libraryPath}");

                        OSPlatform currentPlatform = GetCurrentPlatform();

                        try
                        {
                            // Pre-load platform-specific dependencies BEFORE configuring library
                            string libraryDir = Path.GetDirectoryName(libraryPath);

                            if (currentPlatform == OSPlatform.Linux)
                            {
                                PreLoadLinuxDependencies(libraryDir, logging);
                            }
                            else if (currentPlatform == OSPlatform.OSX)
                            {
                                PreLoadMacOSDependencies(libraryDir, logging);
                            }

                            // CRITICAL: Configure library path BEFORE any LlamaSharp types are referenced
                            NativeLibraryConfig
                                .All
                                .WithLibrary(libraryPath, "llama");

                            logging.Info($"[NativeLibraryBootstrapper] successfully configured {backend} backend");

                            // Force NativeApi static constructor to run now (while we can catch errors)
                            // This ensures the library actually loads successfully
                            try
                            {
                                long deviceCount = NativeApi.llama_max_devices();
                                logging.Debug($"[NativeLibraryBootstrapper] library loaded successfully, {deviceCount} device(s) reported");
                            }
                            catch (Exception initEx)
                            {
                                throw new Exception($"Library configured but failed to load: {initEx.Message}" + Environment.NewLine + initEx.ToString(), initEx);
                            }

                            // Now safe to configure logging (after library is loaded)
                            ConfigureNativeLogging(settings, logging);
                        }
                        catch (Exception ex)
                        {
                            logging.Warn($"[NativeLibraryBootstrapper] failed to configure {backend} backend, will attempt fallback: {ex.Message}" + Environment.NewLine + ex.ToString());

                            if (requireBackend)
                            {
                                throw;
                            }

                            // Try CPU fallback
                            if (!backend.Equals("cpu", StringComparison.OrdinalIgnoreCase))
                            {
                                string cpuPath = GetLibraryPath("cpu", settings, logging);
                                if (!String.IsNullOrEmpty(cpuPath) && File.Exists(cpuPath))
                                {
                                    logging.Info($"[NativeLibraryBootstrapper] attempting CPU fallback: {cpuPath}");

                                    try
                                    {
                                        // Pre-load platform-specific dependencies BEFORE configuring library
                                        string cpuDir = Path.GetDirectoryName(cpuPath);

                                        if (currentPlatform == OSPlatform.Linux)
                                        {
                                            PreLoadLinuxDependencies(cpuDir, logging);
                                        }
                                        else if (currentPlatform == OSPlatform.OSX)
                                        {
                                            PreLoadMacOSDependencies(cpuDir, logging);
                                        }

                                        NativeLibraryConfig
                                            .All
                                            .WithLibrary(cpuPath, "llama");

                                        _SelectedBackend = "cpu";
                                        NativeBackendInfo.SelectedBackend = "cpu";
                                        logging.Info("[NativeLibraryBootstrapper] successfully configured CPU backend as fallback");

                                        // Force library load
                                        try
                                        {
                                            long deviceCount = NativeApi.llama_max_devices();
                                            logging.Debug($"[NativeLibraryBootstrapper] fallback library loaded successfully, {deviceCount} device(s) reported");
                                        }
                                        catch (Exception initEx)
                                        {
                                            throw new Exception($"Fallback library configured but failed to load: {initEx.Message}" + Environment.NewLine + initEx.ToString(), initEx);
                                        }

                                        // Configure logging after library is loaded
                                        ConfigureNativeLogging(settings, logging);
                                    }
                                    catch (Exception fallbackEx)
                                    {
                                        logging.Warn($"[NativeLibraryBootstrapper] CPU fallback also failed: {fallbackEx.Message}" + Environment.NewLine + fallbackEx.ToString());
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        logging.Warn($"[NativeLibraryBootstrapper] library file not found: {libraryPath}");
                        if (requireBackend)
                        {
                            throw new FileNotFoundException($"Required {backend} backend library was not found.", libraryPath);
                        }
                    }
                }
                else
                {
                    logging.Debug("[NativeLibraryBootstrapper] no explicit library path configured, using default LlamaSharp library loading");
                }

                _IsInitialized = true;
            }
        }

        private static void PreLoadLinuxDependencies(string libraryDir, LoggingModule logging)
        {
            if (GetCurrentPlatform() != OSPlatform.Linux) return;

            logging.Debug($"[NativeLibraryBootstrapper] pre-loading Linux dependencies from: {libraryDir}");

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
                        logging.Debug($"[NativeLibraryBootstrapper] pre-loaded dependency: {dep}");
                    }
                    catch (Exception ex)
                    {
                        logging.Warn($"[NativeLibraryBootstrapper] failed to pre-load {dep}:" + Environment.NewLine + ex.ToString());
                    }
                }
                else
                {
                    logging.Debug($"[NativeLibraryBootstrapper] dependency not found: {depPath}");
                }
            }
        }

        private static void PreLoadMacOSDependencies(string libraryDir, LoggingModule logging)
        {
            if (GetCurrentPlatform() != OSPlatform.OSX) return;

            logging.Debug($"[NativeLibraryBootstrapper] pre-loading macOS dependencies from: {libraryDir}");

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
                        logging.Debug($"[NativeLibraryBootstrapper] pre-loaded dependency: {dep}");
                    }
                    catch (Exception ex)
                    {
                        logging.Warn($"[NativeLibraryBootstrapper] failed to pre-load {dep}:" + Environment.NewLine + ex.ToString());
                    }
                }
                else
                {
                    logging.Debug($"[NativeLibraryBootstrapper] dependency not found: {depPath}");
                }
            }
        }

        private static bool DetectMetalAvailability(LoggingModule logging)
        {
            try
            {
                string baseDirectory = AppContext.BaseDirectory;
                string rid = GetRuntimeIdentifier(OSPlatform.OSX);
                string nativeDir = Path.Combine(baseDirectory, "runtimes", rid, "native");
                string metalLib = Path.Combine(nativeDir, "libggml-metal.dylib");

                if (File.Exists(metalLib))
                {
                    logging.Debug($"[NativeLibraryBootstrapper] Metal library detected: {metalLib}");
                    return true;
                }

                // Try custom Docker structure
                string customMetalLib = Path.Combine(baseDirectory, "runtimes", "metal", "libggml-metal.dylib");
                if (File.Exists(customMetalLib))
                {
                    logging.Debug($"[NativeLibraryBootstrapper] Metal library detected at custom path: {customMetalLib}");
                    return true;
                }

                logging.Debug("[NativeLibraryBootstrapper] Metal library not found");
                return false;
            }
            catch (Exception ex)
            {
                logging.Debug($"[NativeLibraryBootstrapper] Metal detection failed: {ex.Message}");
                return false;
            }
        }

        private static void ConfigureNativeLogging(Settings settings, LoggingModule logging)
        {
            bool enableLogging = SharpAIEnvironment.GetBool(
                SharpAIEnvironment.EnableNativeLogging,
                settings.Runtime?.EnableNativeLogging ?? false);

            try
            {
                if (enableLogging)
                {
                    // Redirect native logging to our logging module
                    NativeLogConfig.LLamaLogCallback logCallback = (LLamaLogLevel level, string message) =>
                    {
                        if (String.IsNullOrEmpty(message)) return;
                        string trimmed = message.TrimEnd('\r', '\n');
                        if (String.IsNullOrEmpty(trimmed)) return;

                        switch (level)
                        {
                            case LLamaLogLevel.Error:
                                logging.Warn("[llama.cpp] " + trimmed);
                                break;
                            case LLamaLogLevel.Warning:
                                logging.Warn("[llama.cpp] " + trimmed);
                                break;
                            case LLamaLogLevel.Info:
                                logging.Info("[llama.cpp] " + trimmed);
                                break;
                            default:
                                logging.Debug("[llama.cpp] " + trimmed);
                                break;
                        }
                    };

                    NativeLogConfig.llama_log_set(logCallback);
                    logging.Debug("[NativeLibraryBootstrapper] native library logging enabled and redirected to log file");
                }
                else
                {
                    // Disable native logging by setting a no-op callback that discards all messages
                    NativeLogConfig.LLamaLogCallback noOpCallback = (LLamaLogLevel level, string message) =>
                    {
                        // Discard all log messages by doing nothing
                    };

                    NativeLogConfig.llama_log_set(noOpCallback);
                    logging.Debug("[NativeLibraryBootstrapper] native library logging disabled");
                }
            }
            catch (Exception ex)
            {
                logging.Debug($"[NativeLibraryBootstrapper] failed to configure native logging:{Environment.NewLine}{ex.ToString()}");
            }
        }

        private static string DetermineBackend(Settings settings, LoggingModule logging)
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
                        logging.Info($"[NativeLibraryBootstrapper] backend forced by environment variable to: {forced}");
                        return forced;
                    }

                    logging.Warn($"[NativeLibraryBootstrapper] ignoring invalid {SharpAIEnvironment.ForceBackend} value: {envBackend}");
                }
            }

            // Check for forced backend setting
            if (!String.IsNullOrEmpty(settings.Runtime?.ForceBackend))
            {
                string forced = settings.Runtime.ForceBackend.ToLowerInvariant();
                if (!forced.Equals("auto", StringComparison.OrdinalIgnoreCase))
                {
                    if (IsValidBackend(forced))
                    {
                        logging.Info($"[NativeLibraryBootstrapper] backend forced by settings to: {forced}");
                        return forced;
                    }

                    logging.Warn($"[NativeLibraryBootstrapper] ignoring invalid Runtime.ForceBackend value: {settings.Runtime.ForceBackend}");
                }
            }

            OSPlatform platform = GetCurrentPlatform();
            Architecture architecture = RuntimeInformation.ProcessArchitecture;

            logging.Debug($"[NativeLibraryBootstrapper] detected platform {platform} architecture {architecture}");

            // Apple Silicon: check for Metal GPU acceleration
            if (platform == OSPlatform.OSX && architecture == Architecture.Arm64)
            {
                bool metalAvailable = DetectMetalAvailability(logging);
                if (metalAvailable)
                {
                    logging.Info("[NativeLibraryBootstrapper] Apple Silicon detected, selecting Metal backend");
                    return "metal";
                }
                else
                {
                    logging.Info("[NativeLibraryBootstrapper] Apple Silicon detected, Metal not available, using CPU");
                    return "cpu";
                }
            }

            // Check for GPU availability
            bool gpuAvailable = DetectGpuAvailability(platform, logging);

            if (gpuAvailable)
            {
                logging.Info("[NativeLibraryBootstrapper] GPU detected, selecting CUDA backend");
                return "cuda";
            }
            else
            {
                logging.Info("[NativeLibraryBootstrapper] no GPU detected, selecting CPU backend");
                return "cpu";
            }
        }

        private static bool DetectGpuAvailability(OSPlatform platform, LoggingModule logging)
        {
            // Method 1: Check for NVIDIA driver file (Linux)
            if (platform == OSPlatform.Linux)
            {
                if (File.Exists("/proc/driver/nvidia/version"))
                {
                    logging.Debug("[NativeLibraryBootstrapper] NVIDIA driver detected via /proc/driver/nvidia/version");
                    return true;
                }
            }

            // Method 2: Check environment variable set by NVIDIA Docker runtime
            string nvidiaVisible = Environment.GetEnvironmentVariable("NVIDIA_VISIBLE_DEVICES");
            if (!String.IsNullOrEmpty(nvidiaVisible)
                && !nvidiaVisible.Equals("void", StringComparison.OrdinalIgnoreCase)
                && !nvidiaVisible.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                logging.Debug($"[NativeLibraryBootstrapper] NVIDIA_VISIBLE_DEVICES detected: {nvidiaVisible}");
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
                            logging.Debug($"[NativeLibraryBootstrapper] nvidia-smi detected GPU: {output.Trim()}");
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logging.Debug($"[NativeLibraryBootstrapper] nvidia-smi check failed: {ex.Message}");
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
                        logging.Debug($"[NativeLibraryBootstrapper] CUDA library detected: {path}");
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
                        logging.Debug($"[NativeLibraryBootstrapper] CUDA library detected: {cudaDll}");
                        return true;
                    }
                }
            }

            logging.Debug("[NativeLibraryBootstrapper] no GPU detected via any method");
            return false;
        }

        private static string GetLibraryPath(string backend, Settings settings, LoggingModule logging)
        {
            // Check for explicit path in settings
            if (backend.Equals("cpu", StringComparison.OrdinalIgnoreCase))
            {
                if (!String.IsNullOrEmpty(settings.Runtime?.CpuBackendPath))
                {
                    string expandedPath = Environment.ExpandEnvironmentVariables(settings.Runtime.CpuBackendPath);
                    logging.Debug($"[NativeLibraryBootstrapper] using CPU backend path from settings: {expandedPath}");
                    return expandedPath;
                }
            }
            else if (backend.Equals("cuda", StringComparison.OrdinalIgnoreCase))
            {
                if (!String.IsNullOrEmpty(settings.Runtime?.GpuBackendPath))
                {
                    string expandedPath = Environment.ExpandEnvironmentVariables(settings.Runtime.GpuBackendPath);
                    logging.Debug($"[NativeLibraryBootstrapper] using GPU backend path from settings: {expandedPath}");
                    return expandedPath;
                }
            }
            else if (backend.Equals("metal", StringComparison.OrdinalIgnoreCase))
            {
                if (!String.IsNullOrEmpty(settings.Runtime?.MetalBackendPath))
                {
                    string expandedPath = Environment.ExpandEnvironmentVariables(settings.Runtime.MetalBackendPath);
                    logging.Debug($"[NativeLibraryBootstrapper] using Metal backend path from settings: {expandedPath}");
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
                logging.Debug($"[NativeLibraryBootstrapper] found library at custom path: {customPath}");
                return customPath;
            }

            // Try standard NuGet runtime structure (for local development)
            string nugetPath = GetNuGetRuntimePath(backend, platform, baseDirectory, libraryName, logging);
            if (!String.IsNullOrEmpty(nugetPath) && File.Exists(nugetPath))
            {
                logging.Debug($"[NativeLibraryBootstrapper] found library at NuGet path: {nugetPath}");
                return nugetPath;
            }

            // Return custom path as fallback (will log file not found later)
            logging.Debug($"[NativeLibraryBootstrapper] library not found, returning default path: {customPath}");
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
                    logging.Debug("[NativeLibraryBootstrapper] using Metal backend (osx-arm64 native)");
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
                        logging.Debug("[NativeLibraryBootstrapper] using ARM64 CPU backend");
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
                            logging.Debug($"[NativeLibraryBootstrapper] using {variant} CPU variant");
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
                    logging.Warn($"[NativeLibraryBootstrapper] ignoring invalid {SharpAIEnvironment.CpuVariant} value: {requested}");
                    return automatic;
                }

                if (!IsCpuVariantSupported(normalized))
                {
                    logging.Warn($"[NativeLibraryBootstrapper] requested CPU variant '{normalized}' is not supported by this CPU, using automatic selection");
                    return automatic;
                }

                logging.Debug($"[NativeLibraryBootstrapper] CPU variant forced by environment variable to: {normalized}");
                return MoveVariantFirst(automatic, normalized);
            }

            logging.Debug("[NativeLibraryBootstrapper] CPU variant order: " + String.Join(", ", automatic));
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
