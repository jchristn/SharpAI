namespace SharpAI
{
    using SharpAI.Engines;
    using SharpAI.Serialization;
    using SQLitePCL;
    using SyslogLogging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.PortableExecutable;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Embeddings driver.
    /// </summary>
    public class EmbeddingsDriver
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private string _Header = "[EmbeddingsDriver] ";
        private LoggingModule _Logging = null;
        private Serializer _Serializer = null;
        private ModelDriver _Models = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Embeddings driver.
        /// </summary>
        /// <param name="logging">Logging module.</param>
        /// <param name="serializer">Serializer.</param>
        /// <param name="models">Model driver.</param>
        public EmbeddingsDriver(LoggingModule logging, Serializer serializer, ModelDriver models)
        {
            _Logging = logging ?? new LoggingModule();
            _Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _Models = models ?? throw new ArgumentNullException(nameof(models));

            SharpAI.Logging.JsonLog.Debug(_Logging, _Header + "initialized");
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Generate embeddings.
        /// </summary>
        /// <param name="model">Model.</param>
        /// <param name="input">String input.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Vectors.</returns>
        public async Task<float[]> Generate(string model, string input, CancellationToken token = default)
        {
            LlamaSharpEngine engine = GetModelEngine(model);
            return await engine.GenerateEmbeddingsAsync(input, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Generate embeddings.
        /// </summary>
        /// <param name="model">Model.</param>
        /// <param name="inputs">Array of string inputs.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Array of vectors.</returns>
        public async Task<float[][]> Generate(string model, string[] inputs, CancellationToken token = default)
        {
            LlamaSharpEngine engine = GetModelEngine(model);
            return await engine.GenerateEmbeddingsAsync(inputs, token).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private LlamaSharpEngine GetModelEngine(string model) => _Models.GetEngine(model);

        #endregion
    }
}
