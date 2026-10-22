namespace SharpAI.Operations
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Helpers;
    using SharpAI.Hosting;
    using SharpAI.Hosting.HuggingFace;
    using SharpAI.Models;
    using SharpAI.Services;

    /// <summary>Shared verified pull pipeline for the library and server. Serializes pulls per storage/name.</summary>
    public static class ModelPullService
    {
        private static readonly ConcurrentDictionary<string, byte> _Pulls = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Pull an immutable, complete, verified model set and register it only after publication.
        /// Missing upstream hashes fail closed. Cancellation preserves resumable staging files.</summary>
        /// <param name="client">Repository client.</param><param name="registry">Registry facade.</param>
        /// <param name="storage">Storage directory.</param><param name="name">Repository name.</param>
        /// <param name="priority">Optional quantization preferences.</param><param name="progress">Optional progress.</param>
        /// <param name="token">Cancellation token.</param><returns>Registered model.</returns>
        public static async Task<ModelFile> PullAsync(HuggingFaceClient client, ModelFileService registry, string storage,
            string name, Dictionary<string, int> priority = null, Action<string, long, decimal> progress = null, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(registry);
            ArgumentException.ThrowIfNullOrWhiteSpace(storage);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ModelPullSource source = ModelPullSource.Parse(name);
            name = name.Trim();
            string key = Path.GetFullPath(storage) + "|" + name;
            while (!_Pulls.TryAdd(key, 0)) await Task.Delay(50, token).ConfigureAwait(false);
            string published = null;
            string journal = Path.Combine(storage, ".registration-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name.ToLowerInvariant()))) + ".json");
            try
            {
                token.ThrowIfCancellationRequested();
                // Recover the publication/registry gap after abrupt process termination. The journal
                // contains only a GUID, so recovery cannot delete paths outside model storage.
                if (File.Exists(journal))
                {
                    Guid pending = Guid.Parse(await File.ReadAllTextAsync(journal, token).ConfigureAwait(false));
                    if (registry.GetByGuid(pending) == null) GgufShards.Delete(storage, pending);
                    File.Delete(journal);
                }
                ModelFile existing = registry.GetByName(name);
                if (existing != null) return existing;
                HuggingFaceModelMetadata metadata = await client.GetModelMetadata(source.Repository, token, source.Revision).ConfigureAwait(false);
                if (String.IsNullOrWhiteSpace(metadata.Sha)) throw new InvalidDataException("Repository has no immutable revision.");
                List<GgufFileInfo> files = await client.GetGgufFilesAsync(source.Repository, token, metadata.Sha).ConfigureAwait(false);
                if (files == null || files.Count == 0) throw new KeyNotFoundException("Repository contains no GGUF models.");
                files = files.Where(f => !Path.GetFileName(f.Path).StartsWith("mmproj", StringComparison.OrdinalIgnoreCase)).ToList();
                if (files.Count == 0) throw new InvalidDataException("Vision projectors are not supported.");
                GgufFileInfo selected = source.FilePath != null ? files.FirstOrDefault(file => file.Path == source.FilePath) ?? throw new InvalidDataException("The requested GGUF file is absent from the resolved revision.") : (priority == null || priority.Count == 0 ? GgufSelector.SortByOllamaPreference(files) : GgufSelector.SortByPreference(files, priority)).First();
                List<GgufFileInfo> shards = GgufShards.Select(files, selected);
                string stage = Path.Combine(storage, ".pull-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name.ToLowerInvariant() + "|" + metadata.Sha + "|" + selected.Path))));
                Directory.CreateDirectory(stage);
                long total = 0;
                foreach (GgufFileInfo file in shards)
                {
                    if (!file.Size.HasValue || file.Size.Value <= 0) throw new InvalidDataException("Missing model size.");
                    VerifiedDownload.NormalizeHash(file.Lfs);
                    total = checked(total + file.Size.Value);
                }
                long completed = 0;
                List<string> hashes = new List<string>();
                foreach (GgufFileInfo file in shards)
                {
                    string local = Path.Combine(stage, Path.GetFileName(file.Path));
                    string url = client.GetRevisionDownloadUrl(source.Repository, metadata.Sha, file.Path);
                    using HttpClient http = client.CreateDownloadClient();
                    string hash = await VerifiedDownload.DownloadAsync(http, url, local, file.Lfs, file.Size.Value,
                        (p, bytes, fraction) => progress?.Invoke(p, completed + bytes, Math.Min(0.99m, (decimal)(completed + bytes) / total)), token).ConfigureAwait(false);
                    hashes.Add(hash);
                    completed += file.Size.Value;
                }
                string entry = Path.Combine(stage, Path.GetFileName(shards[0].Path));
                ModelInspection inspection = ModelInspector.Inspect(entry);
                if (!inspection.Supported) throw new InvalidDataException("Model contains unsupported components.");
                GgufMetadataReader.DetectCapabilities(entry, out string architecture, out bool embeddings, out bool generation);
                ModelFile model = new ModelFile
                {
                    Name = name, ContentLength = total, Quantization = selected.QuantizationType,
                    SHA256Hash = shards.Count == 1 ? hashes[0] : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(String.Join("\n", hashes)))),
                    Family = architecture, Embeddings = embeddings, Completions = generation,
                    SourceUrl = "https://huggingface.co/" + source.Repository + "/tree/" + metadata.Sha,
                    ParameterCount = inspection.ParameterCount > 0 ? inspection.ParameterCount : metadata.SafeTensors?.Total ?? 0, ModelCreationUtc = selected.LastModified
                };
                if (shards.Count == 1)
                {
                    using FileStream stream = File.OpenRead(entry);
                    HashResults hashResults = HashHelper.ComputeAllHashes(stream);
                    model.MD5Hash = Convert.ToHexString(hashResults.MD5);
                    model.SHA1Hash = Convert.ToHexString(hashResults.SHA1);
                }
                await File.WriteAllTextAsync(Path.Combine(stage, "manifest.json"), JsonSerializer.Serialize(new { revision = metadata.Sha,
                    files = shards.Select((f, i) => new { path = Path.GetFileName(f.Path), size = f.Size, sha256 = hashes[i] }).ToArray() }), token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                published = Path.Combine(storage, model.GUID.ToString());
                await File.WriteAllTextAsync(journal + ".partial", model.GUID.ToString(), token).ConfigureAwait(false);
                File.Move(journal + ".partial", journal, true);
                if (shards.Count == 1)
                {
                    File.Move(entry, published);
                    Directory.Delete(stage, true);
                }
                else Directory.Move(stage, published);
                ModelFile result = registry.Add(model);
                if (result.GUID != model.GUID) GgufShards.Delete(storage, model.GUID);
                published = null;
                File.Delete(journal);
                progress?.Invoke(name, total, 1m);
                return result;
            }
            finally
            {
                if (published != null)
                {
                    if (Directory.Exists(published)) Directory.Delete(published, true);
                    else File.Delete(published);
                    File.Delete(journal);
                }
                _Pulls.TryRemove(key, out _);
            }
        }
    }
}
