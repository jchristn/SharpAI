namespace SharpAI.Server.Classes.Settings
{
    using System;

    /// <summary>
    /// Runtime settings for native library backend configuration.
    /// </summary>
    public class RuntimeSettings
    {
        #region Public-Members

        /// <summary>
        /// Force a specific backend.
        /// Valid values: "cpu", "cuda", "metal", or null for auto-detection.
        /// </summary>
        public string ForceBackend
        {
            get
            {
                return _ForceBackend;
            }
            set
            {
                _ForceBackend = value;
            }
        }

        /// <summary>
        /// Path to the CPU backend native library.
        /// Default: "./runtimes/cpu/libllama.so" (Linux), "./runtimes/cpu/llama.dll" (Windows), "./runtimes/cpu/libllama.dylib" (macOS).
        /// Supports environment variable expansion.
        /// </summary>
        public string CpuBackendPath
        {
            get
            {
                return _CpuBackendPath;
            }
            set
            {
                _CpuBackendPath = value;
            }
        }

        /// <summary>
        /// Path to the GPU backend native library.
        /// Default: "./runtimes/cuda/libllama.so" (Linux), "./runtimes/cuda/llama.dll" (Windows).
        /// Supports environment variable expansion.
        /// Note: On macOS Apple Silicon, use MetalBackendPath instead.
        /// </summary>
        public string GpuBackendPath
        {
            get
            {
                return _GpuBackendPath;
            }
            set
            {
                _GpuBackendPath = value;
            }
        }

        /// <summary>
        /// Path to the Metal backend native library.
        /// Default: "./runtimes/osx-arm64/native/libllama.dylib" (macOS Apple Silicon).
        /// Supports environment variable expansion.
        /// Note: Only available on macOS Apple Silicon (ARM64).
        /// </summary>
        public string MetalBackendPath
        {
            get
            {
                return _MetalBackendPath;
            }
            set
            {
                _MetalBackendPath = value;
            }
        }

        /// <summary>
        /// Enable or disable LlamaSharp native library logging to console.
        /// When disabled, llama.cpp log messages will not be written to console.
        /// Default: false (disabled).
        /// </summary>
        public bool EnableNativeLogging
        {
            get
            {
                return _EnableNativeLogging;
            }
            set
            {
                _EnableNativeLogging = value;
            }
        }

        #endregion

        #region Private-Members

        private string _ForceBackend = null;
        private string _CpuBackendPath = null;
        private string _GpuBackendPath = null;
        private string _MetalBackendPath = null;
        private bool _EnableNativeLogging = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RuntimeSettings()
        {
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
