namespace SharpAI.Sdk.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using SharpAI.Sdk.Interfaces;
    using SharpAI.Sdk.Models.Admin;

    /// <summary>
    /// Implementation of administrative methods: settings, request history, authentication, audit, and
    /// account/RBAC management.
    /// </summary>
    public class AdminMethods : IAdminMethods
    {
        private readonly SharpAISdk _Sdk;

        /// <summary>
        /// Initialize the administrative methods.
        /// </summary>
        /// <param name="sdk">Owning SDK instance.</param>
        public AdminMethods(SharpAISdk sdk)
        {
            _Sdk = sdk ?? throw new ArgumentNullException(nameof(sdk));
        }

        private string Url(string path, IDictionary<string, string>? query = null)
        {
            string url = _Sdk.Endpoint + path;
            if (query != null)
            {
                List<string> parts = new List<string>();
                foreach (KeyValuePair<string, string> kv in query)
                {
                    if (!string.IsNullOrEmpty(kv.Value))
                        parts.Add(Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value));
                }
                if (parts.Count > 0) url += "?" + string.Join("&", parts);
            }
            return url;
        }

        /// <inheritdoc />
        public Task<JsonElement?> GetSettingsAsync(CancellationToken cancellationToken = default)
            => _Sdk.SendAsync<JsonElement?>(HttpMethod.Get, Url("/api/settings"), null, null, cancellationToken);

        /// <inheritdoc />
        public Task<JsonElement?> UpdateSettingsAsync(object settings, CancellationToken cancellationToken = default)
            => _Sdk.SendAsync<JsonElement?>(HttpMethod.Put, Url("/api/settings"), settings, null, cancellationToken);

        /// <inheritdoc />
        public Task<JsonElement?> RequestHistoryAsync(IDictionary<string, string>? query = null, CancellationToken cancellationToken = default)
            => _Sdk.SendAsync<JsonElement?>(HttpMethod.Get, Url("/v1.0/api/request-history", query), null, null, cancellationToken);

        /// <inheritdoc />
        public Task<JsonElement?> RequestHistorySummaryAsync(IDictionary<string, string>? query = null, CancellationToken cancellationToken = default)
            => _Sdk.SendAsync<JsonElement?>(HttpMethod.Get, Url("/v1.0/api/request-history/summary", query), null, null, cancellationToken);

        /// <inheritdoc />
        public Task<JsonElement?> RequestHistoryEntryAsync(string id, CancellationToken cancellationToken = default)
            => _Sdk.SendAsync<JsonElement?>(HttpMethod.Get, Url("/v1.0/api/request-history/" + Uri.EscapeDataString(id)), null, null, cancellationToken);

        /// <inheritdoc />
        public async Task<LoginResponse?> LoginAsync(string email, string password, string? tenantGuid = null, CancellationToken cancellationToken = default)
        {
            Dictionary<string, string> headers = new Dictionary<string, string> { { "x-email", email }, { "x-password", password } };
            if (!string.IsNullOrEmpty(tenantGuid)) headers["x-tenant-guid"] = tenantGuid!;
            LoginResponse? result = await _Sdk.SendAsync<LoginResponse>(HttpMethod.Post, Url("/v1.0/token"), null, headers, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(result?.Token)) _Sdk.Token = result!.Token;
            return result;
        }

        /// <inheritdoc />
        public Task<JsonElement?> SessionAsync(CancellationToken cancellationToken = default)
            => _Sdk.SendAsync<JsonElement?>(HttpMethod.Get, Url("/v1.0/token"), null, null, cancellationToken);

        /// <inheritdoc />
        public async Task<JsonElement?> LogoutAsync(CancellationToken cancellationToken = default)
        {
            JsonElement? result = await _Sdk.SendAsync<JsonElement?>(HttpMethod.Delete, Url("/v1.0/token"), null, null, cancellationToken).ConfigureAwait(false);
            _Sdk.Token = null;
            return result;
        }

        /// <inheritdoc />
        public Task<JsonElement?> AuditAsync(IDictionary<string, string>? query = null, CancellationToken cancellationToken = default)
            => _Sdk.SendAsync<JsonElement?>(HttpMethod.Get, Url("/v1.0/api/audit", query), null, null, cancellationToken);

        /// <inheritdoc />
        public Task<JsonElement?> ListTenantsAsync(IDictionary<string, string>? query = null, CancellationToken cancellationToken = default)
            => _Sdk.SendAsync<JsonElement?>(HttpMethod.Get, Url("/v1.0/tenants", query), null, null, cancellationToken);

        /// <inheritdoc />
        public Task<JsonElement?> CreateTenantAsync(string name, CancellationToken cancellationToken = default)
            => _Sdk.SendAsync<JsonElement?>(HttpMethod.Post, Url("/v1.0/tenants"), new { Name = name }, null, cancellationToken);

        /// <inheritdoc />
        public Task<JsonElement?> ListUsersAsync(string tenantGuid, IDictionary<string, string>? query = null, CancellationToken cancellationToken = default)
            => _Sdk.SendAsync<JsonElement?>(HttpMethod.Get, Url($"/v1.0/tenants/{tenantGuid}/users", query), null, null, cancellationToken);

        /// <inheritdoc />
        public Task<JsonElement?> CreateUserAsync(string tenantGuid, CreateUserRequest request, CancellationToken cancellationToken = default)
            => _Sdk.SendAsync<JsonElement?>(HttpMethod.Post, Url($"/v1.0/tenants/{tenantGuid}/users"), request, null, cancellationToken);

        /// <inheritdoc />
        public Task<JsonElement?> DeleteUserAsync(string tenantGuid, string userGuid, CancellationToken cancellationToken = default)
            => _Sdk.SendAsync<JsonElement?>(HttpMethod.Delete, Url($"/v1.0/tenants/{tenantGuid}/users/{userGuid}"), null, null, cancellationToken);

        /// <inheritdoc />
        public Task<JsonElement?> ListCredentialsAsync(string tenantGuid, IDictionary<string, string>? query = null, CancellationToken cancellationToken = default)
            => _Sdk.SendAsync<JsonElement?>(HttpMethod.Get, Url($"/v1.0/tenants/{tenantGuid}/credentials", query), null, null, cancellationToken);

        /// <inheritdoc />
        public Task<CreatedCredential?> CreateCredentialAsync(string tenantGuid, string userGuid, string name, CancellationToken cancellationToken = default)
            => _Sdk.SendAsync<CreatedCredential>(HttpMethod.Post, Url($"/v1.0/tenants/{tenantGuid}/credentials"), new { UserGuid = userGuid, Name = name }, null, cancellationToken);

        /// <inheritdoc />
        public Task<JsonElement?> ListRolesAsync(string tenantGuid, IDictionary<string, string>? query = null, CancellationToken cancellationToken = default)
            => _Sdk.SendAsync<JsonElement?>(HttpMethod.Get, Url($"/v1.0/tenants/{tenantGuid}/roles", query), null, null, cancellationToken);

        /// <inheritdoc />
        public Task<JsonElement?> CreateAssignmentAsync(string tenantGuid, CreateAssignmentRequest request, CancellationToken cancellationToken = default)
            => _Sdk.SendAsync<JsonElement?>(HttpMethod.Post, Url($"/v1.0/tenants/{tenantGuid}/assignments"), request, null, cancellationToken);

        /// <inheritdoc />
        public Task<JsonElement?> UserPermissionsAsync(string tenantGuid, string userGuid, CancellationToken cancellationToken = default)
            => _Sdk.SendAsync<JsonElement?>(HttpMethod.Get, Url($"/v1.0/tenants/{tenantGuid}/users/{userGuid}/permissions"), null, null, cancellationToken);

        /// <inheritdoc />
        public Task<JsonElement?> CredentialPermissionsAsync(string tenantGuid, string credentialGuid, CancellationToken cancellationToken = default)
            => _Sdk.SendAsync<JsonElement?>(HttpMethod.Get, Url($"/v1.0/tenants/{tenantGuid}/credentials/{credentialGuid}/permissions"), null, null, cancellationToken);
    }
}
