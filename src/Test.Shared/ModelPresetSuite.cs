namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using SharpAI.Database;
    using SharpAI.Database.Sqlite;
    using SharpAI.Models;

    using SyslogLogging;

    using Touchstone.Core;

    /// <summary>
    /// Contract suite for the model-preset (Modelfile-equivalent) data layer, exercised against SQLite. The
    /// same <see cref="SharpAI.Database.Interfaces.IModelPresetMethods"/> contract is honored by the other
    /// providers via their v5 migration.
    /// </summary>
    public static class ModelPresetSuite
    {
        #region Private-Members

        private static readonly object _Lock = new object();
        private static SqliteDatabaseDriver _Driver = null!;
        private static string _DbPath = null!;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build the preset contract suite.
        /// </summary>
        /// <returns>Preset suite.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor("Presets", "Create_Read_RoundTrip", "Create then read a preset by name and GUID",
                    ct =>
                    {
                        ModelPreset created = Db().Presets.Add(NewPreset("assistant-json"));
                        ModelPreset byName = Db().Presets.GetByName("assistant-json");
                        TestAssert.True(byName != null, "preset should be found by name");
                        TestAssert.Equal("qwen-base", byName!.ModelName);
                        TestAssert.Equal("You are helpful.", byName.SystemPrompt);
                        TestAssert.True(byName.Temperature.HasValue && Math.Abs(byName.Temperature.Value - 0.3f) < 0.001f, "temperature should round-trip");
                        TestAssert.True(byName.MaxTokens == 256, "max tokens should round-trip");
                        TestAssert.Equal(2, byName.Stop.Count);

                        ModelPreset byGuid = Db().Presets.GetByGuid(created.GUID);
                        TestAssert.True(byGuid != null, "preset should be found by GUID");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Presets", "Duplicate_Name_Returns_Existing", "Adding a duplicate name returns the existing preset",
                    ct =>
                    {
                        ModelPreset first = Db().Presets.Add(NewPreset("dup-preset"));
                        ModelPreset second = Db().Presets.Add(NewPreset("dup-preset"));
                        TestAssert.Equal(first.GUID.ToString(), second.GUID.ToString());
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Presets", "Update", "Updating a preset persists new values",
                    ct =>
                    {
                        ModelPreset p = Db().Presets.Add(NewPreset("to-update"));
                        p.SystemPrompt = "Updated prompt.";
                        p.Temperature = 0.9f;
                        p.Stop = new List<string> { "STOP" };
                        Db().Presets.Update(p);

                        ModelPreset reloaded = Db().Presets.GetByName("to-update");
                        TestAssert.Equal("Updated prompt.", reloaded!.SystemPrompt);
                        TestAssert.True(Math.Abs(reloaded.Temperature!.Value - 0.9f) < 0.001f, "updated temperature should persist");
                        TestAssert.Equal(1, reloaded.Stop.Count);
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Presets", "Enumerate", "Enumerate returns created presets",
                    ct =>
                    {
                        Db().Presets.Add(NewPreset("enum-a"));
                        Db().Presets.Add(NewPreset("enum-b"));
                        EnumerationResult<ModelPreset> page = Db().Presets.Enumerate(new EnumerationQuery { PageSize = 100 });
                        TestAssert.True(page.TotalRecords >= 2, "at least two presets should be enumerated");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Presets", "Delete", "Deleting a preset removes it",
                    ct =>
                    {
                        ModelPreset p = Db().Presets.Add(NewPreset("to-delete"));
                        Db().Presets.Delete(p.GUID);
                        TestAssert.True(Db().Presets.GetByName("to-delete") == null, "deleted preset should not be found");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Presets", "Nullable_Fields", "Null numeric/prompt fields round-trip as null",
                    ct =>
                    {
                        ModelPreset minimal = new ModelPreset { Name = "minimal", ModelName = "qwen-base" };
                        Db().Presets.Add(minimal);
                        ModelPreset reloaded = Db().Presets.GetByName("minimal");
                        TestAssert.True(reloaded!.Temperature == null, "temperature should be null");
                        TestAssert.True(reloaded.MaxTokens == null, "max tokens should be null");
                        TestAssert.True(reloaded.TopP == null, "top-p should be null");
                        TestAssert.True(reloaded.SystemPrompt == null, "system prompt should be null");
                        TestAssert.Equal(0, reloaded.Stop.Count);
                        return Task.CompletedTask;
                    })
            };

            return new TestSuiteDescriptor("Presets", "Model presets (SQLite contract)", cases, BeforeSuiteAsync, AfterSuiteAsync);
        }

        #endregion

        #region Private-Methods

        private static ModelPreset NewPreset(string name)
        {
            return new ModelPreset
            {
                Name = name,
                ModelName = "qwen-base",
                SystemPrompt = "You are helpful.",
                Temperature = 0.3f,
                MaxTokens = 256,
                TopP = 0.9f,
                Stop = new List<string> { "</s>", "User:" }
            };
        }

        private static ValueTask BeforeSuiteAsync(CancellationToken token)
        {
            Db();
            return new ValueTask();
        }

        private static SqliteDatabaseDriver Db()
        {
            lock (_Lock)
            {
                if (_Driver == null)
                {
                    _DbPath = Path.Combine(Path.GetTempPath(), "sharpai-preset-test-" + Guid.NewGuid().ToString("N") + ".db");
                    _Driver = new SqliteDatabaseDriver(new DatabaseSettings(_DbPath), new LoggingModule());
                    _Driver.InitializeAsync().GetAwaiter().GetResult();
                }
                return _Driver;
            }
        }

        private static ValueTask AfterSuiteAsync(CancellationToken token)
        {
            return new ValueTask();
        }

        #endregion
    }
}
