namespace Test.Xunit
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Database;
    using SharpAI.Hosting;
    using SharpAI.Models;
    using SharpAI.Operations;
    using SharpAI.Services;
    using SyslogLogging;
    using Xunit;

    /// <summary>Runs production downloads over real HTTP against the committed Postman stand-in.</summary>
    public sealed class DownloadSimulationTests : IDisposable
    {
        private readonly CancellationToken _Token = CancellationToken.None;
        private readonly string _Directory = Path.Combine(Path.GetTempPath(), "sharpai-download-" + Guid.NewGuid());
        private static readonly byte[] _Data = Encoding.UTF8.GetBytes("verified model content");
        private static string BaseUrl { get { return Environment.GetEnvironmentVariable("SHARPAI_TEST_HF_URL")!; } }
        public DownloadSimulationTests() { Directory.CreateDirectory(_Directory); }
        public void Dispose() { Directory.Delete(_Directory, true); }
        private static string Digest { get { return Convert.ToHexString(SHA256.HashData(_Data)); } }
        private string Destination { get { return Path.Combine(_Directory, "model"); } }

        private void Seed(string url, string? etag = "\"v1\"", string? identity = null, int length = 4)
        {
            File.WriteAllBytes(Destination + ".partial", _Data.Take(length).ToArray());
            File.WriteAllText(Destination + ".partial.json", JsonSerializer.Serialize(new string?[] { identity ?? url + "\n" + Digest + "\n" + _Data.Length, etag }));
        }

        [SimulationTheory]
        [InlineData("ok", false)] [InlineData("ok", true)] [InlineData("ignore", true)] [InlineData("changed", true)]
        public async Task FullResumeAndRangeFallbackAreVerified(string scenario, bool resume)
        {
            string url = BaseUrl + "/file/" + scenario;
            if (resume) Seed(url);
            File.WriteAllText(Destination, "old valid file");
            using HttpClient http = new HttpClient();
            int callbacks = 0;
            Assert.Equal(Digest, await VerifiedDownload.DownloadAsync(http, url, Destination, Digest.ToLowerInvariant(), _Data.Length,
                (path, bytes, progress) => { Assert.Equal(Destination, path); Assert.InRange(progress, 0m, 1m); callbacks++; }));
            Assert.Equal(_Data, File.ReadAllBytes(Destination)); Assert.False(File.Exists(Destination + ".partial"));
            Assert.False(File.Exists(Destination + ".partial.json")); Assert.True(callbacks > 0);
        }

        [SimulationTheory]
        [InlineData("wrongrange")] [InlineData("wrongetag")] [InlineData("wrongtotal")] [InlineData("wrongend")] [InlineData("missingrange")] [InlineData("wrongunit")]
        public async Task InvalidResumeDoesNotModifyPartialOrPublishedFile(string scenario)
        {
            string url = BaseUrl + "/file/" + scenario; Seed(url); File.WriteAllText(Destination, "old");
            using HttpClient http = new HttpClient();
            await Assert.ThrowsAsync<InvalidDataException>(() => VerifiedDownload.DownloadAsync(http, url, Destination, Digest, _Data.Length));
            Assert.Equal("old", File.ReadAllText(Destination)); Assert.Equal(_Data.Take(4), File.ReadAllBytes(Destination + ".partial"));
        }

        /// <summary>Repository URLs retain their revision and exact file selection through real HTTP metadata, tree and verified download calls.</summary>
        [SimulationTheory]
        [InlineData("tree")] [InlineData("resolve")] [InlineData("blob")]
        public async Task FullRepositoryUrlsPreserveRevisionAndExactGgufSelection(string kind)
        {
            using DatabaseDriverBase database = await DatabaseDriverFactory.CreateAndInitializeAsync(new DatabaseSettings { Filename = Path.Combine(_Directory, "url.db") }, new LoggingModule(), _Token);
            ModelFileService registry = new ModelFileService(new LoggingModule(), database.Models, _Directory);
            HuggingFaceClient client = new HuggingFaceClient(new LoggingModule(), null!, BaseUrl);
            string source = "https://huggingface.co/org/single/" + kind + "/revision1" + (kind == "tree" ? "" : "/model-Q4_K_M.gguf");
            ModelFile model = await ModelPullService.PullAsync(client, registry, _Directory, source, token: _Token);
            Assert.Equal(source, model.Name); Assert.Contains("/org/single/tree/revision1", model.SourceUrl);
            Assert.True(File.Exists(registry.GetFilename(source)));
            await Assert.ThrowsAsync<InvalidDataException>(() => ModelPullService.PullAsync(client, registry, _Directory, "https://huggingface.co/org/single/resolve/revision1/missing.gguf", token: _Token));
            await Assert.ThrowsAsync<HttpRequestException>(() => ModelPullService.PullAsync(client, registry, _Directory, "https://huggingface.co/org/single/tree/absent", token: _Token));
        }

        [SimulationTheory]
        [InlineData("corrupt")] [InlineData("oversize")] [InlineData("short")]
        [InlineData("unexpected206")] [InlineData("badlength")] [InlineData("empty")]
        public async Task BadContentNeverReplacesPublishedModel(string scenario)
        {
            File.WriteAllText(Destination, "old");
            using HttpClient http = new HttpClient();
            await Assert.ThrowsAsync<InvalidDataException>(() => VerifiedDownload.DownloadAsync(http, BaseUrl + "/file/" + scenario, Destination, Digest, _Data.Length));
            Assert.Equal("old", File.ReadAllText(Destination));
            if (scenario == "corrupt" || scenario == "oversize" || scenario == "short") Assert.False(File.Exists(Destination + ".partial"));
        }

        [SimulationTheory]
        [InlineData("error")] [InlineData("html")]
        public async Task UpstreamErrorsDoNotPublish(string scenario)
        {
            using HttpClient http = new HttpClient();
            await Assert.ThrowsAsync<HttpRequestException>(() => VerifiedDownload.DownloadAsync(http, BaseUrl + "/file/" + scenario, Destination, Digest, _Data.Length));
            Assert.False(File.Exists(Destination));
        }

        [SimulationTheory]
        [InlineData("different-revision", "\"v1\"")] [InlineData(null, "W/\"v1\"")] [InlineData(null, null)] [InlineData(null, "invalid")]
        public async Task StaleOrWeakResumeIdentityStartsOver(string? identity, string? etag)
        {
            string url = BaseUrl + "/file/ok"; Seed(url, etag, identity);
            using HttpClient http = new HttpClient();
            await VerifiedDownload.DownloadAsync(http, url, Destination, Digest, _Data.Length);
            Assert.Equal(_Data, File.ReadAllBytes(Destination));
        }

        [SimulationTheory]
        [InlineData("{broken")] [InlineData("[]")] [InlineData("null")]
        public async Task MalformedSidecarStartsOver(string json)
        {
            string url = BaseUrl + "/file/ok"; Seed(url); File.WriteAllText(Destination + ".partial.json", json);
            using HttpClient http = new HttpClient();
            await VerifiedDownload.DownloadAsync(http, url, Destination, Digest, _Data.Length);
            Assert.Equal(_Data, File.ReadAllBytes(Destination));
        }

        [SimulationTheory]
        [InlineData(0)] [InlineData(22)]
        public async Task EmptyOrCompletePartialIsRevalidatedFromScratch(int length)
        {
            string url = BaseUrl + "/file/ok"; Seed(url, length: length);
            using HttpClient http = new HttpClient();
            await VerifiedDownload.DownloadAsync(http, url, Destination, Digest, _Data.Length);
            Assert.Equal(_Data, File.ReadAllBytes(Destination));
        }

        [SimulationTheory]
        [InlineData(250)]
        public async Task CancellationRetainsResumablePartialAndNeverPublishes(int milliseconds)
        {
            using HttpClient http = new HttpClient(); using CancellationTokenSource cancellation = new CancellationTokenSource(milliseconds);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => VerifiedDownload.DownloadAsync(http, BaseUrl + "/file/slow", Destination, Digest, _Data.Length, token: cancellation.Token));
            Assert.False(File.Exists(Destination)); Assert.True(File.Exists(Destination + ".partial"));
            Assert.Equal(4, new FileInfo(Destination + ".partial").Length); Assert.True(File.Exists(Destination + ".partial.json"));
        }

        [SimulationTheory]
        [InlineData("single", 1)] [InlineData("split", 2)]
        public async Task PullPublishesCompleteSetAndConcurrentPullsShareRegistryEntry(string scenario, int count)
        {
            using DatabaseDriverBase db = await DatabaseDriverFactory.CreateAndInitializeAsync(new DatabaseSettings { Filename = Path.Combine(_Directory, "registry.db") }, new LoggingModule());
            ModelFileService registry = new ModelFileService(new LoggingModule(), db.Models, _Directory);
            HuggingFaceClient client = new HuggingFaceClient(new LoggingModule(), null!, BaseUrl);
            Task<ModelFile>[] tasks = Enumerable.Range(0, 8).Select(_ => ModelPullService.PullAsync(client, registry, _Directory, "org/" + scenario)).ToArray();
            ModelFile[] models = await Task.WhenAll(tasks);
            Assert.All(models, model => Assert.Equal(models[0].GUID, model.GUID));
            string entry = GgufShards.Resolve(_Directory, models[0].GUID);
            Assert.Equal(count, GgufShards.Files(entry).Count); Assert.Equal(entry, registry.GetFilename("org/" + scenario));
            Assert.Equal(models[0].ContentLength, GgufShards.Files(entry).Sum(path => new FileInfo(path).Length));
            Assert.Equal(64, models[0].SHA256Hash.Length); Assert.Contains("revision1", models[0].SourceUrl);
            Assert.DoesNotContain(".partial", entry);
            Assert.Empty(Directory.GetDirectories(_Directory, ".pull-*"));
        }

        [SimulationTheory]
        [InlineData("corrupt")] [InlineData("missinghash")] [InlineData("incomplete")]
        [InlineData("notfound")] [InlineData("norevision")] [InlineData("nogguf")]
        [InlineData("projector")] [InlineData("unsupported")]
        public async Task FailedPullNeverRegistersAndLockIsReleased(string scenario)
        {
            using DatabaseDriverBase db = await DatabaseDriverFactory.CreateAndInitializeAsync(new DatabaseSettings { Filename = Path.Combine(_Directory, "registry.db") }, new LoggingModule());
            ModelFileService registry = new ModelFileService(new LoggingModule(), db.Models, _Directory);
            HuggingFaceClient client = new HuggingFaceClient(new LoggingModule(), null!, BaseUrl);
            for (int i = 0; i < 2; i++)
            {
                Exception error = await Assert.ThrowsAnyAsync<Exception>(() => ModelPullService.PullAsync(client, registry, _Directory, "org/" + scenario));
                if (scenario == "notfound") Assert.IsType<HttpRequestException>(error);
                else if (scenario == "nogguf") Assert.IsType<System.Collections.Generic.KeyNotFoundException>(error);
                else Assert.IsType<InvalidDataException>(error);
                Assert.Null(registry.GetByName("org/" + scenario));
            }
            Assert.DoesNotContain(Directory.GetDirectories(_Directory), path => Guid.TryParse(Path.GetFileName(path), out _));
        }
        [SimulationTheory]
        [InlineData("paged")]
        public async Task RepositoryPaginationRetainsEveryShard(string scenario)
        {
            HuggingFaceClient client = new HuggingFaceClient(new LoggingModule(), null!, BaseUrl);
            System.Collections.Generic.List<SharpAI.Hosting.HuggingFace.GgufFileInfo> files = await client.GetGgufFilesAsync("org/" + scenario, token: default, revision: "revision1");
            Assert.Equal(2, files.Count); Assert.Equal(2, GgufShards.Select(files, files[0]).Count);
        }

        [SimulationTheory]
        [InlineData("pageescape")] [InlineData("pageloop")]
        public async Task UnsafePaginationFailsWithoutFollowingTarget(string scenario)
        {
            HuggingFaceClient client = new HuggingFaceClient(new LoggingModule(), null!, BaseUrl);
            await Assert.ThrowsAsync<InvalidDataException>(() => client.GetModelFilesAsync("org/" + scenario, token: default, revision: "revision1"));
        }

        [SimulationTheory]
        [InlineData("single")] [InlineData("split")]
        public async Task RegistryFailureCleansPublishedFiles(string scenario)
        {
            using DatabaseDriverBase db = await DatabaseDriverFactory.CreateAndInitializeAsync(new DatabaseSettings { Filename = Path.Combine(_Directory, "registry.db") }, new LoggingModule());
            db.NonQuery("CREATE TRIGGER reject_model BEFORE INSERT ON model_files BEGIN SELECT RAISE(FAIL, 'injected registry write failure'); END", null!);
            ModelFileService registry = new ModelFileService(new LoggingModule(), db.Models, _Directory);
            HuggingFaceClient client = new HuggingFaceClient(new LoggingModule(), null!, BaseUrl);
            Exception error = await Assert.ThrowsAnyAsync<Exception>(() => ModelPullService.PullAsync(client, registry, _Directory, "org/" + scenario));
            Assert.Contains("injected registry write failure", error.Message);
            Assert.Null(registry.GetByName("org/" + scenario));
            Assert.DoesNotContain(Directory.GetFileSystemEntries(_Directory), path => Guid.TryParse(Path.GetFileName(path), out _));
        }

        [SimulationTheory]
        [InlineData("single", false)] [InlineData("single", true)]
        [InlineData("split", false)] [InlineData("split", true)]
        public async Task PublicationJournalRecoversOrphanAndPreservesCommittedRegistry(string scenario, bool committed)
        {
            using DatabaseDriverBase? db = await DatabaseDriverFactory.CreateAndInitializeAsync(new DatabaseSettings { Filename = Path.Combine(_Directory, "registry.db") }, new LoggingModule());
            ModelFileService? registry = new ModelFileService(new LoggingModule(), db.Models, _Directory);
            HuggingFaceClient? client = new HuggingFaceClient(new LoggingModule(), null!, BaseUrl);
            string name = "org/" + scenario;
            ModelFile? first = await ModelPullService.PullAsync(client, registry, _Directory, name);
            string original = registry.GetFilename(name);
            if (!committed) registry.Delete(first.GUID);
            string journal = Path.Combine(_Directory, ".registration-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name.ToLowerInvariant()))) + ".json");
            File.WriteAllText(journal, first.GUID.ToString());
            // A killed journal writer can leave an incomplete temporary file; recovery only reads the atomic final name.
            if (!committed) File.WriteAllText(journal + ".partial", "incomplete");
            ModelFile? recovered = await ModelPullService.PullAsync(client, registry, _Directory, name);
            Assert.Equal(committed, recovered.GUID == first.GUID);
            Assert.True(File.Exists(registry.GetFilename(name)));
            Assert.Equal(committed, File.Exists(original));
            Assert.False(File.Exists(journal)); Assert.False(File.Exists(journal + ".partial"));
            Assert.Empty(Directory.GetDirectories(_Directory, ".pull-*"));
        }

    }
}
