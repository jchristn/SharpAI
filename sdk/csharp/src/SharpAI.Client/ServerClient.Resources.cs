namespace SharpAI.Client
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed partial class ServerClient
    {
        /// <summary>Read the authoritative authentication and operation capabilities.</summary>
        public Task<JsonNode?> AuthContextAsync(CancellationToken cancellationToken) { return JsonAsync("GET", "/v1.0/auth/context", null, cancellationToken); }
        /// <summary>Read the effective library workspace and principal.</summary>
        public Task<JsonNode?> LibraryContextAsync(string? tenantId, CancellationToken cancellationToken) { return JsonAsync("GET", "/v1.0/library-context" + Query(new Dictionary<string, string?> { ["tenantId"] = tenantId }), null, cancellationToken); }
        /// <summary>Read a page of scoped library records.</summary>
        public async Task<ResourcePage<LibraryRecord>> LibrariesAsync(string kind, int page, int pageSize, string? tenantId, CancellationToken cancellationToken)
        {
            JsonNode? result = await JsonAsync("GET", LibraryPath(kind) + Query(new Dictionary<string, string?> { ["pageNumber"] = page.ToString(System.Globalization.CultureInfo.InvariantCulture), ["pageSize"] = pageSize.ToString(System.Globalization.CultureInfo.InvariantCulture), ["tenantId"] = tenantId }), null, cancellationToken).ConfigureAwait(false);
            return result?.Deserialize<ResourcePage<LibraryRecord>>() ?? new ResourcePage<LibraryRecord>();
        }
        /// <summary>Read one library record. Invisible items return 404.</summary>
        public async Task<LibraryRecord?> LibraryAsync(string kind, string id, CancellationToken cancellationToken)
        {
            JsonNode? result = await JsonAsync("GET", LibraryPath(kind) + "/" + Segment(id), null, cancellationToken).ConfigureAwait(false);
            return result?.Deserialize<LibraryRecord>();
        }
        /// <summary>Create, update or duplicate using the public library payload. Duplication preserves its source.</summary>
        public async Task<LibraryRecord?> SaveLibraryAsync(string kind, string? id, bool duplicate, JsonObject payload, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(payload);
            JsonNode? result = await JsonAsync(id == null || duplicate ? "POST" : "PUT", LibraryPath(kind) + (id == null ? "" : "/" + Segment(id) + (duplicate ? "/duplicate" : "")), payload, cancellationToken).ConfigureAwait(false);
            return result?.Deserialize<LibraryRecord>();
        }
        /// <summary>Delete an authorized library record. Never automatically retried.</summary>
        public Task<JsonNode?> DeleteLibraryAsync(string kind, string id, CancellationToken cancellationToken) { return JsonAsync("DELETE", LibraryPath(kind) + "/" + Segment(id), null, cancellationToken); }
        /// <summary>Read history metadata, with caller-supplied server filter values.</summary>
        public async Task<ResourcePage<HistoryRecord>> HistoryAsync(IDictionary<string, string?> query, CancellationToken cancellationToken)
        {
            JsonNode? result = await JsonAsync("GET", "/v1.0/api/request-history" + Query(query), null, cancellationToken).ConfigureAwait(false);
            return result?.Deserialize<ResourcePage<HistoryRecord>>() ?? new ResourcePage<HistoryRecord>();
        }
        /// <summary>Read the separately fetched history detail, including redacted captured bodies and measured phases.</summary>
        public async Task<HistoryRecord?> HistoryEntryAsync(string id, CancellationToken cancellationToken)
        {
            JsonNode? result = await JsonAsync("GET", "/v1.0/api/request-history/" + Segment(id), null, cancellationToken).ConfigureAwait(false);
            return result?.Deserialize<HistoryRecord>();
        }
        private static string LibraryPath(string kind)
        {
            if (kind is not ("system-prompts" or "model-configurations")) throw new ArgumentException("Unknown library kind.", nameof(kind));
            return "/v1.0/" + kind;
        }
    }
}
