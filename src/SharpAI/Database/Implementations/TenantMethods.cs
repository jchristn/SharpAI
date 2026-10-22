namespace SharpAI.Database.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;

    using SharpAI.Database.Interfaces;
    using SharpAI.Models;
    using SharpAI.Security;

    /// <summary>
    /// Handwritten-SQL implementation of tenant data access.
    /// </summary>
    internal sealed class TenantMethods : ITenantMethods
    {
        private const string _Cols = "guid, name, active, isprotected, createdutc, lastupdateutc";
        private readonly DatabaseDriverBase _Db;

        public TenantMethods(DatabaseDriverBase db)
        {
            _Db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public Tenant Create(Tenant tenant)
        {
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));
            _Db.NonQuery(
                "INSERT INTO tenants (" + _Cols + ") VALUES (@guid, @name, @active, @isprotected, @createdutc, @lastupdateutc)",
                Params(tenant));
            return tenant;
        }

        public Tenant Read(string guid)
        {
            if (String.IsNullOrEmpty(guid)) throw new ArgumentNullException(nameof(guid));
            DataTable t = _Db.Query("SELECT " + _Cols + " FROM tenants WHERE guid = @guid", One("@guid", guid));
            return t.Rows.Count > 0 ? Map(t.Rows[0]) : null;
        }

        public Tenant GetByName(string name)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            DataTable t = _Db.Query("SELECT " + _Cols + " FROM tenants WHERE name = @name", One("@name", name));
            return t.Rows.Count > 0 ? Map(t.Rows[0]) : null;
        }

        public Tenant Update(Tenant tenant)
        {
            if (tenant == null) throw new ArgumentNullException(nameof(tenant));
            tenant.LastUpdateUtc = DateTime.UtcNow;
            _Db.NonQuery(
                "UPDATE tenants SET name = @name, active = @active, isprotected = @isprotected, lastupdateutc = @lastupdateutc WHERE guid = @guid",
                Params(tenant));
            return tenant;
        }

        public void Delete(string guid)
        {
            if (String.IsNullOrEmpty(guid)) throw new ArgumentNullException(nameof(guid));
            _Db.NonQuery("DELETE FROM library_items WHERE tenantid = @guid", One("@guid", guid));
            _Db.NonQuery("DELETE FROM tenants WHERE guid = @guid", One("@guid", guid));
        }

        public EnumerationResult<Tenant> Enumerate(EnumerationQuery query)
        {
            if (query == null) query = new EnumerationQuery();
            EnumerationResult<Tenant> result = new EnumerationResult<Tenant> { MaxResults = query.PageSize, Skip = query.Offset };
            long total = Convert.ToInt64(_Db.Scalar("SELECT COUNT(*) FROM tenants", null));
            result.TotalRecords = total;
            DataTable t = _Db.Query("SELECT " + _Cols + " FROM tenants ORDER BY createdutc DESC " + _Db.BuildPaging(query.PageSize, query.Offset), null);
            List<Tenant> list = new List<Tenant>();
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

        private static Dictionary<string, object> Params(Tenant t)
        {
            return new Dictionary<string, object>
            {
                { "@guid", t.Guid },
                { "@name", t.Name },
                { "@active", t.Active ? 1 : 0 },
                { "@isprotected", t.IsProtected ? 1 : 0 },
                { "@createdutc", DbRow.Iso(t.CreatedUtc) },
                { "@lastupdateutc", DbRow.Iso(t.LastUpdateUtc) }
            };
        }

        private static Tenant Map(DataRow row)
        {
            return new Tenant
            {
                Guid = DbRow.Str(row, "guid"),
                Name = DbRow.Str(row, "name"),
                Active = DbRow.Bool(row, "active"),
                IsProtected = DbRow.Bool(row, "isprotected"),
                CreatedUtc = DbRow.Dt(row, "createdutc"),
                LastUpdateUtc = DbRow.Dt(row, "lastupdateutc")
            };
        }
    }
}
