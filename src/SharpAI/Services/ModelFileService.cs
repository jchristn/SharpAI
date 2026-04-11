namespace SharpAI.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using DatabaseWrapper.Core;
    using ExpressionTree;
    using SharpAI.Models;
    using SyslogLogging;
    using Watson.ORM.Sqlite;
    using Watson.ORM.Core;
    using System.IO;
    using System.Collections.Concurrent;

    /// <summary>
    /// Model file service.
    /// </summary>
    public class ModelFileService
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private LoggingModule _Logging = null;
        private WatsonORM _ORM = null;
        private string _StorageDirectory = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Model file service.
        /// </summary>
        /// <param name="logging">Logging module.</param>
        /// <param name="orm">ORM.</param>
        /// <param name="storageDirectory">Storage directory.</param>
        public ModelFileService(LoggingModule logging, WatsonORM orm, string storageDirectory)
        {
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _ORM = orm ?? throw new ArgumentNullException(nameof(orm));
            _StorageDirectory = !String.IsNullOrEmpty(storageDirectory) ? storageDirectory : throw new ArgumentNullException(nameof(storageDirectory));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Retrieve all.
        /// </summary>
        /// <returns></returns>
        public List<ModelFile> All()
        {
            Expr expr = new(
                _ORM.GetColumnName<ModelFile>(nameof(ModelFile.GUID)),
                OperatorEnum.IsNotNull,
                null);

            return _ORM.SelectMany<ModelFile>(expr);
        }

        /// <summary>
        /// Enumerate.
        /// </summary>
        /// <param name="continuationToken">Continuation token.</param>
        /// <param name="maxResults">Maximum number of results to retrieve.</param>
        /// <param name="skip">The number of records to skip.</param>
        /// <param name="filter">Filters to add to the request.</param>
        /// <param name="ordering">Ordering.</param>
        /// <returns>Enumeration result.</returns>
        public EnumerationResult<ModelFile> Enumerate(
            Guid? continuationToken = null,
            int maxResults = 100,
            int skip = 0,
            Dictionary<string, string> filter = null,
            EnumerationOrderEnum ordering = EnumerationOrderEnum.CreatedDescending)
        {
            if (maxResults < 1) throw new ArgumentOutOfRangeException(nameof(maxResults));
            if (maxResults > 1000) maxResults = 1000;
            if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip));
            if (continuationToken != null && skip > 0) throw new ArgumentException("Skip count and enumeration tokens cannot be used in the same enumeration request.");

            Models.EnumerationResult<ModelFile> result = new Models.EnumerationResult<ModelFile>
            {
                MaxResults = maxResults
            };

            Expr expr = new Expr(_ORM.GetColumnName<ModelFile>(nameof(ModelFile.GUID)), OperatorEnum.IsNotNull, null);
            if (filter != null)
                foreach (KeyValuePair<string, string> kvp in filter)
                    expr = expr.PrependAnd(_ORM.GetColumnName<ModelFile>(kvp.Key), OperatorEnum.Equals, kvp.Value);

            result.TotalRecords = _ORM.Count<ModelFile>(expr);

            DateTime? lastCreated = GetCreatedUtcFromGuid(continuationToken);
            if (lastCreated != null) expr.PrependAnd(_ORM.GetColumnName<ModelFile>(nameof(ModelFile.CreatedUtc)), OperatorEnum.LessThan, lastCreated.Value);

            ResultOrder[] order = EnumerationOrderToResultOrder(ordering);

            if (skip > 0)
            {
                List<ModelFile> skippedResults = _ORM.SelectMany<ModelFile>(null, skip, expr, order);
                if (skippedResults != null && skippedResults.Count == skip)
                {
                    DateTime skipCreated = skippedResults.Min(r => r.CreatedUtc);
                    expr.PrependAnd(_ORM.GetColumnName<ModelFile>(nameof(ModelFile.CreatedUtc)), OperatorEnum.LessThan, skipCreated);
                }
            }

            result.RecordsRemaining = _ORM.Count<ModelFile>(expr);

            result.Objects = _ORM.SelectMany<ModelFile>(null, maxResults, expr, order);
            result.IterationsRequired = 1;

            if (result.Objects == null) result.Objects = new List<ModelFile>();
            result.RecordsRemaining -= result.Objects.Count;

            if (result.Objects != null
                && result.Objects.Count > 0
                && result.RecordsRemaining > 0)
            {
                result.EndOfResults = false;
                result.ContinuationToken = result.Objects.Last().GUID;
            }

            return result;
        }

        /// <summary>
        /// Get by GUID.
        /// </summary>
        /// <param name="guid">GUID.</param>
        /// <returns>Instance.</returns>
        public ModelFile GetByGuid(Guid guid)
        {
            Expr expr = new(
                _ORM.GetColumnName<ModelFile>(nameof(ModelFile.GUID)),
                OperatorEnum.Equals,
                guid);

            return _ORM.SelectFirst<ModelFile>(expr);
        }

        /// <summary>
        /// Get by name.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <returns>Instance.</returns>
        public ModelFile GetByName(string name)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            Expr expr = new(
                _ORM.GetColumnName<ModelFile>(nameof(ModelFile.Name)),
                OperatorEnum.Equals,
                name);

            return _ORM.SelectFirst<ModelFile>(expr);
        }

        /// <summary>
        /// Retrieve the full path and filename of a given model by name.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <returns>Path and filename.</returns>
        public string GetFilename(string name)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            ModelFile modelFile = GetByName(name);
            if (modelFile == null) throw new KeyNotFoundException("The specified model file was not found.");

            return Path.Combine(_StorageDirectory, modelFile.Name);
        }

        /// <summary>
        /// Get many.
        /// </summary>
        /// <param name="guids">GUIDs.</param>
        /// <returns>List.</returns>
        public List<ModelFile> GetMany(List<Guid> guids)
        {
            List<ModelFile> ret = new List<ModelFile>();

            if (guids != null && guids.Count > 0)
            {
                Expr expr = new(
                    _ORM.GetColumnName<ModelFile>(nameof(ModelFile.GUID)),
                    OperatorEnum.In,
                    guids.Distinct().ToList()
                    );

                ret = _ORM.SelectMany<ModelFile>(expr);
            }

            return ret;
        }

        /// <summary>
        /// Exists by GUID.
        /// </summary>
        /// <param name="guid">GUID.</param>
        /// <returns>True if exists.</returns>
        public bool ExistsByGuid(Guid guid)
        {
            Expr expr = new(
                _ORM.GetColumnName<ModelFile>(nameof(ModelFile.GUID)),
                OperatorEnum.Equals,
                guid);

            return _ORM.Exists<ModelFile>(expr);
        }

        /// <summary>
        /// Retrieve first.
        /// </summary>
        /// <param name="expr">Expr.</param>
        /// <returns>Instance.</returns>
        public ModelFile First(Expr expr)
        {
            if (expr == null) throw new ArgumentNullException(nameof(expr));

            return _ORM.SelectFirst<ModelFile>(expr);
        }

        /// <summary>
        /// Add.
        /// </summary>
        /// <param name="obj">ModelFile.</param>
        /// <returns>Instance.</returns>
        public ModelFile Add(ModelFile obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            ModelFile existing = GetByName(obj.Name);
            if (existing != null) return existing;

            return _ORM.Insert<ModelFile>(obj);
        }

        /// <summary>
        /// Update an existing ModelFile row.
        /// </summary>
        /// <param name="obj">ModelFile to update.</param>
        /// <returns>The updated instance.</returns>
        public ModelFile Update(ModelFile obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            return _ORM.Update<ModelFile>(obj);
        }

        /// <summary>
        /// Delete.
        /// </summary>
        /// <param name="guid">GUID.</param>
        public void Delete(Guid guid)
        {
            ModelFile existing = GetByGuid(guid);
            if (existing == null) throw new KeyNotFoundException("The specified object does not exist.");

            Expr expr = new(
                _ORM.GetColumnName<ModelFile>(nameof(ModelFile.GUID)),
                OperatorEnum.Equals,
                guid);

            _ORM.DeleteMany<ModelFile>(expr);
        }

        #endregion

        #region Private-Methods

        private DateTime? GetCreatedUtcFromGuid(Guid? guid)
        {
            if (guid != null)
            {
                Expr e = new Expr(_ORM.GetColumnName<ModelFile>(nameof(ModelFile.GUID)), OperatorEnum.Equals, guid);
                ModelFile obj = _ORM.SelectFirst<ModelFile>(e);
                return (obj != null ? obj.CreatedUtc : null);
            }
            return null;
        }

        private ResultOrder[] EnumerationOrderToResultOrder(EnumerationOrderEnum ordering)
        {
            ResultOrder[] order = new ResultOrder[1];
            order[0] = new ResultOrder(_ORM.GetColumnName<ModelFile>(nameof(ModelFile.CreatedUtc)), OrderDirectionEnum.Descending);

            switch (ordering)
            {
                case EnumerationOrderEnum.SizeAscending:
                    order[0] = new ResultOrder(_ORM.GetColumnName<ModelFile>(nameof(ModelFile.ContentLength)), OrderDirectionEnum.Ascending);
                    break;
                case EnumerationOrderEnum.SizeDescending:
                    order[0] = new ResultOrder(_ORM.GetColumnName<ModelFile>(nameof(ModelFile.ContentLength)), OrderDirectionEnum.Descending);
                    break;
                case EnumerationOrderEnum.NameAscending:
                    order[0] = new ResultOrder(_ORM.GetColumnName<ModelFile>(nameof(ModelFile.Name)), OrderDirectionEnum.Ascending);
                    break;
                case EnumerationOrderEnum.NameDescending:
                    order[0] = new ResultOrder(_ORM.GetColumnName<ModelFile>(nameof(ModelFile.Name)), OrderDirectionEnum.Descending);
                    break;
                case EnumerationOrderEnum.CreatedAscending:
                    order[0] = new ResultOrder(_ORM.GetColumnName<ModelFile>(nameof(ModelFile.CreatedUtc)), OrderDirectionEnum.Ascending);
                    break;
                case EnumerationOrderEnum.CreatedDescending:
                default:
                    break;
            }

            return order;
        }

        #endregion
    }
}
