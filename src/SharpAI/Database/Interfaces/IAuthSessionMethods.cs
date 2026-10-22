namespace SharpAI.Database.Interfaces
{
    using System;

    using SharpAI.Security;

    /// <summary>
    /// Data-access methods for authentication sessions. Handwritten, provider-aware SQL.
    /// </summary>
    public interface IAuthSessionMethods
    {
        /// <summary>Enumerate sessions within a tenant.</summary>
        /// <param name="tenantGuid">Tenant identifier.</param><param name="query">Paging query.</param>
        /// <returns>Session page, without bearer token material.</returns>
        SharpAI.Models.EnumerationResult<AuthSession> Enumerate(string tenantGuid, SharpAI.Models.EnumerationQuery query);

        /// <summary>Create a session.</summary>
        /// <param name="session">Session.</param>
        /// <returns>The created session.</returns>
        AuthSession Create(AuthSession session);

        /// <summary>Read a session by identifier.</summary>
        /// <param name="guid">Session identifier.</param>
        /// <returns>The session, or null.</returns>
        AuthSession Read(string guid);

        /// <summary>Revoke a session (mark inactive with a reason).</summary>
        /// <param name="guid">Session identifier.</param>
        /// <param name="reason">Revocation reason.</param>
        void Revoke(string guid, string reason);

        /// <summary>Delete a session by identifier.</summary>
        /// <param name="guid">Session identifier.</param>
        void Delete(string guid);

        /// <summary>Prune sessions that expired before the given UTC cutoff.</summary>
        /// <param name="olderThanUtc">Cutoff.</param>
        /// <returns>Number of rows deleted.</returns>
        int Prune(DateTime olderThanUtc);
    }
}
