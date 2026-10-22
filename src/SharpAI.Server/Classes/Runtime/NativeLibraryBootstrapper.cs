namespace SharpAI.Server.Classes.Runtime
{
    using System;
    using SharpAI.Classes.Runtime;
    using SharpAI.Server.Classes.Settings;
    using SyslogLogging;

    /// <summary>Adapts server settings to the native initializer shared with standalone library consumers.</summary>
    public static class NativeLibraryBootstrapper
    {
        /// <summary>Whether the shared native initializer has completed.</summary>
        public static bool IsInitialized { get { return NativeBackendInitializer.IsInitialized; } }

        /// <summary>The backend selected by the shared native initializer.</summary>
        public static string SelectedBackend { get { return NativeBackendInitializer.SelectedBackend; } }

        /// <summary>Initialize native loading before the first native operation.</summary>
        /// <param name="settings">Non-null server settings.</param>
        /// <param name="logging">Non-null logging module.</param>
        /// <exception cref="ArgumentNullException">Settings or logging is null.</exception>
        /// <remarks>Thread-safe. The first initialization, from either the server or library, owns the process-wide selection.</remarks>
        public static void Initialize(Settings settings, LoggingModule logging)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(logging);
            RuntimeSettings runtime = settings.Runtime;
            NativeBackendInitializer.Initialize(new NativeBackendOptions
            {
                ForceBackend = runtime?.ForceBackend,
                CpuBackendPath = runtime?.CpuBackendPath,
                GpuBackendPath = runtime?.GpuBackendPath,
                MetalBackendPath = runtime?.MetalBackendPath,
                EnableNativeLogging = runtime?.EnableNativeLogging ?? false
            }, logging);
        }
    }
}
