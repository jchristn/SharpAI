namespace SharpAI.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using SharpAI.Database.Interfaces;
    using SharpAI.Models;
    using SyslogLogging;

    /// <summary>
    /// Model file service. A thin facade over the model registry data-access methods that adds
    /// storage-path resolution. Persistence is handled by the provider-specific database driver behind
    /// <see cref="IModelRegistryMethods"/>.
    /// </summary>
    public class ModelFileService
    {
        #region Private-Members

        private LoggingModule _Logging = null;
        private IModelRegistryMethods _Registry = null;
        private string _StorageDirectory = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Model file service.
        /// </summary>
        /// <param name="logging">Logging module.</param>
        /// <param name="registry">Model registry data-access methods.</param>
        /// <param name="storageDirectory">Storage directory.</param>
        public ModelFileService(LoggingModule logging, IModelRegistryMethods registry, string storageDirectory)
        {
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _StorageDirectory = !String.IsNullOrEmpty(storageDirectory) ? storageDirectory : throw new ArgumentNullException(nameof(storageDirectory));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Enumerate model files using a paginated query. This is the only list operation; there is no
        /// unbounded "get all".
        /// </summary>
        /// <param name="query">Enumeration query. When null, defaults are used.</param>
        /// <returns>Enumeration result.</returns>
        public EnumerationResult<ModelFile> Enumerate(EnumerationQuery query)
        {
            return _Registry.Enumerate(query);
        }

        /// <summary>
        /// Get by GUID.
        /// </summary>
        /// <param name="guid">GUID.</param>
        /// <returns>Instance, or null when not found.</returns>
        public ModelFile GetByGuid(Guid guid)
        {
            return _Registry.GetByGuid(guid);
        }

        /// <summary>
        /// Get by name.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <returns>Instance, or null when not found.</returns>
        public ModelFile GetByName(string name)
        {
            return _Registry.GetByName(name);
        }

        /// <summary>
        /// Retrieve the full path and filename of a given model by name.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <returns>Path and filename.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when the model file is not found.</exception>
        public string GetFilename(string name)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            ModelFile modelFile = GetByName(name);
            if (modelFile == null) throw new KeyNotFoundException("The specified model file was not found.");

            return SharpAI.Operations.GgufShards.Resolve(_StorageDirectory, modelFile.GUID);
        }

        /// <summary>
        /// Get many.
        /// </summary>
        /// <param name="guids">GUIDs.</param>
        /// <returns>List.</returns>
        public List<ModelFile> GetMany(List<Guid> guids)
        {
            return _Registry.GetMany(guids);
        }

        /// <summary>
        /// Exists by GUID.
        /// </summary>
        /// <param name="guid">GUID.</param>
        /// <returns>True if exists.</returns>
        public bool ExistsByGuid(Guid guid)
        {
            return _Registry.ExistsByGuid(guid);
        }

        /// <summary>
        /// Add.
        /// </summary>
        /// <param name="obj">ModelFile.</param>
        /// <returns>Instance.</returns>
        public ModelFile Add(ModelFile obj)
        {
            return _Registry.Add(obj);
        }

        /// <summary>
        /// Update an existing ModelFile.
        /// </summary>
        /// <param name="obj">ModelFile to update.</param>
        /// <returns>The updated instance.</returns>
        public ModelFile Update(ModelFile obj)
        {
            return _Registry.Update(obj);
        }

        /// <summary>
        /// Delete.
        /// </summary>
        /// <param name="guid">GUID.</param>
        public void Delete(Guid guid)
        {
            _Registry.Delete(guid);
        }

        #endregion
    }
}
