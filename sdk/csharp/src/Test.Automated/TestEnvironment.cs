namespace Test.Automated
{
    using System;

    /// <summary>
    /// Test environment for SharpAI SDK tests.
    /// </summary>
    public class TestEnvironment
    {
        /// <summary>
        /// SharpAI server hostname.
        /// </summary>
        public string SharpAIHostname = "localhost";

        /// <summary>
        /// SharpAI server port.
        /// </summary>
        public int SharpAIPort = 8000;

        /// <summary>
        /// Timeout in milliseconds for API calls.
        /// </summary>
        public int TimeoutMs = 300000; // 5 minutes

        /// <summary>
        /// Enable request logging.
        /// </summary>
        public bool LogRequests = false;

        /// <summary>
        /// Enable response logging.
        /// </summary>
        public bool LogResponses = false;

        /// <summary>
        /// Embeddings model to use for testing.
        /// </summary>
        public string EmbeddingsModel
        {
            get => _EmbeddingsModel;
            set => _EmbeddingsModel = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(EmbeddingsModel)));
        }

        /// <summary>
        /// Completions model to use for testing.
        /// </summary>
        public string CompletionsModel
        {
            get => _CompletionsModel;
            set => _CompletionsModel = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(CompletionsModel)));
        }

        /// <summary>
        /// Chat completions model to use for testing.
        /// </summary>
        public string ChatCompletionsModel
        {
            get => _ChatCompletionsModel;
            set => _ChatCompletionsModel = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(ChatCompletionsModel)));
        }

        /// <summary>
        /// Test environment.
        /// </summary>
        public TestEnvironment()
        {

        }

        private string _EmbeddingsModel = "leliuga/all-MiniLM-L6-v2-GGUF";
        private string _CompletionsModel = "QuantFactory/Qwen2.5-3B-GGUF";
        private string _ChatCompletionsModel = "QuantFactory/Qwen2.5-3B-GGUF";
    }
}
