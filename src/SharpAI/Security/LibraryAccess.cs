namespace SharpAI.Security
{
    using System;
    using SharpAI.Models;

    /// <summary>Intrinsic library ownership rules. RBAC may further restrict these rights; it never broadens private-item visibility.</summary>
    public static class LibraryAccess
    {
        /// <summary>Human user identity, or credential owner. Implicit system principals have no private library.</summary>
        public static string UserId(RequestContext actor) => actor?.PrincipalType == PrincipalTypeEnum.User ? actor.PrincipalGuid : actor?.OwnerUserGuid;
        /// <summary>Whether an item is visible in this tenant workspace.</summary>
        public static bool CanRead(LibraryItem item, RequestContext actor, string tenantId)
        {
            if (item == null || actor == null || !actor.IsAuthenticated || actor.ShouldChallenge) return false;
            return item.Scope == "System" || (item.Scope == "Tenant" && item.TenantId == tenantId)
                || (item.Scope == "User" && !String.IsNullOrEmpty(UserId(actor)) && item.UserId == UserId(actor) && item.TenantId == actor.TenantGuid);
        }
        /// <summary>Whether ownership and administrator level permit changing an item.</summary>
        public static bool CanEdit(LibraryItem item, RequestContext actor)
        {
            if (item == null || actor == null || !actor.IsAuthenticated || actor.ShouldChallenge) return false;
            if (item.Scope == "System") return actor.IsAdmin;
            if (item.Scope == "Tenant") return actor.IsAdmin || (actor.IsTenantAdmin && item.TenantId == actor.TenantGuid);
            return item.Scope == "User" && !String.IsNullOrEmpty(UserId(actor)) && item.UserId == UserId(actor) && item.TenantId == actor.TenantGuid;
        }
    }
}
