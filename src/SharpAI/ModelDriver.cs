namespace SharpAI
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection.Metadata.Ecma335;
    using System.Runtime;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using CommunityToolkit.HighPerformance;
    using SharpAI.Database;
    using SharpAI.Engines;
    using SharpAI.Helpers;
    using SharpAI.Hosting;
    using SharpAI.Hosting.HuggingFace;
    using SharpAI.Models;
    using SharpAI.Serialization;
    using SharpAI.Services;
    using SyslogLogging;

    /// <summary>
    /// Model driver.
    /// </summary>
    public class ModelDriver
    {
        #region Public-Members

        /// <summary>
        /// HuggingFace API key.
        /// </summary>
        public string HuggingFaceApiKey
        {
            get
            {
                return _HuggingFaceApiKey;
            }
        }

        /// <summary>
        /// Model directory.
        /// </summary>
        public string ModelDirectory
        {
            get
            {
                return _ModelDirectory;
            }
        }

        #endregion

        #region Private-Members

        private string _Header = "[ModelDriver] ";
        private LoggingModule _Logging = null;
        private DatabaseDriverBase _Database = null;
        private Serializer _Serializer = null;
        private string _HuggingFaceApiKey = null;
        private string _ModelDirectory = "./models/";
        private ModelEngineService _ModelEngines = null;
        private ModelFileService _ModelFiles = null;
        private HuggingFaceClient _HuggingFace = null;


        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Model driver.
        /// </summary>
        /// <param name="logging">Logging module.</param>
        /// <param name="database">Database driver.</param>
        /// <param name="serializer">Serializer.</param>
        /// <param name="huggingFaceApiKey">HuggingFace API key.</param>
        /// <param name="modelDirectory">Model storage directory.</param>
        public ModelDriver(
            LoggingModule logging,
            DatabaseDriverBase database,
            Serializer serializer,
            string huggingFaceApiKey,
            string modelDirectory = "./models/")
        {
            if (String.IsNullOrEmpty(modelDirectory)) throw new ArgumentNullException(nameof(modelDirectory));

            modelDirectory = DirectoryHelper.NormalizeDirectory(modelDirectory);

            _Logging = logging ?? new LoggingModule();
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _HuggingFaceApiKey = huggingFaceApiKey;
            _ModelDirectory = modelDirectory;
            _ModelEngines = new ModelEngineService(_Logging);
            _ModelFiles = new ModelFileService(_Logging, _Database.Models, _ModelDirectory);
            _HuggingFace = new HuggingFaceClient(_Logging, HuggingFaceApiKey);

            SharpAI.Logging.JsonLog.Debug(_Logging, _Header + "initialized");
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Enumerate models using a paginated query. This is the only list operation; there is no
        /// unbounded "get all".
        /// </summary>
        /// <param name="query">Enumeration query. When null, defaults are used.</param>
        /// <returns>Enumeration result.</returns>
        public EnumerationResult<ModelFile> Enumerate(EnumerationQuery query)
        {
            return _ModelFiles.Enumerate(query);
        }

        /// <summary>
        /// Get by GUID.
        /// </summary>
        /// <param name="guid">GUID.</param>
        /// <returns>Instance.</returns>
        public ModelFile GetByGuid(Guid guid)
        {
            return _ModelFiles.GetByGuid(guid);
        }

        /// <summary>
        /// Get by name.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <returns>Instance.</returns>
        public ModelFile GetByName(string name)
        {
            return _ModelFiles.GetByName(name);
        }

        /// <summary>
        /// Retrieve the full path and filename of a given model by name.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <returns>Path and filename.</returns>
        public string GetFilename(string name)
        {
            return _ModelFiles.GetFilename(name);
        }

        /// <summary>
        /// Get many.
        /// </summary>
        /// <param name="guids">GUIDs.</param>
        /// <returns>List.</returns>
        public List<ModelFile> GetMany(List<Guid> guids)
        {
            return _ModelFiles.GetMany(guids);
        }

        /// <summary>
        /// Exists by GUID.
        /// </summary>
        /// <param name="guid">GUID.</param>
        /// <returns>True if exists.</returns>
        public bool ExistsByGuid(Guid guid)
        {
            return _ModelFiles.ExistsByGuid(guid);
        }

        /// <summary>
        /// Add.
        /// </summary>
        /// <param name="name">Model name.</param>
        /// <param name="quantizationPriority">Quantization priority.</param>
        /// <param name="progressCallback">Progress callback (URL, bytes downloaded, percentage 0-1).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Instance.</returns>
        public async Task<ModelFile> Add(
            string name,
            Dictionary<string, int> quantizationPriority,
            Action<string, long, decimal> progressCallback,
            CancellationToken token = default)
        {
            return await SharpAI.Operations.ModelPullService.PullAsync(_HuggingFace, _ModelFiles, _ModelDirectory,
                name, quantizationPriority, progressCallback, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete.
        /// </summary>
        /// <param name="name">Model name.</param>
        public void Delete(string name)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            ModelFile modelFile = _ModelFiles.GetByName(name);
            if (modelFile == null)
            {
                SharpAI.Logging.JsonLog.Warn(_Logging, _Header + "model " + name + " not found");
                throw new KeyNotFoundException("Model " + name + " was not found.");
            }
            else
            {
                _ModelFiles.Delete(modelFile.GUID);
                _ModelEngines.UnloadModel(SharpAI.Operations.GgufShards.Resolve(_ModelDirectory, modelFile.GUID));
                SharpAI.Operations.GgufShards.Delete(_ModelDirectory, modelFile.GUID);
                SharpAI.Logging.JsonLog.Info(_Logging, _Header + "successfully deleted model " + name + " in GUID " + modelFile.GUID);
            }
        }

        /// <summary>
        /// Get the engine for a given model.
        /// </summary>
        /// <param name="name">Model name.</param>
        /// <returns>Engine.</returns>
        public LlamaSharpEngine GetEngine(string name)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

            // Get the model file by name first
            ModelFile modelFile = _ModelFiles.GetByName(name);
            if (modelFile == null) throw new KeyNotFoundException($"Model '{name}' was not found.");

            string modelFilePath = SharpAI.Operations.GgufShards.Resolve(_ModelDirectory, modelFile.GUID);
            return _ModelEngines.GetByModelFile(modelFilePath);
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
