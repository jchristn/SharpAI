namespace SharpAI.Client
{
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed partial class ServerClient
    {
        /// <summary>Pull organization/name or a validated Hugging Face repository/tree/resolve/blob HTTPS URL.
        /// Requested revisions resolve to immutable commits; exact file URLs preserve GGUF and shard selection.
        /// Streaming errors throw. Pulls are never automatically retried.</summary>
        public async IAsyncEnumerable<PullProgress> PullAsync(string source, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (JsonNode frame in StreamAsync("/api/pull", new JsonObject { ["model"] = source, ["stream"] = true }, cancellationToken).ConfigureAwait(false))
                yield return frame.Deserialize<PullProgress>() ?? throw new System.IO.InvalidDataException("Invalid pull progress record.");
        }
    }
}
