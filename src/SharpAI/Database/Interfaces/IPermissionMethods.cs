namespace SharpAI.Database.Interfaces
{
    using System.Collections.Generic;

    using SharpAI.Models;
    using SharpAI.Security;

    /// <summary>
    /// Data-access methods for RBAC permissions. Handwritten, provider-aware SQL.
    /// </summary>
    public interface IPermissionMethods
    {
        /// <summary>Create a permission.</summary>
        /// <param name="permission">Permission.</param>
        /// <returns>The created permission.</returns>
        Permission Create(Permission permission);

        /// <summary>Read a permission by identifier.</summary>
        /// <param name="guid">Permission identifier.</param>
        /// <returns>The permission, or null.</returns>
        Permission Read(string guid);

        /// <summary>Get the active permissions mapped to a role.</summary>
        /// <param name="roleGuid">Role identifier.</param>
        /// <returns>Permissions; never null.</returns>
        List<Permission> GetForRole(string roleGuid);

        /// <summary>Update a custom permission.</summary>
        /// <param name="permission">Permission.</param><returns>Updated permission.</returns>
        Permission Update(Permission permission);

        /// <summary>Delete a permission by identifier.</summary>
        /// <param name="guid">Permission identifier.</param>
        void Delete(string guid);

        /// <summary>Page through permissions within a tenant.</summary>
        /// <param name="tenantGuid">Tenant identifier.</param>
        /// <param name="query">Enumeration query.</param>
        /// <returns>Enumeration result.</returns>
        EnumerationResult<Permission> Enumerate(string tenantGuid, EnumerationQuery query);
    }
}
