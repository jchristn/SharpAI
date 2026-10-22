namespace Test.Xunit
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using SharpAI.Database;
    using SharpAI.Hosting;
    using SharpAI.Operations;
    using SharpAI.Services;
    using SyslogLogging;
    using Xunit;

    public sealed class RealShardTheoryAttribute : TheoryAttribute
    {
        public RealShardTheoryAttribute()
        {
            if (String.IsNullOrEmpty(Environment.GetEnvironmentVariable("SHARPAI_TEST_SPLIT_DIRECTORY")) ||
                String.IsNullOrEmpty(Environment.GetEnvironmentVariable("SHARPAI_TEST_HF_URL")))
                Skip = "Requires real split weights served by the Postman simulation.";
        }
    }

    public sealed class RealShardTests
    {
        [RealShardTheory]
        [InlineData("org/real")]
        public async Task DownloadInspectLoadGenerateUnloadAndDeleteRealShards(string name)
        {
            string directory = Path.Combine(Path.GetTempPath(), "sharpai-real-shards-" + Guid.NewGuid());
            Directory.CreateDirectory(directory);
            try
            {
                using DatabaseDriverBase? db = await DatabaseDriverFactory.CreateAndInitializeAsync(new DatabaseSettings { Filename = Path.Combine(directory, "registry.db") }, new LoggingModule());
                ModelFileService? registry = new ModelFileService(new LoggingModule(), db.Models, directory);
                HuggingFaceClient? upstream = new HuggingFaceClient(new LoggingModule(), null!, Environment.GetEnvironmentVariable("SHARPAI_TEST_HF_URL"));
                SharpAI.Models.ModelFile? model = await ModelPullService.PullAsync(upstream, registry, directory, name);
                string entry = registry.GetFilename(name);
                Assert.Equal(2, GgufShards.Files(entry).Count);
                Assert.True(ModelInspector.Inspect(entry).ParameterCount > 1000000);
                using (ModelEngineService? service = new ModelEngineService(new LoggingModule()))
                {
                    SharpAI.Engines.LlamaSharpEngine? engine = await service.GetByModelFileAsync(entry);
                    Assert.NotEmpty(await engine.GenerateTextAsync("Once upon a time", 4));
                    Assert.True(service.UnloadModel(entry)); Assert.True(engine.IsDisposed);
                }
                // Real incomplete sets must be rejected before native initialization.
                string second = GgufShards.Files(entry)[1]; string hidden = second + ".hidden";
                File.Move(second, hidden);
                Assert.ThrowsAny<Exception>(() => ModelInspector.Inspect(entry));
                File.Move(hidden, second);
                registry.Delete(model.GUID); GgufShards.Delete(directory, model.GUID);
                Assert.Null(registry.GetByName(name)); Assert.False(Directory.Exists(Path.GetDirectoryName(entry)));
            }
            finally { Directory.Delete(directory, true); }
        }
    }
}
