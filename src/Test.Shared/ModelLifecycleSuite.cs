namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using SharpAI.Engines;
    using SharpAI.Exceptions;
    using SharpAI.Services;
    using SyslogLogging;

    using Touchstone.Core;

    /// <summary>
    /// Fixture-gated (W10.T3) lifecycle suite for <see cref="ModelEngineService"/>: load/unload accounting,
    /// keep-alive expiry semantics, memory-budget admission refusal, and max-resident LRU eviction. Skips
    /// cleanly when no GGUF fixture is available.
    /// </summary>
    public static class ModelLifecycleSuite
    {
        #region Public-Methods

        /// <summary>
        /// Build the lifecycle suite.
        /// </summary>
        /// <returns>Lifecycle suite.</returns>
        public static TestSuiteDescriptor Build()
        {
            bool skip = !ModelFixture.IsAvailable;
            string reason = ModelFixture.SkipReason;

            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor("ModelLifecycle", "LoadUnload", "Loading registers the model; unload frees it",
                    ExecuteLoadUnload, Array.Empty<string>(), skip, reason),

                new TestCaseDescriptor("ModelLifecycle", "Expiry", "Keep-alive expiry is null when disabled and future when enabled",
                    ExecuteExpiry, Array.Empty<string>(), skip, reason),

                new TestCaseDescriptor("ModelLifecycle", "AdmissionRefusal", "A model larger than the memory budget is refused",
                    ExecuteAdmissionRefusal, Array.Empty<string>(), skip, reason),

                new TestCaseDescriptor("ModelLifecycle", "LruEviction", "Max-resident cap evicts the least-recently-used model",
                    ExecuteLruEviction, Array.Empty<string>(), skip, reason),

                new TestCaseDescriptor("ModelLifecycle", "ParallelSlots", "Multiple concurrent generation slots run without error",
                    ExecuteParallelSlots, Array.Empty<string>(), skip, reason)
            };

            return new TestSuiteDescriptor("ModelLifecycle", "Model lifecycle (fixture-gated)", cases);
        }

        #endregion

        #region Private-Methods

        // The service reads its lifecycle knobs from environment variables in its constructor, so configure
        // them here before constructing and clear them immediately after (the ctor snapshots them).
        private static ModelEngineService NewService(int keepAliveSeconds = 0, int maxResidentModels = 0, int memoryBudgetMb = 0)
        {
            string prevKeepAlive = Environment.GetEnvironmentVariable("SHARPAI_KEEP_ALIVE_SECONDS");
            string prevMaxResident = Environment.GetEnvironmentVariable("SHARPAI_MAX_RESIDENT_MODELS");
            string prevBudget = Environment.GetEnvironmentVariable("SHARPAI_MODEL_MEMORY_BUDGET_MB");

            Environment.SetEnvironmentVariable("SHARPAI_KEEP_ALIVE_SECONDS", keepAliveSeconds.ToString());
            Environment.SetEnvironmentVariable("SHARPAI_MAX_RESIDENT_MODELS", maxResidentModels.ToString());
            Environment.SetEnvironmentVariable("SHARPAI_MODEL_MEMORY_BUDGET_MB", memoryBudgetMb.ToString());

            try
            {
                return new ModelEngineService(new LoggingModule());
            }
            finally
            {
                Environment.SetEnvironmentVariable("SHARPAI_KEEP_ALIVE_SECONDS", prevKeepAlive);
                Environment.SetEnvironmentVariable("SHARPAI_MAX_RESIDENT_MODELS", prevMaxResident);
                Environment.SetEnvironmentVariable("SHARPAI_MODEL_MEMORY_BUDGET_MB", prevBudget);
            }
        }

        private static async Task ExecuteLoadUnload(CancellationToken token)
        {
            ModelEngineService service = NewService();
            try
            {
                string path = ModelFixture.Path!;
                LlamaSharpEngine engine = await service.GetByModelFileAsync(path, token).ConfigureAwait(false);
                TestAssert.True(engine != null && engine.IsInitialized, "engine should load and initialize");
                TestAssert.True(service.GetLoadedModelPaths().Contains(path), "loaded paths should contain the model");

                bool unloaded = service.UnloadModel(path);
                TestAssert.True(unloaded, "unload should report success");
                TestAssert.True(!service.GetLoadedModelPaths().Contains(path), "loaded paths should no longer contain the model");
            }
            finally
            {
                service.Dispose();
            }
        }

        private static async Task ExecuteExpiry(CancellationToken token)
        {
            string path = ModelFixture.Path!;

            ModelEngineService disabled = NewService(keepAliveSeconds: 0);
            try
            {
                await disabled.GetByModelFileAsync(path, token).ConfigureAwait(false);
                TestAssert.True(disabled.GetExpiryUtc(path) == null, "expiry should be null when keep-alive is disabled");
            }
            finally
            {
                disabled.Dispose();
            }

            ModelEngineService enabled = NewService(keepAliveSeconds: 120);
            try
            {
                await enabled.GetByModelFileAsync(path, token).ConfigureAwait(false);
                DateTime? expiry = enabled.GetExpiryUtc(path);
                TestAssert.True(expiry != null, "expiry should be set when keep-alive is enabled");
                TestAssert.True(expiry!.Value > DateTime.UtcNow, "expiry should be in the future");
            }
            finally
            {
                enabled.Dispose();
            }
        }

        private static async Task ExecuteAdmissionRefusal(CancellationToken token)
        {
            // Budget far below the model's file size: with no resident models to evict, admission must fail
            // with a structured exception rather than attempting the load.
            ModelEngineService service = NewService(memoryBudgetMb: 1);
            try
            {
                string path = ModelFixture.Path!;

                bool threw = false;
                try
                {
                    await service.GetByModelFileAsync(path, token).ConfigureAwait(false);
                }
                catch (ModelAdmissionException)
                {
                    threw = true;
                }

                TestAssert.True(threw, "loading a model larger than the memory budget should throw ModelAdmissionException");
            }
            finally
            {
                service.Dispose();
            }
        }

        private static async Task ExecuteLruEviction(CancellationToken token)
        {
            string source = ModelFixture.Path!;
            string secondPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(source)!,
                "lru-copy-" + Guid.NewGuid().ToString("N") + ".gguf");

            // A distinct second path is needed to exercise eviction. Prefer a hard/symbolic link to avoid
            // copying a multi-GB file; fall back to a copy when links are unavailable.
            bool linked = TryLink(source, secondPath);
            if (!linked)
            {
                File.Copy(source, secondPath, true);
            }

            ModelEngineService service = NewService(maxResidentModels: 1);
            try
            {
                await service.GetByModelFileAsync(source, token).ConfigureAwait(false);
                await service.GetByModelFileAsync(secondPath, token).ConfigureAwait(false);

                List<string> loaded = service.GetLoadedModelPaths();
                TestAssert.Equal(1, loaded.Count);
                TestAssert.True(loaded.Contains(secondPath), "the most-recently-used model should remain resident");
                TestAssert.True(!loaded.Contains(source), "the least-recently-used model should have been evicted");
            }
            finally
            {
                service.Dispose();
                try { if (File.Exists(secondPath)) File.Delete(secondPath); } catch (Exception) { }
            }
        }

        // Regression lock for the concurrency fix: with more than one generation slot, concurrent decode
        // must run on isolated executors/contexts (a shared StatelessExecutor previously raced and threw
        // ObjectDisposedException). Configure 3 slots, fire 3 concurrent generations, and require all to
        // complete without error.
        private static async Task ExecuteParallelSlots(CancellationToken token)
        {
            string prev = Environment.GetEnvironmentVariable("SHARPAI_MAX_CONCURRENT_GENERATIONS");
            Environment.SetEnvironmentVariable("SHARPAI_MAX_CONCURRENT_GENERATIONS", "3");

            LlamaSharpEngine engine = new LlamaSharpEngine();
            try
            {
                await engine.InitializeAsync(ModelFixture.Path!).ConfigureAwait(false);

                Task<string> a = engine.GenerateTextAsync("A one-sentence fact:", 16, 0.2f, null, token);
                Task<string> b = engine.GenerateTextAsync("B one-sentence fact:", 16, 0.2f, null, token);
                Task<string> c = engine.GenerateTextAsync("C one-sentence fact:", 16, 0.2f, null, token);

                string[] results = await Task.WhenAll(a, b, c).ConfigureAwait(false);
                TestAssert.Equal(3, results.Length);
                foreach (string r in results) TestAssert.True(r != null, "each concurrent generation should complete");
            }
            finally
            {
                engine.Dispose();
                Environment.SetEnvironmentVariable("SHARPAI_MAX_CONCURRENT_GENERATIONS", prev);
            }
        }

        private static bool TryLink(string source, string linkPath)
        {
            try
            {
                File.CreateSymbolicLink(linkPath, source);
                return File.Exists(linkPath);
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion
    }
}
