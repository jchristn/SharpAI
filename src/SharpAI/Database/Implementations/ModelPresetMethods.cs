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
    /// Handwritten-SQL implementation of the model-preset methods. Portable ANSI SQL executed through the
    /// driver's connection-agnostic helpers; nullable numerics and the stop list are stored as text so the
    /// schema is identical across providers.
    /// </summary>
    internal sealed class ModelPresetMethods : IModelPresetMethods
    {
        #region Private-Members

        private const string _AllColumns =
            "guid, name, modelname, systemprompt, temperature, maxtokens, topp, templateoverride, stopjson, " +
            "createdutc, lastupdateutc";

        private static readonly string _TimeFormat = "yyyy-MM-ddTHH:mm:ss.fffffffZ";

        private readonly DatabaseDriverBase _Db;

        #endregion

        #region Constructors-and-Factories

        public ModelPresetMethods(DatabaseDriverBase db)
        {
            _Db = db ?? throw new ArgumentNullException(nameof(db));
        }

        #endregion

        #region Public-Methods

        public EnumerationResult<ModelPreset> Enumerate(EnumerationQuery query)
        {
            if (query == null) query = new EnumerationQuery();

            EnumerationResult<ModelPreset> result = new EnumerationResult<ModelPreset>
            {
                MaxResults = query.PageSize,
                Skip = query.Offset
            };

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            string where = String.Empty;
            if (!String.IsNullOrEmpty(query.Name))
            {
                where = " WHERE name = @f_name";
                parameters["@f_name"] = query.Name;
            }

            long total = Convert.ToInt64(_Db.Scalar("SELECT COUNT(*) FROM model_presets" + where, parameters));
            result.TotalRecords = total;

            string paging = _Db.BuildPaging(query.PageSize, query.Offset);
            DataTable table = _Db.Query("SELECT " + _AllColumns + " FROM model_presets" + where + " ORDER BY createdutc DESC " + paging, parameters);
            result.Objects = MapAll(table);

            long consumed = (long)query.Offset + result.Objects.Count;
            result.RecordsRemaining = Math.Max(0, total - consumed);
            result.EndOfResults = result.RecordsRemaining <= 0;
            result.IterationsRequired = 1;
            return result;
        }

        public ModelPreset GetByGuid(Guid guid)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object> { { "@guid", guid.ToString() } };
            DataTable table = _Db.Query("SELECT " + _AllColumns + " FROM model_presets WHERE guid = @guid", parameters);
            return table.Rows.Count > 0 ? MapRow(table.Rows[0]) : null;
        }

        public ModelPreset GetByName(string name)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            Dictionary<string, object> parameters = new Dictionary<string, object> { { "@name", name } };
            DataTable table = _Db.Query("SELECT " + _AllColumns + " FROM model_presets WHERE name = @name", parameters);
            return table.Rows.Count > 0 ? MapRow(table.Rows[0]) : null;
        }

        public ModelPreset Add(ModelPreset preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));

            ModelPreset existing = GetByName(preset.Name);
            if (existing != null) return existing;

            Dictionary<string, object> parameters = ToParameters(preset);
            _Db.NonQuery(
                "INSERT INTO model_presets (" + _AllColumns + ") VALUES (" +
                "@guid, @name, @modelname, @systemprompt, @temperature, @maxtokens, @topp, @templateoverride, @stopjson, " +
                "@createdutc, @lastupdateutc)",
                parameters);

            return preset;
        }

        public ModelPreset Update(ModelPreset preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));

            preset.LastUpdateUtc = DateTime.UtcNow;
            Dictionary<string, object> parameters = ToParameters(preset);
            _Db.NonQuery(
                "UPDATE model_presets SET " +
                "name = @name, modelname = @modelname, systemprompt = @systemprompt, temperature = @temperature, " +
                "maxtokens = @maxtokens, topp = @topp, templateoverride = @templateoverride, stopjson = @stopjson, " +
                "lastupdateutc = @lastupdateutc WHERE guid = @guid",
                parameters);

            return preset;
        }

        public void Delete(Guid guid)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object> { { "@guid", guid.ToString() } };
            _Db.NonQuery("DELETE FROM model_presets WHERE guid = @guid", parameters);
        }

        #endregion

        #region Private-Methods

        private Dictionary<string, object> ToParameters(ModelPreset p)
        {
            return new Dictionary<string, object>
            {
                { "@guid", p.GUID.ToString() },
                { "@name", p.Name },
                { "@modelname", p.ModelName },
                { "@systemprompt", (object)p.SystemPrompt },
                { "@temperature", (object)(p.Temperature.HasValue ? p.Temperature.Value.ToString("R", CultureInfo.InvariantCulture) : null) },
                { "@maxtokens", (object)(p.MaxTokens.HasValue ? (object)p.MaxTokens.Value : null) },
                { "@topp", (object)(p.TopP.HasValue ? p.TopP.Value.ToString("R", CultureInfo.InvariantCulture) : null) },
                { "@templateoverride", (object)p.TemplateOverride },
                { "@stopjson", (object)(p.Stop != null && p.Stop.Count > 0 ? JsonSerializer.Serialize(p.Stop) : null) },
                { "@createdutc", p.CreatedUtc.ToUniversalTime().ToString(_TimeFormat) },
                { "@lastupdateutc", p.LastUpdateUtc.ToUniversalTime().ToString(_TimeFormat) }
            };
        }

        private List<ModelPreset> MapAll(DataTable table)
        {
            List<ModelPreset> list = new List<ModelPreset>();
            foreach (DataRow row in table.Rows) list.Add(MapRow(row));
            return list;
        }

        private ModelPreset MapRow(DataRow row)
        {
            string stopJson = GetNullableString(row, "stopjson");
            List<string> stop = new List<string>();
            if (!String.IsNullOrEmpty(stopJson))
            {
                try { stop = JsonSerializer.Deserialize<List<string>>(stopJson) ?? new List<string>(); }
                catch (JsonException) { stop = new List<string>(); }
            }

            return new ModelPreset
            {
                GUID = Guid.Parse(GetString(row, "guid")),
                Name = GetString(row, "name"),
                ModelName = GetString(row, "modelname"),
                SystemPrompt = GetNullableString(row, "systemprompt"),
                Temperature = ParseNullableFloat(GetNullableString(row, "temperature")),
                MaxTokens = ParseNullableInt(row, "maxtokens"),
                TopP = ParseNullableFloat(GetNullableString(row, "topp")),
                TemplateOverride = GetNullableString(row, "templateoverride"),
                Stop = stop,
                CreatedUtc = GetDateTime(row, "createdutc"),
                LastUpdateUtc = GetDateTime(row, "lastupdateutc")
            };
        }

        private static float? ParseNullableFloat(string value)
        {
            if (String.IsNullOrEmpty(value)) return null;
            if (Single.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)) return parsed;
            return null;
        }

        private static int? ParseNullableInt(DataRow row, string column)
        {
            object value = row[column];
            if (value == null || value == DBNull.Value) return null;
            return Convert.ToInt32(value);
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

        private static DateTime GetDateTime(DataRow row, string column)
        {
            object value = row[column];
            if (value == null || value == DBNull.Value) return DateTime.UtcNow;
            if (value is DateTime dt) return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            return DateTime.Parse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        }

        #endregion
    }
}
