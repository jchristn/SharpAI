namespace SharpAI.Classes.Runtime
{
    using System;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Provides information about the native backend that was selected during initialization.
    /// </summary>
    public static class NativeBackendInfo
    {
        private static readonly string _LlamaSharpVersion = typeof(NativeBackendInfo).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(attribute => attribute.Key == "LlamaSharpVersion")?.Value ?? "unknown";

        /// <summary>The pinned LlamaSharp package version, recorded by the build. The upstream assembly version is not its package version.</summary>
        public static string LlamaSharpVersion { get { return _LlamaSharpVersion; } }

        #region Public-Members

        /// <summary>
        /// Gets or sets the backend that was selected during initialization.
        /// </summary>
        public static string SelectedBackend
        {
            get
            {
                return _SelectedBackend;
            }
            set
            {
                _SelectedBackend = value;
            }
        }

        #endregion

        #region Private-Members

        private static string _SelectedBackend = "unknown";

        #endregion
    }
}
