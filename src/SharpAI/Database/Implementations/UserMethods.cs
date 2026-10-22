namespace SharpAI.Database.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;

    using SharpAI.Database.Interfaces;
    using SharpAI.Models;
    using SharpAI.Security;

    /// <summary>
    /// Handwritten-SQL implementation of user data access. Enumeration is tenant-scoped.
    /// </summary>
    internal sealed class UserMethods : IUserMethods
    {
        private const string _Cols =
            "guid, tenantguid, firstname, lastname, email, passwordsha256, isadmin, istenantadmin, " +
            "active, isprotected, createdutc, lastupdateutc";

        private readonly DatabaseDriverBase _Db;

        public UserMethods(DatabaseDriverBase db)
        {
            _Db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public User Create(User user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            _Db.NonQuery(
                "INSERT INTO users (" + _Cols + ") VALUES (@guid, @tenantguid, @firstname, @lastname, @email, " +
                "@passwordsha256, @isadmin, @istenantadmin, @active, @isprotected, @createdutc, @lastupdateutc)",
                Params(user));
            return user;
        }

        public User Read(string guid)
        {
            if (String.IsNullOrEmpty(guid)) throw new ArgumentNullException(nameof(guid));
            DataTable t = _Db.Query("SELECT " + _Cols + " FROM users WHERE guid = @guid", One("@guid", guid));
            return t.Rows.Count > 0 ? Map(t.Rows[0]) : null;
        }

        public User GetByEmail(string tenantGuid, string email)
        {
            if (String.IsNullOrEmpty(email)) throw new ArgumentNullException(nameof(email));
            Dictionary<string, object> p = new Dictionary<string, object> { { "@tenantguid", tenantGuid }, { "@email", email } };
            DataTable t = _Db.Query("SELECT " + _Cols + " FROM users WHERE tenantguid = @tenantguid AND email = @email", p);
            return t.Rows.Count > 0 ? Map(t.Rows[0]) : null;
        }

        public User Update(User user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            user.LastUpdateUtc = DateTime.UtcNow;
            _Db.NonQuery(
                "UPDATE users SET tenantguid = @tenantguid, firstname = @firstname, lastname = @lastname, email = @email, " +
                "passwordsha256 = @passwordsha256, isadmin = @isadmin, istenantadmin = @istenantadmin, active = @active, " +
                "isprotected = @isprotected, lastupdateutc = @lastupdateutc WHERE guid = @guid",
                Params(user));
            return user;
        }

        public void Delete(string guid)
        {
            if (String.IsNullOrEmpty(guid)) throw new ArgumentNullException(nameof(guid));
            _Db.NonQuery("DELETE FROM library_items WHERE scope = 'User' AND userid = @guid", One("@guid", guid));
            _Db.NonQuery("DELETE FROM users WHERE guid = @guid", One("@guid", guid));
        }

        public EnumerationResult<User> Enumerate(string tenantGuid, EnumerationQuery query)
        {
            if (query == null) query = new EnumerationQuery();
            EnumerationResult<User> result = new EnumerationResult<User> { MaxResults = query.PageSize, Skip = query.Offset };
            Dictionary<string, object> p = new Dictionary<string, object>();
            string where = String.Empty;
            if (!String.IsNullOrEmpty(tenantGuid)) { where = " WHERE tenantguid = @tenantguid"; p["@tenantguid"] = tenantGuid; }

            long total = Convert.ToInt64(_Db.Scalar("SELECT COUNT(*) FROM users" + where, p));
            result.TotalRecords = total;
            DataTable t = _Db.Query("SELECT " + _Cols + " FROM users" + where + " ORDER BY createdutc DESC " + _Db.BuildPaging(query.PageSize, query.Offset), p);
            List<User> list = new List<User>();
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

        private static Dictionary<string, object> Params(User u)
        {
            return new Dictionary<string, object>
            {
                { "@guid", u.Guid },
                { "@tenantguid", u.TenantGuid },
                { "@firstname", u.FirstName },
                { "@lastname", u.LastName },
                { "@email", u.Email },
                { "@passwordsha256", u.PasswordSha256 },
                { "@isadmin", u.IsAdmin ? 1 : 0 },
                { "@istenantadmin", u.IsTenantAdmin ? 1 : 0 },
                { "@active", u.Active ? 1 : 0 },
                { "@isprotected", u.IsProtected ? 1 : 0 },
                { "@createdutc", DbRow.Iso(u.CreatedUtc) },
                { "@lastupdateutc", DbRow.Iso(u.LastUpdateUtc) }
            };
        }

        private static User Map(DataRow row)
        {
            return new User
            {
                Guid = DbRow.Str(row, "guid"),
                TenantGuid = DbRow.Str(row, "tenantguid"),
                FirstName = DbRow.Str(row, "firstname"),
                LastName = DbRow.Str(row, "lastname"),
                Email = DbRow.Str(row, "email"),
                PasswordSha256 = DbRow.Str(row, "passwordsha256"),
                IsAdmin = DbRow.Bool(row, "isadmin"),
                IsTenantAdmin = DbRow.Bool(row, "istenantadmin"),
                Active = DbRow.Bool(row, "active"),
                IsProtected = DbRow.Bool(row, "isprotected"),
                CreatedUtc = DbRow.Dt(row, "createdutc"),
                LastUpdateUtc = DbRow.Dt(row, "lastupdateutc")
            };
        }
    }
}
