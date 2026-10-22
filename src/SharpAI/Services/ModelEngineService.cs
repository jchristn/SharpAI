namespace SharpAI.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Classes.Runtime;
    using SharpAI.Engines;
    using SharpAI.Exceptions;
    using SharpAI.Operations;
    using SyslogLogging;

    /// <summary>
    /// Manages the lifecycle of loaded model engines. Each model is loaded at most once and cached;
    /// loading one model never blocks access to a different, already-loaded model. Admission enforces an
    /// optional resident-count cap and memory budget (evicting least-recently-used models to make room),
    /// and an optional keep-alive timeout evicts idle models. Thread-safe.
    /// </summary>
    public class ModelEngineService : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Idle seconds after which a loaded model is evicted. 0 disables idle eviction.
        /// </summary>
        public int KeepAliveSeconds
        {
            get { return _KeepAliveSeconds; }
        }

        /// <summary>
        /// Maximum number of models resident at once. 0 means unlimited.
        /// </summary>
        public int MaxResidentModels
        {
            get { return _MaxResidentModels; }
        }

        /// <summary>
        /// Total conservative peak model-memory budget in bytes (weights, KV, buffers, and headroom). 0 means unlimited.
        /// </summary>
        public long ModelMemoryBudgetBytes
        {
            get { return _ModelMemoryBudgetBytes; }
        }

        #endregion

        #region Private-Members

        private string _Header = "[ModelEngineService] ";
        private LoggingModule _Logging = null;
        private readonly ConcurrentDictionary<string, ModelEngineEntry> _Engines =
            new ConcurrentDictionary<string, ModelEngineEntry>();
        private readonly object _AdmissionLock = new object();

        private int _KeepAliveSeconds = 0;
        private int _MaxResidentModels = 0;
        private long _ModelMemoryBudgetBytes = 0;
        private long _HostBudgetBytes;
        private long _DeviceBudgetBytes;
        private Timer _SweepTimer = null;
        private volatile bool _Disposed = false;
        private Func<string, Task<LlamaSharpEngine>> _EngineFactory;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Model engine service.
        /// </summary>
        /// <param name="logging">Logging.</param>
        public ModelEngineService(LoggingModule logging)
        {
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));

            _KeepAliveSeconds = SharpAIEnvironment.GetInt(SharpAIEnvironment.KeepAliveSeconds, 0, 0);
            _MaxResidentModels = SharpAIEnvironment.GetInt(SharpAIEnvironment.MaxResidentModels, 0, 0);
            int budgetMb = SharpAIEnvironment.GetInt(SharpAIEnvironment.ModelMemoryBudgetMb, 0, 0);
            _ModelMemoryBudgetBytes = (long)budgetMb * 1024L * 1024L;

            _HostBudgetBytes = (long)SharpAIEnvironment.GetInt("SHARPAI_HOST_MEMORY_BUDGET_MB", 0, 0) * 1024 * 1024;
            _DeviceBudgetBytes = (long)SharpAIEnvironment.GetInt("SHARPAI_DEVICE_MEMORY_BUDGET_MB", 0, 0) * 1024 * 1024;
            SharpAI.Telemetry.SharpAITelemetry.SetResidentModelCountProvider(CountResidentModels);

            if (_KeepAliveSeconds > 0)
            {
                int intervalMs = Math.Min(_KeepAliveSeconds, 30) * 1000;
                _SweepTimer = new Timer(SweepIdleCallback, null, intervalMs, intervalMs);
            }

            SharpAI.Logging.JsonLog.Debug(_Logging, _Header + $"initialized (keepAlive={_KeepAliveSeconds}s, maxResident={_MaxResidentModels}, budget={budgetMb}MB)");
        }

        internal ModelEngineService(LoggingModule logging, Func<string, Task<LlamaSharpEngine>> engineFactory) : this(logging)
        {
            _EngineFactory = engineFactory ?? throw new ArgumentNullException(nameof(engineFactory));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Get a snapshot of the model file paths that currently have a live, non-disposed engine loaded.
        /// </summary>
        /// <returns>List of model file paths for currently loaded engines.</returns>
        public List<string> GetLoadedModelPaths()
        {
            List<string> loaded = new List<string>();

            foreach (KeyValuePair<string, ModelEngineEntry> kvp in _Engines)
            {
                LlamaSharpEngine engine = TryGetCompletedEngine(kvp.Value);
                if (engine != null && !engine.IsDisposed) loaded.Add(kvp.Key);
                else if (kvp.Value.Lazy.IsValueCreated && kvp.Value.Lazy.Value.IsCompleted && (engine == null || engine.IsDisposed)) RemoveEntryIfSame(kvp.Key, kvp.Value);
            }

            return loaded;
        }

        /// <summary>
        /// Get the engine for a given model file, loading and initializing it if necessary. This
        /// synchronous overload blocks the caller until the requested model is ready; prefer
        /// <see cref="GetByModelFileAsync"/> in async code.
        /// </summary>
        /// <param name="filename">Path and filename to the model.</param>
        /// <returns>Instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="filename"/> is null or empty.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the model file does not exist.</exception>
        /// <exception cref="ModelAdmissionException">Thrown when the model cannot be admitted within the memory budget.</exception>
        public LlamaSharpEngine GetByModelFile(string filename)
        {
            return GetByModelFileAsync(filename).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Get the engine for a given model file asynchronously, loading and initializing it if necessary.
        /// Concurrent callers for the same model share a single initialization; different models load
        /// concurrently. Admission may evict least-recently-used models to honor the configured caps.
        /// </summary>
        /// <param name="filename">Path and filename to the model.</param>
        /// <param name="token">Cancellation token that cancels the caller's wait (not the shared load).</param>
        /// <returns>Instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="filename"/> is null or empty.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the model file does not exist.</exception>
        /// <exception cref="ModelAdmissionException">Thrown when the model cannot be admitted within the memory budget.</exception>
        public async Task<LlamaSharpEngine> GetByModelFileAsync(string filename, CancellationToken token = default)
        {
            ObjectDisposedException.ThrowIf(_Disposed, this);
            if (String.IsNullOrEmpty(filename)) throw new ArgumentNullException(nameof(filename));
            filename = Path.GetFullPath(filename);
            if (!File.Exists(filename)) throw new FileNotFoundException("The specified file could not be found.", filename);

            while (true)
            {
                token.ThrowIfCancellationRequested();

                ModelEngineEntry entry = GetOrAdmit(filename);
                LlamaSharpEngine engine;

                try
                {
                    engine = await entry.Lazy.Value.WaitAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    RemoveEntryIfSame(filename, entry);
                    throw;
                }

                if (engine == null || engine.IsDisposed)
                {
                    RemoveEntryIfSame(filename, entry);
                    continue;
                }

                entry.LastAccessUtc = DateTime.UtcNow;
                return engine;
            }
        }

        /// <summary>
        /// Get the UTC time at which a loaded model will be evicted for inactivity, or null when idle
        /// eviction is disabled or the model is not loaded.
        /// </summary>
        /// <param name="filename">Path and filename to the model.</param>
        /// <returns>Eviction time in UTC, or null.</returns>
        public DateTime? GetExpiryUtc(string filename)
        {
            if (_KeepAliveSeconds <= 0 || String.IsNullOrEmpty(filename)) return null;
            filename = Path.GetFullPath(filename);
            if (_Engines.TryGetValue(filename, out ModelEngineEntry entry))
                return entry.LastAccessUtc.AddSeconds(_KeepAliveSeconds);
            return null;
        }

        /// <summary>
        /// Unload a model by its file path, disposing the engine and freeing GPU/CPU memory.
        /// </summary>
        /// <param name="filename">Path and filename to the model.</param>
        /// <returns>True if the model was found and unloaded, false if no engine was loaded for this path.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="filename"/> is null or empty.</exception>
        public bool UnloadModel(string filename)
        {
            if (String.IsNullOrEmpty(filename)) throw new ArgumentNullException(nameof(filename));

            filename = Path.GetFullPath(filename);
            if (_Engines.TryGetValue(filename, out ModelEngineEntry pending) && (!pending.Lazy.IsValueCreated || !pending.Lazy.Value.IsCompleted)) return false;
            if (_Engines.TryGetValue(filename, out ModelEngineEntry active) && TryGetCompletedEngine(active)?.ActiveGenerations > 0) return false;
            if (_Engines.TryRemove(filename, out ModelEngineEntry entry))
            {
                DisposeEntry(filename, entry);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Unload all currently loaded models, disposing all engines and freeing GPU/CPU memory.
        /// </summary>
        /// <returns>The number of models that were unloaded.</returns>
        public int UnloadAllModels()
        {
            int count = 0;

            foreach (string key in new List<string>(_Engines.Keys))
            {
                if (!_Disposed && _Engines.TryGetValue(key, out ModelEngineEntry pending) &&
                    (!pending.Lazy.IsValueCreated || !pending.Lazy.Value.IsCompleted || TryGetCompletedEngine(pending)?.ActiveGenerations > 0)) continue;
                if (_Engines.TryRemove(key, out ModelEngineEntry entry))
                {
                    if (DisposeEntry(key, entry)) count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Dispose the service, stopping the eviction sweep and unloading all models.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;

            try { _SweepTimer?.Dispose(); } catch { }
            _SweepTimer = null;

            UnloadAllModels();
        }

        #endregion

        #region Private-Methods

        private ModelEngineEntry GetOrAdmit(string filename)
        {
            lock (_AdmissionLock)
            {
                ObjectDisposedException.ThrowIf(_Disposed, this);
                if (_Engines.TryGetValue(filename, out ModelEngineEntry existing))
                {
                    existing.LastAccessUtc = DateTime.UtcNow;
                    return existing;
                }

                foreach (KeyValuePair<string, ModelEngineEntry> cached in _Engines)
                {
                    if (!cached.Value.Lazy.IsValueCreated || !cached.Value.Lazy.Value.IsCompleted) continue;
                    LlamaSharpEngine completed = TryGetCompletedEngine(cached.Value);
                    if (completed == null || completed.IsDisposed) RemoveEntryIfSame(cached.Key, cached.Value);
                }
                ModelInspection inspection = ModelInspector.Inspect(filename);
                if (!inspection.Supported) throw new ModelAdmissionException("Model contains unsupported components.");
                if ((_ModelMemoryBudgetBytes > 0 || _HostBudgetBytes > 0 || _DeviceBudgetBytes > 0) && !inspection.EstimateAvailable)
                    throw new ModelAdmissionException("Cannot admit model within a memory budget: " + inspection.Assumptions);
                long newBytes = inspection.EstimateAvailable ? inspection.TotalBytes : inspection.WeightsBytes;
                AdmitLocked(filename, newBytes);
                AdmitPoolLocked(inspection.HostBytes, _HostBudgetBytes, true);
                AdmitPoolLocked(inspection.DeviceBytes, _DeviceBudgetBytes, false);

                Lazy<Task<LlamaSharpEngine>> lazy = new Lazy<Task<LlamaSharpEngine>>(
                    () => CreateEngineAsync(filename), LazyThreadSafetyMode.ExecutionAndPublication);
                ModelEngineEntry entry = new ModelEngineEntry(lazy, newBytes, DateTime.UtcNow, inspection.HostBytes, inspection.DeviceBytes);
                _Engines[filename] = entry;
                return entry;
            }
        }

        private void AdmitLocked(string filename, long newBytes)
        {
            if (_MaxResidentModels > 0)
            {
                while (_Engines.Count >= _MaxResidentModels)
                {
                    if (!EvictOneLruLocked()) throw new ModelAdmissionException("Model capacity is reserved by loading models; retry after a load completes.");
                }
            }

            if (_ModelMemoryBudgetBytes > 0)
            {
                while (newBytes > _ModelMemoryBudgetBytes - ResidentBytesLocked())
                {
                    if (!EvictOneLruLocked()) break;
                }

                if (newBytes > _ModelMemoryBudgetBytes - ResidentBytesLocked())
                {
                    throw new ModelAdmissionException(
                        $"Model '{Path.GetFileName(filename)}' (~{newBytes / (1024L * 1024L)} MB) exceeds the configured model-memory budget " +
                        $"of {_ModelMemoryBudgetBytes / (1024L * 1024L)} MB and cannot be loaded.");
                }
            }
        }

        private void AdmitPoolLocked(long bytes, long budget, bool host)
        {
            if (budget == 0) return;
            while (true)
            {
                long reserved = 0;
                foreach (ModelEngineEntry entry in _Engines.Values) reserved = checked(reserved + (host ? entry.HostBytes : entry.DeviceBytes));
                if (bytes <= budget - reserved) return;
                if (!EvictOneLruLocked()) throw new ModelAdmissionException((host ? "Host" : "Device") + " memory budget is exhausted or reserved by loading models.");
            }
        }

        private long ResidentBytesLocked()
        {
            long total = 0;
            foreach (KeyValuePair<string, ModelEngineEntry> kvp in _Engines) total = checked(total + kvp.Value.FileSizeBytes);
            return total;
        }

        private bool EvictOneLruLocked()
        {
            string lruKey = null;
            ModelEngineEntry lruEntry = null;

            foreach (KeyValuePair<string, ModelEngineEntry> kvp in _Engines)
            {
                LlamaSharpEngine engine = TryGetCompletedEngine(kvp.Value);
                if (engine == null || engine.IsDisposed || engine.ActiveGenerations > 0) continue; // only evict fully-loaded, live engines

                if (lruEntry == null || kvp.Value.LastAccessUtc < lruEntry.LastAccessUtc)
                {
                    lruKey = kvp.Key;
                    lruEntry = kvp.Value;
                }
            }

            if (lruKey == null) return false;

            _Engines.TryRemove(lruKey, out _);
            SharpAI.Logging.JsonLog.Info(_Logging, _Header + "evicting least-recently-used model to make room: " + lruKey);
            DisposeEntry(lruKey, lruEntry);
            return true;
        }

        private void SweepIdleCallback(object state)
        {
            if (_Disposed || _KeepAliveSeconds <= 0) return;

            try
            {
                DateTime cutoff = DateTime.UtcNow.AddSeconds(-_KeepAliveSeconds);

                lock (_AdmissionLock)
                {
                    foreach (KeyValuePair<string, ModelEngineEntry> kvp in new List<KeyValuePair<string, ModelEngineEntry>>(_Engines))
                    {
                        if (kvp.Value.LastAccessUtc > cutoff) continue;

                        LlamaSharpEngine engine = TryGetCompletedEngine(kvp.Value);
                        if (engine == null || engine.IsDisposed || engine.ActiveGenerations > 0) continue;

                        if (_Engines.TryRemove(kvp.Key, out ModelEngineEntry removed))
                        {
                            SharpAI.Logging.JsonLog.Info(_Logging, _Header + "evicting idle model (keep-alive expired): " + kvp.Key);
                            DisposeEntry(kvp.Key, removed);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "exception during idle sweep:" + Environment.NewLine + ex.ToString());
            }
        }

        private async Task<LlamaSharpEngine> CreateEngineAsync(string filename)
        {
            LlamaSharpEngine engine = _EngineFactory == null ? new LlamaSharpEngine(_Logging) : await _EngineFactory(filename).ConfigureAwait(false);
            engine.ModelPath = filename;
            try
            {
                if (_EngineFactory == null) await engine.InitializeAsync(filename).ConfigureAwait(false);
                if (_Disposed || !_Engines.ContainsKey(filename)) { engine.Dispose(); throw new ObjectDisposedException(nameof(ModelEngineService)); }
                return engine;
            }
            catch { engine.Dispose(); throw; }
        }

        private LlamaSharpEngine TryGetCompletedEngine(ModelEngineEntry entry)
        {
            if (entry == null || !entry.Lazy.IsValueCreated) return null;

            Task<LlamaSharpEngine> task = entry.Lazy.Value;
            if (task.Status != TaskStatus.RanToCompletion) return null;

            return task.Result;
        }

        private bool DisposeEntry(string filename, ModelEngineEntry entry)
        {
            LlamaSharpEngine engine = TryGetCompletedEngine(entry);
            if (engine == null || engine.IsDisposed) return false;

            try
            {
                engine.Dispose();
                SharpAI.Logging.JsonLog.Info(_Logging, _Header + "unloaded model: " + filename);
                return true;
            }
            catch (Exception ex)
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "exception during model unload:" + Environment.NewLine + ex.ToString());
                return false;
            }
        }

        private void RemoveEntryIfSame(string filename, ModelEngineEntry expected)
        {
            if (_Engines.TryGetValue(filename, out ModelEngineEntry current) && ReferenceEquals(current, expected))
            {
                _Engines.TryRemove(new KeyValuePair<string, ModelEngineEntry>(filename, expected));
            }
        }

        private int CountResidentModels()
        {
            int count = 0;
            foreach (KeyValuePair<string, ModelEngineEntry> kvp in _Engines)
            {
                LlamaSharpEngine engine = TryGetCompletedEngine(kvp.Value);
                if (engine != null && !engine.IsDisposed) count++;
            }
            return count;
        }

        #endregion
    }
}
