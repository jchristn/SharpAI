namespace SharpAI.Database.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;

    using SharpAI.Database.Interfaces;
    using SharpAI.Models;
    using SharpAI.Security;

    /// <summary>
    /// Handwritten-SQL implementation of RBAC permission data access. Resource-type and operation-type lists
    /// are stored as JSON text arrays.
    /// </summary>
    internal sealed class PermissionMethods : IPermissionMethods
    {
        private const string _Cols = "guid, tenantguid, name, resourcetypes, operationtypes, permissiontype, active, isprotected, createdutc, lastupdateutc";
        private readonly DatabaseDriverBase _Db;

        public PermissionMethods(DatabaseDriverBase db)
        {
            _Db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public Permission Create(Permission permission)
        {
            if (permission == null) throw new ArgumentNullException(nameof(permission));
            _Db.NonQuery(
                "INSERT INTO permissions (" + _Cols + ") VALUES (@guid, @tenantguid, @name, @resourcetypes, @operationtypes, @permissiontype, @active, @isprotected, @createdutc, @lastupdateutc)",
                Params(permission));
            return permission;
        }

        public Permission Read(string guid)
        {
            if (String.IsNullOrEmpty(guid)) throw new ArgumentNullException(nameof(guid));
            DataTable t = _Db.Query("SELECT " + _Cols + " FROM permissions WHERE guid = @guid", One("@guid", guid));
            return t.Rows.Count > 0 ? Map(t.Rows[0]) : null;
        }

        public List<Permission> GetForRole(string roleGuid)
        {
            if (String.IsNullOrEmpty(roleGuid)) throw new ArgumentNullException(nameof(roleGuid));
            DataTable t = _Db.Query(
                "SELECT p.guid, p.tenantguid, p.name, p.resourcetypes, p.operationtypes, p.permissiontype, p.active, p.isprotected, p.createdutc, p.lastupdateutc " +
                "FROM permissions p INNER JOIN rolepermissionmaps rpm ON p.guid = rpm.permissionguid " +
                "WHERE rpm.roleguid = @roleguid AND p.active = 1 AND rpm.active = 1",
                One("@roleguid", roleGuid));
            List<Permission> list = new List<Permission>();
            foreach (DataRow row in t.Rows) list.Add(Map(row));
            return list;
        }

        public Permission Update(Permission permission)
        {
            permission.LastUpdateUtc = DateTime.UtcNow;
            _Db.NonQuery("UPDATE permissions SET name=@name, resourcetypes=@resourcetypes, operationtypes=@operationtypes, permissiontype=@permissiontype, active=@active, lastupdateutc=@lastupdateutc WHERE guid=@guid", Params(permission));
            return permission;
        }

        public void Delete(string guid)
        {
            if (String.IsNullOrEmpty(guid)) throw new ArgumentNullException(nameof(guid));
            _Db.NonQuery("DELETE FROM permissions WHERE guid = @guid", One("@guid", guid));
        }

        public EnumerationResult<Permission> Enumerate(string tenantGuid, EnumerationQuery query)
        {
            if (query == null) query = new EnumerationQuery();
            EnumerationResult<Permission> result = new EnumerationResult<Permission> { MaxResults = query.PageSize, Skip = query.Offset };
            Dictionary<string, object> p = new Dictionary<string, object> { { "@tenantguid", (object)tenantGuid } };
            long total = Convert.ToInt64(_Db.Scalar("SELECT COUNT(*) FROM permissions WHERE tenantguid = @tenantguid OR tenantguid IS NULL", p));
            result.TotalRecords = total;
            DataTable t = _Db.Query(
                "SELECT " + _Cols + " FROM permissions WHERE tenantguid = @tenantguid OR tenantguid IS NULL ORDER BY createdutc DESC " + _Db.BuildPaging(query.PageSize, query.Offset), p);
            List<Permission> list = new List<Permission>();
            foreach (DataRow row in t.Rows) list.Add(Map(row));
            result.Objects = list;
            result.RecordsRemaining = Math.Max(0, total - ((long)query.Offset + list.Count));
            result.EndOfResults = result.RecordsRemaining <= 0;
            return result;
        }

        private static Dictionary<string, object> One(string key, object value)
        {
            return new Dictionary<string, object> { { key, value } };
        }

        private static Dictionary<string, object> Params(Permission p)
        {
            return new Dictionary<string, object>
            {
                { "@guid", p.Guid },
                { "@tenantguid", (object)p.TenantGuid },
                { "@name", p.Name },
                { "@resourcetypes", DbRow.Json(p.ResourceTypes) },
                { "@operationtypes", DbRow.Json(p.OperationTypes) },
                { "@permissiontype", p.Effect.ToString() },
                { "@active", p.Active ? 1 : 0 },
                { "@isprotected", p.IsProtected ? 1 : 0 },
                { "@createdutc", DbRow.Iso(p.CreatedUtc) },
                { "@lastupdateutc", DbRow.Iso(p.LastUpdateUtc) }
            };
        }

        private static Permission Map(DataRow row)
        {
            Permission p = new Permission
            {
                Guid = DbRow.Str(row, "guid"),
                TenantGuid = DbRow.NullStr(row, "tenantguid"),
                Name = DbRow.Str(row, "name"),
                ResourceTypes = DbRow.StrList(row, "resourcetypes"),
                OperationTypes = DbRow.StrList(row, "operationtypes"),
                Active = DbRow.Bool(row, "active"),
                IsProtected = DbRow.Bool(row, "isprotected"),
                CreatedUtc = DbRow.Dt(row, "createdutc"),
                LastUpdateUtc = DbRow.Dt(row, "lastupdateutc")
            };

            if (Enum.TryParse<PermissionEffectEnum>(DbRow.Str(row, "permissiontype"), true, out PermissionEffectEnum effect)) p.Effect = effect;
            return p;
        }
    }
}
