namespace SharpAI.Services
{
    using System;
    using System.Threading.Tasks;
    using SharpAI.Engines;

    /// <summary>
    /// Cache entry for a loaded (or loading) model engine, tracking the deferred initialization task, the
    /// model file size used for memory-budget admission, and the last-access time used for idle eviction.
    /// </summary>
    internal sealed class ModelEngineEntry
    {
        /// <summary>
        /// Deferred, single-initialization task that produces the engine.
        /// </summary>
        public Lazy<Task<LlamaSharpEngine>> Lazy { get; }

        /// <summary>
        /// Size of the model file in bytes, used for memory-budget admission.
        /// </summary>
        public long FileSizeBytes { get; }
        /// <summary>Conservative host pool reservation.</summary>
        public long HostBytes { get { return _HostBytes; } }
        private readonly long _HostBytes;
        /// <summary>Conservative device pool reservation; overlaps total reservation.</summary>
        public long DeviceBytes { get { return _DeviceBytes; } }
        private readonly long _DeviceBytes;

        /// <summary>
        /// UTC time this entry was created.
        /// </summary>
        public DateTime LoadedUtc { get; }

        /// <summary>
        /// UTC time the engine was last handed to a caller. Updated on each successful acquisition and used
        /// to select idle and least-recently-used models for eviction.
        /// </summary>
        public DateTime LastAccessUtc { get; set; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="lazy">Deferred initialization task.</param>
        /// <param name="fileSizeBytes">Model file size in bytes.</param>
        /// <param name="nowUtc">Current UTC time.</param>
        /// <param name="hostBytes">Host memory reservation.</param>
        /// <param name="deviceBytes">Device memory reservation.</param>
        public ModelEngineEntry(Lazy<Task<LlamaSharpEngine>> lazy, long fileSizeBytes, DateTime nowUtc, long hostBytes = 0, long deviceBytes = 0)
        {
            Lazy = lazy ?? throw new ArgumentNullException(nameof(lazy));
            FileSizeBytes = fileSizeBytes;
            _HostBytes = hostBytes;
            _DeviceBytes = deviceBytes;
            LoadedUtc = nowUtc;
            LastAccessUtc = nowUtc;
        }
    }
}
