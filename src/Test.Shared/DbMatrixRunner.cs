namespace Test.Shared
{
    using System;
    using System.Collections.Generic;

    using SharpAI.Database;
    using SharpAI.Models;

    /// <summary>
    /// Provider-agnostic contract runner (W10.T5). Exercises the model-registry, preset, and request-history
    /// data-access contracts against any initialized <see cref="DatabaseDriverBase"/> so the MySQL, PostgreSQL,
    /// and SQL Server drivers can be validated against real servers (spun up as ephemeral containers), not just
    /// the embedded SQLite driver. Returns a structured pass/fail summary; it is not a Touchstone suite because
    /// it runs against a live external server selected at runtime.
    /// </summary>
    public static class DbMatrixRunner
    {
        #region Public-Methods

        /// <summary>
        /// Run the contract against an initialized driver.
        /// </summary>
        /// <param name="db">Initialized database driver.</param>
        /// <returns>Result summary.</returns>
        public static DbMatrixResult Run(DatabaseDriverBase db)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));

            DbMatrixResult result = new DbMatrixResult();

            // ---- Model registry ----
            string modelName = "m-" + Guid.NewGuid().ToString("N");
            Guid modelGuid = Guid.Empty;

            Check(result, "model.add+getByName", () =>
            {
                ModelFile m = new ModelFile
                {
                    Name = modelName,
                    Family = "llama",
                    Format = "gguf",
                    ContentLength = 123456,
                    ParameterCount = 7000000,
                    MD5Hash = "abc",
                    SHA256Hash = "def",
                    Quantization = "Q4_K_M",
                    Completions = true
                };
                db.Models.Add(m);
                modelGuid = m.GUID;
                ModelFile fetched = db.Models.GetByName(modelName);
                Require(fetched != null, "model not found by name");
                Require(fetched.Family == "llama", "family mismatch");
                Require(fetched.Quantization == "Q4_K_M", "quantization mismatch");
            });

            Check(result, "model.getByGuid", () =>
            {
                Require(db.Models.GetByGuid(modelGuid) != null, "model not found by guid");
            });

            Check(result, "model.enumerate", () =>
            {
                EnumerationResult<ModelFile> page = db.Models.Enumerate(new EnumerationQuery { PageSize = 50 });
                Require(page.TotalRecords >= 1, "expected at least one model");
            });

            Check(result, "model.update", () =>
            {
                ModelFile m = db.Models.GetByName(modelName);
                m.Family = "qwen2";
                db.Models.Update(m);
                Require(db.Models.GetByName(modelName).Family == "qwen2", "update did not persist");
            });

            Check(result, "model.delete", () =>
            {
                db.Models.Delete(modelGuid);
                Require(db.Models.GetByName(modelName) == null, "model still present after delete");
            });

            // ---- Presets ----
            string presetName = "p-" + Guid.NewGuid().ToString("N");
            Guid presetGuid = Guid.Empty;

            Check(result, "preset.add+getByName", () =>
            {
                ModelPreset p = new ModelPreset
                {
                    Name = presetName,
                    ModelName = "base",
                    SystemPrompt = "You are helpful.",
                    Temperature = 0.3f,
                    MaxTokens = 256,
                    Stop = new List<string> { "</s>" }
                };
                db.Presets.Add(p);
                presetGuid = p.GUID;
                ModelPreset fetched = db.Presets.GetByName(presetName);
                Require(fetched != null, "preset not found");
                Require(fetched.Temperature.HasValue && Math.Abs(fetched.Temperature.Value - 0.3f) < 0.001f, "temperature mismatch");
                Require(fetched.Stop.Count == 1, "stop list not round-tripped");
            });

            Check(result, "preset.update", () =>
            {
                ModelPreset p = db.Presets.GetByName(presetName);
                p.SystemPrompt = "Updated.";
                db.Presets.Update(p);
                Require(db.Presets.GetByName(presetName).SystemPrompt == "Updated.", "preset update did not persist");
            });

            Check(result, "preset.enumerate", () =>
            {
                Require(db.Presets.Enumerate(new EnumerationQuery { PageSize = 50 }).TotalRecords >= 1, "expected at least one preset");
            });

            Check(result, "preset.delete", () =>
            {
                db.Presets.Delete(presetGuid);
                Require(db.Presets.GetByName(presetName) == null, "preset still present after delete");
            });

            // ---- Request history ----
            string historyId = null;

            Check(result, "history.create+read", () =>
            {
                RequestHistoryEntry e = new RequestHistoryEntry
                {
                    Method = "POST",
                    Path = "/api/chat",
                    Url = "http://127.0.0.1/api/chat",
                    StatusCode = 200,
                    DurationMs = 12.5,
                    SourceIp = "127.0.0.1",
                    CreatedUtc = DateTime.UtcNow,
                    RequestBody = "{\"x\":1}"
                };
                e.RequestHeaders["Content-Type"] = "application/json";
                db.RequestHistory.Create(e);
                historyId = e.Id;
                RequestHistoryEntry read = db.RequestHistory.Read(historyId);
                Require(read != null, "history entry not found");
                Require(read.RequestBody == "{\"x\":1}", "request body not round-tripped");
            });

            Check(result, "history.enumerate", () =>
            {
                RequestHistoryQuery q = new RequestHistoryQuery();
                Require(db.RequestHistory.Enumerate(q).TotalRecords >= 1, "expected at least one history entry");
            });

            Check(result, "history.summarize", () =>
            {
                RequestHistoryQuery q = new RequestHistoryQuery();
                Require(db.RequestHistory.Summarize(q) != null, "summarize returned null");
            });

            Check(result, "history.delete", () =>
            {
                Require(db.RequestHistory.Delete(historyId), "delete returned false");
                Require(db.RequestHistory.Read(historyId) == null, "history entry still present after delete");
            });

            return result;
        }

        #endregion

        #region Private-Methods

        private static void Check(DbMatrixResult result, string name, Action action)
        {
            result.Total++;
            try
            {
                action();
                result.Passed++;
            }
            catch (Exception ex)
            {
                result.Failures.Add(name + ": " + ex.Message);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        #endregion

        #region Public-Types

        /// <summary>
        /// Result of a matrix run.
        /// </summary>
        public class DbMatrixResult
        {
            /// <summary>Total checks executed.</summary>
            public int Total { get; set; }

            /// <summary>Checks that passed.</summary>
            public int Passed { get; set; }

            /// <summary>Failure descriptions (empty when all passed).</summary>
            public List<string> Failures { get; set; } = new List<string>();
        }

        #endregion
    }
}
