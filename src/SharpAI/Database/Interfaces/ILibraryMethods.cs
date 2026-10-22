namespace SharpAI.Database.Interfaces
{
    using System.Collections.Generic;
    using SharpAI.Models;

    /// <summary>Provider-neutral persistence for scoped reusable prompts and generation configurations. Route handlers enforce privileges.</summary>
    public interface ILibraryMethods
    {
        /// <summary>Enumerate system defaults, selected tenant defaults and only the current user's private items; exclusions apply before paging and counts.</summary>
        EnumerationResult<LibraryItem> Enumerate(string kind,string tenantId,string userTenantId,string userId,EnumerationQuery query,IReadOnlyCollection<string> excludedIds=null);
        /// <summary>Read by immutable ID, or null. Callers must authorize before returning the result.</summary>
        LibraryItem Read(string id);
        /// <summary>Create a new authorized item.</summary>
        LibraryItem Create(LibraryItem item);
        /// <summary>Update an authorized item without changing identity.</summary>
        LibraryItem Update(LibraryItem item);
        /// <summary>Delete an authorized item.</summary>
        void Delete(string id);
    }
}
