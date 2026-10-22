namespace SharpAI.Database.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.Text.Json;

    using SharpAI.Database.Interfaces;
    using SharpAI.Models;

    /// <summary>
    /// Handwritten-SQL implementation of the request-history methods. Scalar fields are typed columns;
    /// header maps are stored as JSON (an explicit unmanaged property, since headers are schemaless).
    /// Timestamps are stored as sortable ISO-8601 UTC text. The summary is bucketed in memory so the same
    /// implementation works across all providers without dialect-specific date arithmetic.
    /// </summary>
    internal sealed class RequestHistoryMethods : IRequestHistoryMethods
    {
        #region Private-Members

        private const string _ListColumns =
            "id, tenantid, userid, principalname, method, path, url, statuscode, durationms, sourceip, " +
            "requestheadersjson, requestbodybytes, requestbodytruncated, responseheadersjson, " +
            "responsebodybytes, responsebodytruncated, createdutc, completedutc, modelname, isstreaming, inputtokens, outputtokens, cachedtokens, ttftms, streamdurationms, queuems, loadms, promptms, generationms, enginetotalms, cancelled, phasetimingsapproximate, traceid, spanid";

        private const string _AllColumns =
            "id, tenantid, userid, principalname, method, path, url, statuscode, durationms, sourceip, " +
            "requestheadersjson, requestbody, requestbodybytes, requestbodytruncated, responseheadersjson, " +
            "responsebody, responsebodybytes, responsebodytruncated, createdutc, completedutc, modelname, isstreaming, inputtokens, outputtokens, cachedtokens, ttftms, streamdurationms, queuems, loadms, promptms, generationms, enginetotalms, cancelled, phasetimingsapproximate, traceid, spanid";

        private static readonly string _TimeFormat = "yyyy-MM-ddTHH:mm:ss.fffffffZ";

        private readonly DatabaseDriverBase _Db;

        #endregion

        #region Constructors-and-Factories

        public RequestHistoryMethods(DatabaseDriverBase db)
        {
            _Db = db ?? throw new ArgumentNullException(nameof(db));
        }

        #endregion

        #region Public-Methods

        public void Create(RequestHistoryEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "@modelname", (object)entry.ModelName },
                { "@isstreaming", (object)entry.IsStreaming == null ? null : (object)(entry.IsStreaming.Value ? 1 : 0) },
                { "@inputtokens", (object)entry.InputTokens },
                { "@outputtokens", (object)entry.OutputTokens },
                { "@cachedtokens", (object)entry.CachedTokens },
                { "@ttftms", (object)entry.TtftMs },
                { "@streamdurationms", (object)entry.StreamDurationMs },
                { "@queuems", (object)entry.QueueMs },
                { "@loadms", (object)entry.LoadMs },
                { "@promptms", (object)entry.PromptMs },
                { "@generationms", (object)entry.GenerationMs },
                { "@enginetotalms", (object)entry.EngineTotalMs },
                { "@cancelled", (object)entry.Cancelled == null ? null : (object)(entry.Cancelled.Value ? 1 : 0) },
                { "@phasetimingsapproximate", (object)entry.PhaseTimingsApproximate == null ? null : (object)(entry.PhaseTimingsApproximate.Value ? 1 : 0) },
                { "@traceid", (object)entry.TraceId },
                { "@spanid", (object)entry.SpanId },
                { "@id", entry.Id },
                { "@tenantid", (object)entry.TenantId },
                { "@userid", (object)entry.UserId },
                { "@principalname", (object)entry.PrincipalName },
                { "@method", entry.Method },
                { "@path", entry.Path },
                { "@url", entry.Url },
                { "@statuscode", entry.StatusCode },
                { "@durationms", entry.DurationMs },
                { "@sourceip", (object)entry.SourceIp },
                { "@requestheadersjson", JsonSerializer.Serialize(entry.RequestHeaders) },
                { "@requestbody", (object)entry.RequestBody },
                { "@requestbodybytes", entry.RequestBodyBytes },
                { "@requestbodytruncated", entry.RequestBodyTruncated ? 1 : 0 },
                { "@responseheadersjson", JsonSerializer.Serialize(entry.ResponseHeaders) },
                { "@responsebody", (object)entry.ResponseBody },
                { "@responsebodybytes", entry.ResponseBodyBytes },
                { "@responsebodytruncated", entry.ResponseBodyTruncated ? 1 : 0 },
                { "@createdutc", entry.CreatedUtc.ToUniversalTime().ToString(_TimeFormat) },
                { "@completedutc", (object)(entry.CompletedUtc.HasValue ? entry.CompletedUtc.Value.ToUniversalTime().ToString(_TimeFormat) : null) }
            };

            _Db.NonQuery(
                "INSERT INTO request_history (" + _AllColumns + ") VALUES (" +
                "@id, @tenantid, @userid, @principalname, @method, @path, @url, @statuscode, @durationms, @sourceip, " +
                "@requestheadersjson, @requestbody, @requestbodybytes, @requestbodytruncated, @responseheadersjson, " +
                "@responsebody, @responsebodybytes, @responsebodytruncated, @createdutc, @completedutc, @modelname, @isstreaming, @inputtokens, @outputtokens, @cachedtokens, @ttftms, @streamdurationms, @queuems, @loadms, @promptms, @generationms, @enginetotalms, @cancelled, @phasetimingsapproximate, @traceid, @spanid)",
                parameters);
        }

        public RequestHistoryEntry Read(string id)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            Dictionary<string, object> parameters = new Dictionary<string, object> { { "@id", id } };
            DataTable table = _Db.Query("SELECT " + _AllColumns + " FROM request_history WHERE id = @id", parameters);
            return table.Rows.Count > 0 ? MapRow(table.Rows[0], true) : null;
        }

        public EnumerationResult<RequestHistoryEntry> Enumerate(RequestHistoryQuery query)
        {
            if (query == null) query = new RequestHistoryQuery();

            EnumerationResult<RequestHistoryEntry> result = new EnumerationResult<RequestHistoryEntry>
            {
                MaxResults = query.PageSize,
                Skip = query.Offset
            };

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            string where = BuildWhere(query, parameters);

            long total = Convert.ToInt64(_Db.Scalar("SELECT COUNT(*) FROM request_history" + where, parameters));
            result.TotalRecords = total;

            string paging = _Db.BuildPaging(query.PageSize, query.Offset);
            DataTable table = _Db.Query(
                "SELECT " + _ListColumns + " FROM request_history" + where + " ORDER BY createdutc DESC " + paging,
                parameters);

            List<RequestHistoryEntry> objects = new List<RequestHistoryEntry>();
            foreach (DataRow row in table.Rows) objects.Add(MapRow(row, false));
            result.Objects = objects;

            long consumed = (long)query.Offset + objects.Count;
            result.RecordsRemaining = Math.Max(0, total - consumed);
            result.EndOfResults = result.RecordsRemaining <= 0;
            result.IterationsRequired = 1;
            return result;
        }

        public RequestHistorySummary Summarize(RequestHistoryQuery query)
        {
            if (query == null) query = new RequestHistoryQuery();

            DateTime toUtc = query.ToUtc.HasValue ? query.ToUtc.Value.ToUniversalTime() : DateTime.UtcNow;
            DateTime fromUtc = query.FromUtc.HasValue ? query.FromUtc.Value.ToUniversalTime() : toUtc.AddHours(-1);
            if (fromUtc >= toUtc) fromUtc = toUtc.AddHours(-1);

            RequestHistoryQuery rangeQuery = new RequestHistoryQuery
            {
                TenantId = query.TenantId,
                UserId = query.UserId,
                Method = query.Method,
                StatusCode = query.StatusCode,
                PathContains = query.PathContains,
                Category = query.Category,
                FromUtc = fromUtc,
                ToUtc = toUtc
            };

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            string where = BuildWhere(rangeQuery, parameters);

            DataTable rows = _Db.Query(
                "SELECT statuscode, durationms, createdutc FROM request_history" + where, parameters);

            long bucketTicks = TimeSpan.FromMinutes(query.BucketMinutes).Ticks;
            int bucketCount = (int)Math.Max(1, Math.Ceiling((toUtc - fromUtc).TotalMinutes / query.BucketMinutes));

            RequestHistorySummary summary = new RequestHistorySummary();
            List<RequestHistoryBucket> buckets = new List<RequestHistoryBucket>();
            double[] durationSums = new double[bucketCount];

            for (int i = 0; i < bucketCount; i++)
            {
                DateTime start = fromUtc.AddMinutes((double)i * query.BucketMinutes);
                DateTime end = start.AddMinutes(query.BucketMinutes);
                if (end > toUtc) end = toUtc;
                buckets.Add(new RequestHistoryBucket { BucketStartUtc = start, BucketEndUtc = end });
            }

            foreach (DataRow row in rows.Rows)
            {
                int statusCode = (int)GetLong(row, "statuscode");
                double duration = GetDouble(row, "durationms");
                DateTime created = GetDateTime(row, "createdutc");

                int index = (int)((created.Ticks - fromUtc.Ticks) / bucketTicks);
                if (index < 0) index = 0;
                if (index >= bucketCount) index = bucketCount - 1;

                RequestHistoryBucket bucket = buckets[index];
                bool success = statusCode < 400;
                if (success) { bucket.SuccessCount++; summary.TotalSuccess++; }
                else { bucket.FailureCount++; summary.TotalFailure++; }
                durationSums[index] += duration;
                summary.TotalCount++;
                summary.AverageDurationMs += duration;
            }

            for (int i = 0; i < bucketCount; i++)
            {
                int count = buckets[i].SuccessCount + buckets[i].FailureCount;
                buckets[i].AverageDurationMs = count > 0 ? durationSums[i] / count : 0;
            }

            summary.AverageDurationMs = summary.TotalCount > 0 ? summary.AverageDurationMs / summary.TotalCount : 0;
            summary.Buckets = buckets;
            return summary;
        }

        public bool Delete(string id)
        {
            if (String.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));

            Dictionary<string, object> parameters = new Dictionary<string, object> { { "@id", id } };
            return _Db.NonQuery("DELETE FROM request_history WHERE id = @id", parameters) > 0;
        }

        public int DeleteMany(RequestHistoryQuery query)
        {
            if (query == null) query = new RequestHistoryQuery();

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            string where = BuildWhere(query, parameters);
            return _Db.NonQuery("DELETE FROM request_history" + where, parameters);
        }

        public int Prune(DateTime olderThanUtc)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "@cutoff", olderThanUtc.ToUniversalTime().ToString(_TimeFormat) }
            };
            return _Db.NonQuery("DELETE FROM request_history WHERE createdutc < @cutoff", parameters);
        }

        #endregion

        #region Private-Methods

        private string BuildWhere(RequestHistoryQuery query, Dictionary<string, object> parameters)
        {
            List<string> clauses = new List<string>();

            if (!String.IsNullOrEmpty(query.TenantId)) { clauses.Add("tenantid = @f_tenant"); parameters["@f_tenant"] = query.TenantId; }
            if (!String.IsNullOrEmpty(query.UserId)) { clauses.Add("userid = @f_user"); parameters["@f_user"] = query.UserId; }
            if (!String.IsNullOrEmpty(query.Method)) { clauses.Add("method = @f_method"); parameters["@f_method"] = query.Method; }
            if (query.StatusCode.HasValue) { clauses.Add("statuscode = @f_status"); parameters["@f_status"] = query.StatusCode.Value; }
            if (!String.IsNullOrEmpty(query.PathContains)) { clauses.Add("path LIKE @f_path"); parameters["@f_path"] = "%" + query.PathContains + "%"; }

            string[] patterns = null;
            switch (query.Category?.Trim().ToLowerInvariant())
            {
                // Preserve the existing combined category for API consumers.
                case "inference":
                    patterns = new string[] { "/api/chat%", "/api/generate%", "/api/embed%", "/v1/chat/completions%", "/v1/completions%", "/v1/embeddings%" };
                    break;
                case "generation":
                    patterns = new string[] { "/api/chat", "/api/generate", "/v1/chat/completions", "/v1/completions" };
                    break;
                case "embeddings":
                    patterns = new string[] { "/api/embed", "/api/embeddings", "/v1/embeddings" };
                    break;
            }
            if (patterns != null)
            {
                List<string> ors = new List<string>();
                for (int i = 0; i < patterns.Length; i++)
                {
                    string key = "@f_cat" + i;
                    ors.Add((query.Category.Trim().Equals("inference", StringComparison.OrdinalIgnoreCase) ? "path LIKE " : "path = ") + key);
                    parameters[key] = patterns[i];
                }
                clauses.Add("(" + String.Join(" OR ", ors) + ")");
            }

            if (query.FromUtc.HasValue) { clauses.Add("createdutc >= @f_from"); parameters["@f_from"] = query.FromUtc.Value.ToUniversalTime().ToString(_TimeFormat); }
            if (query.ToUtc.HasValue) { clauses.Add("createdutc < @f_to"); parameters["@f_to"] = query.ToUtc.Value.ToUniversalTime().ToString(_TimeFormat); }

            return clauses.Count > 0 ? " WHERE " + String.Join(" AND ", clauses) : String.Empty;
        }

        private RequestHistoryEntry MapRow(DataRow row, bool includeBodies)
        {
            RequestHistoryEntry entry = new RequestHistoryEntry
            {
                ModelName = row.IsNull("modelname") ? null : (string)(Convert.ToString(row["modelname"])),
                IsStreaming = row.IsNull("isstreaming") ? null : (bool?)(Convert.ToInt32(row["isstreaming"]) != 0),
                InputTokens = row.IsNull("inputtokens") ? null : (int?)(Convert.ToInt32(row["inputtokens"])),
                OutputTokens = row.IsNull("outputtokens") ? null : (int?)(Convert.ToInt32(row["outputtokens"])),
                CachedTokens = row.IsNull("cachedtokens") ? null : (int?)(Convert.ToInt32(row["cachedtokens"])),
                TtftMs = row.IsNull("ttftms") ? null : (double?)(Convert.ToDouble(row["ttftms"])),
                StreamDurationMs = row.IsNull("streamdurationms") ? null : (double?)(Convert.ToDouble(row["streamdurationms"])),
                QueueMs = row.IsNull("queuems") ? null : (double?)(Convert.ToDouble(row["queuems"])),
                LoadMs = row.IsNull("loadms") ? null : (double?)(Convert.ToDouble(row["loadms"])),
                PromptMs = row.IsNull("promptms") ? null : (double?)(Convert.ToDouble(row["promptms"])),
                GenerationMs = row.IsNull("generationms") ? null : (double?)(Convert.ToDouble(row["generationms"])),
                EngineTotalMs = row.IsNull("enginetotalms") ? null : (double?)(Convert.ToDouble(row["enginetotalms"])),
                Cancelled = row.IsNull("cancelled") ? null : (bool?)(Convert.ToInt32(row["cancelled"]) != 0),
                PhaseTimingsApproximate = row.IsNull("phasetimingsapproximate") ? null : (bool?)(Convert.ToInt32(row["phasetimingsapproximate"]) != 0),
                TraceId = row.IsNull("traceid") ? null : (string)(Convert.ToString(row["traceid"])),
                SpanId = row.IsNull("spanid") ? null : (string)(Convert.ToString(row["spanid"])),
                Id = GetString(row, "id"),
                TenantId = GetNullableString(row, "tenantid"),
                UserId = GetNullableString(row, "userid"),
                PrincipalName = GetNullableString(row, "principalname"),
                Method = GetString(row, "method"),
                Path = GetString(row, "path"),
                Url = GetString(row, "url"),
                StatusCode = (int)GetLong(row, "statuscode"),
                DurationMs = GetDouble(row, "durationms"),
                SourceIp = GetNullableString(row, "sourceip"),
                RequestHeaders = DeserializeHeaders(GetNullableString(row, "requestheadersjson")),
                RequestBodyBytes = GetLong(row, "requestbodybytes"),
                RequestBodyTruncated = GetLong(row, "requestbodytruncated") != 0,
                ResponseHeaders = DeserializeHeaders(GetNullableString(row, "responseheadersjson")),
                ResponseBodyBytes = GetLong(row, "responsebodybytes"),
                ResponseBodyTruncated = GetLong(row, "responsebodytruncated") != 0,
                CreatedUtc = GetDateTime(row, "createdutc"),
                CompletedUtc = GetNullableDateTime(row, "completedutc")
            };

            if (includeBodies)
            {
                entry.RequestBody = GetNullableString(row, "requestbody");
                entry.ResponseBody = GetNullableString(row, "responsebody");
            }

            return entry;
        }

        private static Dictionary<string, string> DeserializeHeaders(string json)
        {
            if (String.IsNullOrEmpty(json)) return new Dictionary<string, string>();
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        private static string GetString(DataRow row, string column)
        {
            object value = row[column];
            return value == null || value == DBNull.Value ? String.Empty : value.ToString();
        }

        private static string GetNullableString(DataRow row, string column)
        {
            object value = row[column];
            return value == null || value == DBNull.Value ? null : value.ToString();
        }

        private static long GetLong(DataRow row, string column)
        {
            object value = row[column];
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt64(value);
        }

        private static double GetDouble(DataRow row, string column)
        {
            object value = row[column];
            return value == null || value == DBNull.Value ? 0 : Convert.ToDouble(value);
        }

        private static DateTime GetDateTime(DataRow row, string column)
        {
            object value = row[column];
            if (value == null || value == DBNull.Value) return DateTime.UtcNow;
            if (value is DateTime dt) return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            return DateTime.Parse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        }

        private static DateTime? GetNullableDateTime(DataRow row, string column)
        {
            object value = row[column];
            if (value == null || value == DBNull.Value) return null;
            if (value is DateTime dt) return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            return DateTime.Parse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        }

        #endregion
    }
}
