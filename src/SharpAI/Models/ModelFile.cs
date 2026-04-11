namespace SharpAI.Models
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using Watson.ORM.Core;

    /// <summary>
    /// Metadata about a model.
    /// </summary>
    [Table("modelfiles")]
    public class ModelFile
    {
        #region Public-Members

        /// <summary>
        /// ID.
        /// </summary>
        [Column("id", true, DataTypes.Int, false)]
        [JsonIgnore]
        public int Id
        {
            get
            {
                return _Id;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(Id));
                _Id = value;
            }
        }

        /// <summary>
        /// GUID.
        /// </summary>
        [Column("guid", false, DataTypes.Nvarchar, 64, false)]
        public Guid GUID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Name.
        /// </summary>
        [Column("modelname", false, DataTypes.Nvarchar, 128, false)]
        public string Name { get; set; } = "Model name";

        /// <summary>
        /// Parent model name.
        /// </summary>
        [Column("parentmodelname", false, DataTypes.Nvarchar, 128, true)]
        public string ParentModel { get; set; } = null;

        /// <summary>
        /// Model format.
        /// </summary>
        [Column("modelformat", false, DataTypes.Nvarchar, 16, true)]
        public string Format { get; set; } = "gguf";

        /// <summary>
        /// Model family.
        /// </summary>
        [Column("modelfamily", false, DataTypes.Nvarchar, 64, true)]
        public string Family { get; set; } = "llama";

        /// <summary>
        /// Content length of the file.
        /// </summary>
        [Column("contentlength", false, DataTypes.Long, false)]
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
        [Column("parametercount", false, DataTypes.Long, false)]
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
        [Column("md5", false, DataTypes.Nvarchar, 32, false)]
        public string MD5Hash { get; set; } = string.Empty;

        /// <summary>
        /// SHA1.
        /// </summary>
        [Column("sha1", false, DataTypes.Nvarchar, 40, false)]
        public string SHA1Hash { get; set; } = null;

        /// <summary>
        /// SHA256.
        /// </summary>
        [Column("sha256", false, DataTypes.Nvarchar, 64, false)]
        public string SHA256Hash { get; set; } = null;

        /// <summary>
        /// Source URL.
        /// </summary>
        [Column("sourceurl", false, DataTypes.Nvarchar, 1024, false)]
        public string SourceUrl { get; set; } = null;

        /// <summary>
        /// Parameter size.
        /// </summary>
        [Column("parametersize", false, DataTypes.Nvarchar, 16, true)]
        public string ParameterSize { get; set; } = null;

        /// <summary>
        /// Quantization.
        /// </summary>
        [Column("quantization", false, DataTypes.Nvarchar, 16, true)]
        public string Quantization { get; set; } = null;

        /// <summary>
        /// Boolean indicating if the model can be used for embeddings.
        /// </summary>
        [Column("embeddings", false, DataTypes.Boolean, false)]
        public bool Embeddings { get; set; } = false;

        /// <summary>
        /// Boolean indicating if the model can be used for completions.
        /// </summary>
        [Column("completions", false, DataTypes.Boolean, false)]
        public bool Completions { get; set; } = false;

        /// <summary>
        /// Timestamp from the hosting provider, generally a last modified timestamp, in UTC time.
        /// </summary>
        [Column("modelcreationutc", false, DataTypes.DateTime, 6, true)]
        public DateTime? ModelCreationUtc { get; set; } = null;

        /// <summary>
        /// Timestamp from creation, in UTC time.
        /// </summary>
        [Column("createdutc", false, DataTypes.DateTime, 6, false)]
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        #endregion

        #region Private-Members

        private int _Id = 0;
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
                modified_at = ModelCreationUtc != null ? ModelCreationUtc.Value.ToString(_TimestampFormat) : DateTime.UtcNow.ToString(_TimestampFormat),
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

        #region Private-Methods

        #endregion
    }
}
