namespace SharpAI
{
    using System;
    using System.Collections.Generic;
    using SharpAI.Helpers;
    using SharpAI.Models;
    using SharpAI.Serialization;
    using SyslogLogging;
    using Watson.ORM.Sqlite;

    /// <summary>
    /// AI driver.
    /// </summary>
    public class AIDriver
    {
        #region Public-Members

        /// <summary>
        /// Chat APIs.
        /// </summary>
        public ChatDriver Chat
        {
            get
            {
                return _Chat;
            }
        }

        /// <summary>
        /// Commpletion APIs.
        /// </summary>
        public CompletionDriver Completion
        {
            get
            {
                return _Completion;
            }
        }

        /// <summary>
        /// Embeddings APIs.
        /// </summary>
        public EmbeddingsDriver Embeddings
        {
            get
            {
                return _Embeddings;
            }
        }

        /// <summary>
        /// Model APIs.
        /// </summary>
        public ModelDriver Models
        {
            get
            {
                return _Models;
            }
        }

        #endregion

        #region Private-Members

        private string _Header = "[AIDriver] ";
        private LoggingModule _Logging = null;
        private Serializer _Serializer = new Serializer();
        private string _DatabaseFilename = null;
        private string _HuggingFaceApiKey = null;
        private string _ModelDirectory = "./models/";
        private WatsonORM _ORM = null;

        private EmbeddingsDriver _Embeddings = null;
        private CompletionDriver _Completion = null;
        private ChatDriver _Chat = null;
        private ModelDriver _Models = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// AI driver.
        /// </summary>
        /// <param name="logging">Logging module.</param>
        /// <param name="databaseFilename">Database filename.</param>
        /// <param name="huggingFaceApiKey">HuggingFace API key.</param>
        /// <param name="modelDirectory">Model storage directory.</param>
            public AIDriver(
            LoggingModule logging,
            string databaseFilename = "./sharpai.db",
            string huggingFaceApiKey = null,
            string modelDirectory = "./models/")
        {
            if (String.IsNullOrEmpty(databaseFilename)) throw new ArgumentNullException(nameof(databaseFilename));
            if (String.IsNullOrEmpty(modelDirectory)) throw new ArgumentNullException(nameof(modelDirectory));

            modelDirectory = DirectoryHelper.NormalizeDirectory(modelDirectory);

            _Logging = logging ?? new LoggingModule();
            _DatabaseFilename = databaseFilename;
            _HuggingFaceApiKey = huggingFaceApiKey;
            _ModelDirectory = modelDirectory;

            _ORM = new WatsonORM(new DatabaseWrapper.Core.DatabaseSettings(_DatabaseFilename));
            _ORM.InitializeDatabase();
            _ORM.InitializeTables(new List<Type>
            {
                typeof(ModelFile)
            });

            _Models = new ModelDriver(_Logging, _ORM, _Serializer, _HuggingFaceApiKey, _ModelDirectory);
            _Embeddings = new EmbeddingsDriver(_Logging, _Serializer, _Models);
            _Completion = new CompletionDriver(_Logging, _Serializer, _Models);
            _Chat = new ChatDriver(_Logging, _Serializer, _Models);

            _Logging.Debug(_Header + "initialized");
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
