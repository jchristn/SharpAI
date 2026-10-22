namespace Test.Shared
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Database;
    using SharpAI.Hosting;
    using SharpAI.Operations;
    using SharpAI.Services;
    using SyslogLogging;

    /// <summary>Child process used by destructive restart tests; pauses at observable persistence boundaries.</summary>
    public static class RecoveryProbe
    {
        public static async Task<int> RunAsync(string[] args)
        {
            string mode = args[1], root = args[2], url = args[3], checkpoint = args[4];
            Directory.CreateDirectory(root);
            void Pause()
            {
                File.WriteAllText(Path.Combine(root, "checkpoint"), checkpoint);
                Thread.Sleep(Timeout.Infinite);
            }
            if (mode == "download")
            {
                byte[] data = new byte[1024 * 1024]; Array.Fill(data, (byte)120);
                string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data));
                using HttpClient client = new HttpClient();
                await VerifiedDownload.DownloadAsync(client, url + "/file/crash", Path.Combine(root, "model"), hash, data.Length,
                    (path, bytes, fraction) => { if (checkpoint == "partial" && bytes >= 131072) Pause(); });
                return 0;
            }
            using DatabaseDriverBase? db = await DatabaseDriverFactory.CreateAndInitializeAsync(new DatabaseSettings { Filename = Path.Combine(root, "registry.db") }, new LoggingModule());
            ModelFileService? registry = new ModelFileService(new LoggingModule(), new CheckpointRegistry(db.Models, () => { if (checkpoint == "registering") Pause(); }, model => { if (checkpoint == "registered") { File.WriteAllText(Path.Combine(root, "result"), model.GUID.ToString()); Pause(); } }), root);
            HuggingFaceClient? upstream = new HuggingFaceClient(new LoggingModule(), null!, url);
            SharpAI.Models.ModelFile? model = await ModelPullService.PullAsync(upstream, registry, root, "org/split", progress:
                (path, bytes, fraction) => { if (checkpoint == "verified" && File.Exists(path)) Pause(); });
            File.WriteAllText(Path.Combine(root, "result"), model.GUID.ToString());
            if (checkpoint == "registered") Pause();
            return 0;
        }
        private sealed class CheckpointRegistry : SharpAI.Database.Interfaces.IModelRegistryMethods
        {
            private readonly SharpAI.Database.Interfaces.IModelRegistryMethods _Inner;
            private readonly Action _Checkpoint;
            private readonly Action<SharpAI.Models.ModelFile> _After;
            public CheckpointRegistry(SharpAI.Database.Interfaces.IModelRegistryMethods inner, Action checkpoint, Action<SharpAI.Models.ModelFile> after) { _Inner = inner; _Checkpoint = checkpoint; _After = after; }
            public SharpAI.Models.EnumerationResult<SharpAI.Models.ModelFile> Enumerate(SharpAI.Models.EnumerationQuery query) => _Inner.Enumerate(query);
            public SharpAI.Models.ModelFile GetByGuid(Guid guid) => _Inner.GetByGuid(guid);
            public SharpAI.Models.ModelFile GetByName(string name) => _Inner.GetByName(name);
            public System.Collections.Generic.List<SharpAI.Models.ModelFile> GetMany(System.Collections.Generic.List<Guid> guids) => _Inner.GetMany(guids);
            public bool ExistsByGuid(Guid guid) => _Inner.ExistsByGuid(guid);
            public SharpAI.Models.ModelFile Add(SharpAI.Models.ModelFile model) { _Checkpoint(); SharpAI.Models.ModelFile? result = _Inner.Add(model); _After(result); return result; }
            public SharpAI.Models.ModelFile Update(SharpAI.Models.ModelFile model) => _Inner.Update(model);
            public void Delete(Guid guid) => _Inner.Delete(guid);
        }

    }
}
