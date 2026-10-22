namespace SharpAI.Models
{
    using System;

    /// <summary>
    /// Metadata about a model.
    /// </summary>
    public class ModelFile
    {
        #region Public-Members

        /// <summary>
        /// GUID (primary identifier).
        /// </summary>
        public Guid GUID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Name.
        /// </summary>
        public string Name { get; set; } = "Model name";

        /// <summary>
        /// Parent model name.
        /// </summary>
        public string ParentModel { get; set; } = null;

        /// <summary>
        /// Model format.
        /// </summary>
        public string Format { get; set; } = "gguf";

        /// <summary>
        /// Model family.
        /// </summary>
        public string Family { get; set; } = "llama";

        /// <summary>
        /// Content length of the file.
        /// </summary>
        public long ContentLength
        {
            get
            {
                return _ContentLength;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(ContentLength));
                _ContentLength = value;
            }
        }

        /// <summary>
        /// Parameter count.
        /// </summary>
        public long ParameterCount
        {
            get
            {
                return _ParameterCount;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(ParameterCount));
                _ParameterCount = value;
            }
        }

        /// <summary>
        /// MD5.
        /// </summary>
        public string MD5Hash { get; set; } = string.Empty;

        /// <summary>
        /// SHA1.
        /// </summary>
        public string SHA1Hash { get; set; } = null;

        /// <summary>
        /// SHA256.
        /// </summary>
        public string SHA256Hash { get; set; } = null;

        /// <summary>
        /// Source URL.
        /// </summary>
        public string SourceUrl { get; set; } = null;

        /// <summary>
        /// Parameter size.
        /// </summary>
        public string ParameterSize { get; set; } = null;

        /// <summary>
        /// Quantization.
        /// </summary>
        public string Quantization { get; set; } = null;

        /// <summary>
        /// Boolean indicating if the model can be used for embeddings.
        /// </summary>
        public bool Embeddings { get; set; } = false;

        /// <summary>
        /// Boolean indicating if the model can be used for completions.
        /// </summary>
        public bool Completions { get; set; } = false;

        /// <summary>
        /// Timestamp from the hosting provider, generally a last modified timestamp, in UTC time.
        /// </summary>
        public DateTime? ModelCreationUtc { get; set; } = null;

        /// <summary>
        /// Timestamp from creation, in UTC time.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        #endregion

        #region Private-Members

        private long _ContentLength = 0;
        private long _ParameterCount = 0;
        private static string _TimestampFormat = "yyyy-MM-ddTHH:mm:ss.ffffffzzz";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Metadata about a model.
        /// </summary>
        public ModelFile()
        {

        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Convert to an Ollama model details API object.
        /// </summary>
        /// <returns>Object.</returns>
        public object ToOllamaModelDetails()
        {
            return new
            {
                name = Name,
                model = Name,
                modified_at = ModelCreationUtc != null ? ModelCreationUtc.Value.ToString(_TimestampFormat) : CreatedUtc.ToString(_TimestampFormat),
                size = ContentLength,
                digest = SHA256Hash,
                details = new
                {
                    parent_model = ParentModel ?? string.Empty,
                    format = Format,
                    family = Family,
                    families = new[] { Family },
                    parameter_size = ParameterCount.ToString(),
                    quantization_level = Quantization
                },
                capabilities = new
                {
                    embeddings = Embeddings,
                    completions = Completions
                }
            };
        }

        #endregion
    }
}
