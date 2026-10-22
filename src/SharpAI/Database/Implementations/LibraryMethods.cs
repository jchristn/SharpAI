namespace SharpAI.Database.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Text.Json;
    using SharpAI.Database.Interfaces;
    using SharpAI.Models;

    /// <summary>Portable parameterized persistence for scoped prompts and generation configurations. All list queries filter ownership in SQL.</summary>
    internal sealed class LibraryMethods : ILibraryMethods
    {
        private readonly DatabaseDriverBase _Db;
        private const string _Columns = "id, kind, name, description, scope, tenantid, userid, content, configurationjson, createdutc, updatedutc";
        /// <summary>Bind to a database driver.</summary>
        public LibraryMethods(DatabaseDriverBase db) { _Db = db ?? throw new ArgumentNullException(nameof(db)); }
        /// <summary>Page through system defaults, selected tenant defaults, and the caller's private items. Missing identities never match private records.</summary>
        public EnumerationResult<LibraryItem> Enumerate(string kind, string tenantId, string userTenantId, string userId, EnumerationQuery query, IReadOnlyCollection<string> excludedIds = null)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            Dictionary<string, object> p = new Dictionary<string, object> { ["@kind"] = kind, ["@tenant"] = tenantId ?? "", ["@userTenant"] = userTenantId ?? "", ["@user"] = userId ?? "" };
            string where = " WHERE kind=@kind AND (scope='System' OR (scope='Tenant' AND tenantid=@tenant) OR (scope='User' AND tenantid=@userTenant AND userid=@user AND userid<>''))";
            if (!String.IsNullOrEmpty(query.Name)) { where += " AND name=@name"; p["@name"] = query.Name; }
            if(excludedIds!=null&&excludedIds.Count>0)
            {
                List<string> names=new List<string>();
                foreach(string id in excludedIds){string key="@excluded"+names.Count;names.Add(key);p[key]=id;}
                where+=" AND id NOT IN ("+String.Join(",",names)+")";
            }
            long total = Convert.ToInt64(_Db.Scalar("SELECT COUNT(*) FROM library_items" + where, p));
            DataTable rows = _Db.Query("SELECT " + _Columns + " FROM library_items" + where + " ORDER BY name, id " + _Db.BuildPaging(query.PageSize, query.Offset), p);
            List<LibraryItem> items = new List<LibraryItem>();
            foreach (DataRow row in rows.Rows) items.Add(Map(row));
            long remaining = Math.Max(0, total - query.Offset - items.Count);
            return new EnumerationResult<LibraryItem> { Objects = items, TotalRecords = total, MaxResults = query.PageSize, Skip = query.Offset, RecordsRemaining = remaining, EndOfResults = remaining == 0 };
        }
        /// <summary>Read a record by ID. The route must check visibility before returning it.</summary>
        public LibraryItem Read(string id)
        {
            DataTable rows = _Db.Query("SELECT " + _Columns + " FROM library_items WHERE id=@id", new Dictionary<string, object> { ["@id"] = id });
            return rows.Rows.Count == 0 ? null : Map(rows.Rows[0]);
        }
        /// <summary>Persist a newly authorized item.</summary>
        public LibraryItem Create(LibraryItem item)
        {
            _Db.NonQuery("INSERT INTO library_items (" + _Columns + ") VALUES (@id,@kind,@name,@description,@scope,@tenantid,@userid,@content,@configurationjson,@createdutc,@updatedutc)", Parameters(item));
            return item;
        }
        /// <summary>Replace the mutable fields of an authorized item.</summary>
        public LibraryItem Update(LibraryItem item)
        {
            item.UpdatedUtc = DateTime.UtcNow;
            _Db.NonQuery("UPDATE library_items SET name=@name,description=@description,scope=@scope,tenantid=@tenantid,userid=@userid,content=@content,configurationjson=@configurationjson,updatedutc=@updatedutc WHERE id=@id AND kind=@kind", Parameters(item));
            return item;
        }
        /// <summary>Delete an authorized item.</summary>
        public void Delete(string id) { _Db.NonQuery("DELETE FROM library_items WHERE id=@id", new Dictionary<string, object> { ["@id"] = id }); }
        private static Dictionary<string, object> Parameters(LibraryItem i) => new Dictionary<string, object>
        {
            ["@id"]=i.Id,["@kind"]=i.Kind,["@name"]=i.Name,["@description"]=i.Description,["@scope"]=i.Scope,["@tenantid"]=i.TenantId,["@userid"]=i.UserId,
            ["@content"]=i.Content,["@configurationjson"]=i.Configuration == null ? null : JsonSerializer.Serialize(i.Configuration),
            ["@createdutc"]=i.CreatedUtc.ToUniversalTime().ToString("O"),["@updatedutc"]=i.UpdatedUtc.ToUniversalTime().ToString("O")
        };
        private static LibraryItem Map(DataRow row)
        {
            string Text(string key) => row[key] == DBNull.Value ? null : Convert.ToString(row[key], CultureInfo.InvariantCulture);
            return new LibraryItem { Id=Text("id"),Kind=Text("kind"),Name=Text("name"),Description=Text("description"),Scope=Text("scope"),TenantId=Text("tenantid"),UserId=Text("userid"),Content=Text("content"),
                Configuration=Text("configurationjson")==null ? null : JsonSerializer.Deserialize<GenerationConfiguration>(Text("configurationjson")),
                CreatedUtc=DateTime.Parse(Text("createdutc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),UpdatedUtc=DateTime.Parse(Text("updatedutc"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) };
        }
    }
}
