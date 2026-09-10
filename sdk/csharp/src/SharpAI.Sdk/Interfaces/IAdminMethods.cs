namespace SharpAI.Sdk.Interfaces
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using SharpAI.Sdk.Models.Admin;

    /// <summary>
    /// Administrative methods: settings, request history, authentication, audit, and account/RBAC
    /// management. Read methods return the parsed JSON element, or null on a non-success response.
    /// </summary>
    public interface IAdminMethods
    {
        /// <summary>Get the current server settings.</summary>
        Task<JsonElement?> GetSettingsAsync(CancellationToken cancellationToken = default);

        /// <summary>Replace the server settings.</summary>
        Task<JsonElement?> UpdateSettingsAsync(object settings, CancellationToken cancellationToken = default);

        /// <summary>List captured requests.</summary>
        Task<JsonElement?> RequestHistoryAsync(IDictionary<string, string>? query = null, CancellationToken cancellationToken = default);

        /// <summary>Summarize captured requests into time buckets.</summary>
        Task<JsonElement?> RequestHistorySummaryAsync(IDictionary<string, string>? query = null, CancellationToken cancellationToken = default);

        /// <summary>Read a captured request by identifier.</summary>
        Task<JsonElement?> RequestHistoryEntryAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>Log in and obtain a bearer session token (stored on the client).</summary>
        Task<LoginResponse?> LoginAsync(string email, string password, string? tenantGuid = null, CancellationToken cancellationToken = default);

        /// <summary>Read the current session.</summary>
        Task<JsonElement?> SessionAsync(CancellationToken cancellationToken = default);

        /// <summary>Revoke the current session (logout).</summary>
        Task<JsonElement?> LogoutAsync(CancellationToken cancellationToken = default);

        /// <summary>List security audit events.</summary>
        Task<JsonElement?> AuditAsync(IDictionary<string, string>? query = null, CancellationToken cancellationToken = default);

        /// <summary>List tenants.</summary>
        Task<JsonElement?> ListTenantsAsync(IDictionary<string, string>? query = null, CancellationToken cancellationToken = default);

        /// <summary>Create a tenant.</summary>
        Task<JsonElement?> CreateTenantAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>List users within a tenant.</summary>
        Task<JsonElement?> ListUsersAsync(string tenantGuid, IDictionary<string, string>? query = null, CancellationToken cancellationToken = default);

        /// <summary>Create a user within a tenant.</summary>
        Task<JsonElement?> CreateUserAsync(string tenantGuid, CreateUserRequest request, CancellationToken cancellationToken = default);

        /// <summary>Delete a user (cascades to credentials and assignments).</summary>
        Task<JsonElement?> DeleteUserAsync(string tenantGuid, string userGuid, CancellationToken cancellationToken = default);

        /// <summary>List credentials within a tenant.</summary>
        Task<JsonElement?> ListCredentialsAsync(string tenantGuid, IDictionary<string, string>? query = null, CancellationToken cancellationToken = default);

        /// <summary>Create a credential; the plaintext secret key is returned once.</summary>
        Task<CreatedCredential?> CreateCredentialAsync(string tenantGuid, string userGuid, string name, CancellationToken cancellationToken = default);

        /// <summary>List roles visible to a tenant (its own plus built-ins).</summary>
        Task<JsonElement?> ListRolesAsync(string tenantGuid, IDictionary<string, string>? query = null, CancellationToken cancellationToken = default);

        /// <summary>Assign a role to a user.</summary>
        Task<JsonElement?> CreateAssignmentAsync(string tenantGuid, CreateAssignmentRequest request, CancellationToken cancellationToken = default);

        /// <summary>Inspect a user's effective permissions.</summary>
        Task<JsonElement?> UserPermissionsAsync(string tenantGuid, string userGuid, CancellationToken cancellationToken = default);

        /// <summary>Inspect a credential's effective permissions.</summary>
        Task<JsonElement?> CredentialPermissionsAsync(string tenantGuid, string credentialGuid, CancellationToken cancellationToken = default);
    }
}
