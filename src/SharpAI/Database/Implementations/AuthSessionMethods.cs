namespace SharpAI.Database.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;

    using SharpAI.Database.Interfaces;
    using SharpAI.Security;

    /// <summary>
    /// Handwritten-SQL implementation of authentication session data access.
    /// </summary>
    internal sealed class AuthSessionMethods : IAuthSessionMethods
    {
        private const string _Cols =
            "guid, userguid, tenantguid, principaltype, active, isprotected, createdutc, expiresutc, " +
            "lastusedutc, revokedutc, revocationreason";

        private readonly DatabaseDriverBase _Db;

        public AuthSessionMethods(DatabaseDriverBase db)
        {
            _Db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public AuthSession Create(AuthSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            _Db.NonQuery(
                "INSERT INTO authsessions (" + _Cols + ") VALUES (@guid, @userguid, @tenantguid, @principaltype, " +
                "@active, @isprotected, @createdutc, @expiresutc, @lastusedutc, @revokedutc, @revocationreason)",
                Params(session));
            return session;
        }

        public AuthSession Read(string guid)
        {
            if (String.IsNullOrEmpty(guid)) throw new ArgumentNullException(nameof(guid));
            DataTable t = _Db.Query("SELECT " + _Cols + " FROM authsessions WHERE guid = @guid", One("@guid", guid));
            return t.Rows.Count > 0 ? Map(t.Rows[0]) : null;
        }

        public void Revoke(string guid, string reason)
        {
            if (String.IsNullOrEmpty(guid)) throw new ArgumentNullException(nameof(guid));
            Dictionary<string, object> p = new Dictionary<string, object>
            {
                { "@guid", guid },
                { "@revokedutc", DbRow.Iso(DateTime.UtcNow) },
                { "@revocationreason", (object)reason }
            };
            _Db.NonQuery("UPDATE authsessions SET active = 0, revokedutc = @revokedutc, revocationreason = @revocationreason WHERE guid = @guid", p);
        }

        public void Delete(string guid)
        {
            if (String.IsNullOrEmpty(guid)) throw new ArgumentNullException(nameof(guid));
            _Db.NonQuery("DELETE FROM authsessions WHERE guid = @guid", One("@guid", guid));
        }

        public SharpAI.Models.EnumerationResult<AuthSession> Enumerate(string tenantGuid, SharpAI.Models.EnumerationQuery query)
        {
            if (query == null) query = new SharpAI.Models.EnumerationQuery();
            SharpAI.Models.EnumerationResult<AuthSession> result = new SharpAI.Models.EnumerationResult<AuthSession> { MaxResults = query.PageSize, Skip = query.Offset };
            Dictionary<string, object> p = new Dictionary<string, object>();
            string where = String.Empty;
            if (!String.IsNullOrEmpty(tenantGuid)) { where = " WHERE tenantguid = @tenantguid"; p["@tenantguid"] = tenantGuid; }

            long total = Convert.ToInt64(_Db.Scalar("SELECT COUNT(*) FROM authsessions" + where, p));
            result.TotalRecords = total;
            DataTable t = _Db.Query("SELECT " + _Cols + " FROM authsessions" + where + " ORDER BY createdutc DESC " + _Db.BuildPaging(query.PageSize, query.Offset), p);
            List<AuthSession> list = new List<AuthSession>();
            foreach (DataRow row in t.Rows) list.Add(Map(row));
            result.Objects = list;
            result.RecordsRemaining = Math.Max(0, total - ((long)query.Offset + list.Count));
            result.EndOfResults = result.RecordsRemaining <= 0;
            return result;
        }

        public int Prune(DateTime olderThanUtc)
        {
            return _Db.NonQuery("DELETE FROM authsessions WHERE expiresutc < @cutoff", One("@cutoff", DbRow.Iso(olderThanUtc)));
        }

        private static Dictionary<string, object> One(string key, object value)
        {
            return new Dictionary<string, object> { { key, value } };
        }

        private static Dictionary<string, object> Params(AuthSession s)
        {
            return new Dictionary<string, object>
            {
                { "@guid", s.Guid },
                { "@userguid", s.UserGuid },
                { "@tenantguid", s.TenantGuid },
                { "@principaltype", s.PrincipalType.ToString() },
                { "@active", s.Active ? 1 : 0 },
                { "@isprotected", s.IsProtected ? 1 : 0 },
                { "@createdutc", DbRow.Iso(s.CreatedUtc) },
                { "@expiresutc", DbRow.Iso(s.ExpiresUtc) },
                { "@lastusedutc", DbRow.IsoOrNull(s.LastUsedUtc) },
                { "@revokedutc", DbRow.IsoOrNull(s.RevokedUtc) },
                { "@revocationreason", (object)s.RevocationReason }
            };
        }

        private static AuthSession Map(DataRow row)
        {
            AuthSession s = new AuthSession
            {
                Guid = DbRow.Str(row, "guid"),
                UserGuid = DbRow.Str(row, "userguid"),
                TenantGuid = DbRow.Str(row, "tenantguid"),
                Active = DbRow.Bool(row, "active"),
                IsProtected = DbRow.Bool(row, "isprotected"),
                CreatedUtc = DbRow.Dt(row, "createdutc"),
                ExpiresUtc = DbRow.Dt(row, "expiresutc"),
                LastUsedUtc = DbRow.NullDt(row, "lastusedutc"),
                RevokedUtc = DbRow.NullDt(row, "revokedutc"),
                RevocationReason = DbRow.NullStr(row, "revocationreason")
            };

            if (Enum.TryParse<PrincipalTypeEnum>(DbRow.Str(row, "principaltype"), true, out PrincipalTypeEnum pt)) s.PrincipalType = pt;
            return s;
        }
    }
}
