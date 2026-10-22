namespace SharpAI.Operations
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Security.Cryptography;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Verified, resumable transfers. The caller must serialize writes to each destination.</summary>
    public static class VerifiedDownload
    {
        /// <summary>Download to a partial file, validate size and SHA-256, then atomically publish.
        /// A missing or malformed expected hash is rejected. Interrupted transfers retain their partial;
        /// corrupted transfers are discarded. A resumed transfer rehashes the existing prefix.</summary>
        /// <param name="client">Caller-owned HTTP client.</param>
        /// <param name="url">Immutable revision URL.</param>
        /// <param name="destination">Final path.</param>
        /// <param name="sha256">Expected upstream SHA-256, optionally prefixed with sha256:.</param>
        /// <param name="size">Expected positive size.</param>
        /// <param name="progress">Optional progress callback.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Verified uppercase digest.</returns>
        public static async Task<string> DownloadAsync(HttpClient client, string url, string destination,
            string sha256, long size, Action<string, long, decimal> progress = null, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentException.ThrowIfNullOrWhiteSpace(url);
            ArgumentException.ThrowIfNullOrWhiteSpace(destination);
            string expected = NormalizeHash(sha256);
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination)));
            string partial = destination + ".partial";
            string sidecar = partial + ".json";
            string identity = url + "\n" + expected + "\n" + size;
            string etag = null;
            long offset = 0;
            if (File.Exists(partial) && File.Exists(sidecar))
            {
                try
                {
                    string[] state = JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(sidecar, token).ConfigureAwait(false));
                    if (state != null && state.Length == 2 && state[0] == identity && EntityTagHeaderValue.TryParse(state[1], out EntityTagHeaderValue parsed) && !parsed.IsWeak)
                    {
                        etag = state[1];
                        offset = new FileInfo(partial).Length;
                    }
                }
                catch (JsonException) { }
            }
            if (offset <= 0 || offset >= size) { offset = 0; etag = null; }
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            if (offset > 0)
            {
                request.Headers.Range = new RangeHeaderValue(offset, null);
                request.Headers.IfRange = new RangeConditionHeaderValue(EntityTagHeaderValue.Parse(etag));
            }
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.StatusCode == HttpStatusCode.PartialContent)
            {
                ContentRangeHeaderValue range = response.Content.Headers.ContentRange;
                if (offset == 0 || range == null || range.Unit != "bytes" || range.From != offset || range.To != size - 1 || range.Length != size || response.Headers.ETag?.ToString() != etag)
                    throw new InvalidDataException("Invalid resume response; partial file was not modified.");
            }
            else if (response.StatusCode == HttpStatusCode.OK) offset = 0;
            else throw new InvalidDataException("Expected a complete or ranged download response.");
            if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value != size - offset)
                throw new InvalidDataException("Download content length does not match the expected model size.");
            await File.WriteAllTextAsync(sidecar, JsonSerializer.Serialize(new string[] { identity, response.Headers.ETag?.ToString() }), token).ConfigureAwait(false);
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = new byte[65536];
            if (offset > 0)
            {
                using FileStream prefix = File.OpenRead(partial);
                int count;
                while ((count = await prefix.ReadAsync(buffer, token).ConfigureAwait(false)) > 0) hash.AppendData(buffer, 0, count);
            }
            long total = offset;
            using (FileStream output = new FileStream(partial, offset > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 65536, true))
            using (Stream input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            {
                int count;
                while ((count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
                {
                    total = checked(total + count);
                    if (total > size) { output.Close(); File.Delete(partial); File.Delete(sidecar); throw new InvalidDataException("Download exceeds expected size."); }
                    hash.AppendData(buffer, 0, count);
                    await output.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
                    progress?.Invoke(destination, total, Math.Min(0.99m, (decimal)total / size));
                }
                await output.FlushAsync(token).ConfigureAwait(false);
            }
            string actual = Convert.ToHexString(hash.GetHashAndReset());
            if (total != size || !actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(partial);
                File.Delete(sidecar);
                throw new InvalidDataException("Model integrity check failed: size or SHA-256 mismatch.");
            }
            token.ThrowIfCancellationRequested();
            File.Move(partial, destination, true);
            File.Delete(sidecar);
            progress?.Invoke(destination, total, 1m);
            return actual;
        }

        /// <summary>Validate and normalize an upstream digest. Missing digests fail closed.</summary>
        /// <param name="value">Hexadecimal SHA-256.</param>
        /// <returns>Uppercase digest.</returns>
        public static string NormalizeHash(string value)
        {
            if (value != null && value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) value = value.Substring(7);
            if (value == null || value.Length != 64) throw new InvalidDataException("A valid upstream SHA-256 is required for model downloads.");
            foreach (char c in value) if (!Uri.IsHexDigit(c)) throw new InvalidDataException("Invalid upstream SHA-256.");
            return value.ToUpperInvariant();
        }
    }
}
