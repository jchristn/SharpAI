namespace Test.Automated
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using SharpAI.Models.Ollama;
    using SharpAI.Models.OpenAI;
    using SharpAI.Sdk;

    internal static class Helpers
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8603 // Possible null reference return.

        #region Ollama-Request-Creation

        /// <summary>
        /// Create Ollama embeddings request for single input.
        /// </summary>
        /// <param name="model">Model name.</param>
        /// <param name="input">Input text.</param>
        /// <returns>Ollama embeddings request.</returns>
        internal static OllamaGenerateEmbeddingsRequest CreateOllamaSingleEmbeddingsRequest(string model, string input)
        {
            var request = new OllamaGenerateEmbeddingsRequest
            {
                Model = model
            };
            request.SetInput(input);
            return request;
        }

        /// <summary>
        /// Create Ollama embeddings request for multiple inputs.
        /// </summary>
        /// <param name="model">Model name.</param>
        /// <param name="inputs">Input texts.</param>
        /// <returns>Ollama embeddings request.</returns>
        internal static OllamaGenerateEmbeddingsRequest CreateOllamaMultipleEmbeddingsRequest(string model, List<string> inputs)
        {
            var request = new OllamaGenerateEmbeddingsRequest
            {
                Model = model
            };
            request.SetInputs(inputs);
            return request;
        }

        #endregion

        #region OpenAI-Request-Creation

        /// <summary>
        /// Create OpenAI embeddings request for single input.
        /// </summary>
        /// <param name="model">Model name.</param>
        /// <param name="input">Input text.</param>
        /// <returns>OpenAI embeddings request.</returns>
        internal static OpenAIGenerateEmbeddingsRequest CreateOpenAISingleEmbeddingsRequest(string model, string input)
        {
            var request = new OpenAIGenerateEmbeddingsRequest
            {
                Model = model
            };
            request.Input = input;
            return request;
        }

        /// <summary>
        /// Create OpenAI embeddings request for multiple inputs.
        /// </summary>
        /// <param name="model">Model name.</param>
        /// <param name="inputs">Input texts.</param>
        /// <returns>OpenAI embeddings request.</returns>
        internal static OpenAIGenerateEmbeddingsRequest CreateOpenAIMultipleEmbeddingsRequest(string model, List<string> inputs)
        {
            var request = new OpenAIGenerateEmbeddingsRequest
            {
                Model = model
            };
            request.Input = inputs.ToArray();
            return request;
        }

        /// <summary>
        /// Create Ollama completion request.
        /// </summary>
        /// <param name="model">Model name.</param>
        /// <param name="prompt">Prompt text.</param>
        /// <param name="stream">Whether to stream the response.</param>
        /// <returns>Ollama completion request.</returns>
        internal static OllamaGenerateCompletionRequest CreateOllamaCompletionRequest(string model, string prompt, bool stream = false)
        {
            return new OllamaGenerateCompletionRequest
            {
                Model = model,
                Prompt = prompt,
                Stream = stream,
                Options = new OllamaCompletionOptions
                {
                    NumKeep = 5,
                    Seed = 42,
                    NumPredict = 100,
                    TopK = 20,
                    TopP = 0.9f,
                    MinP = 0.0f,
                    TfsZ = 0.5f,
                    TypicalP = 0.7f,
                    RepeatLastN = 33,
                    Temperature = 0.8f,
                    RepeatPenalty = 1.2f,
                    PresencePenalty = 1.5f,
                    FrequencyPenalty = 1.0f,
                    Mirostat = 1,
                    MirostatTau = 0.8f,
                    MirostatEta = 0.6f,
                    PenalizeNewline = true,
                    Stop = new List<string> { "\n", "user:" },
                    Numa = false,
                    NumCtx = 1024,
                    NumBatch = 2,
                    NumGpu = 1,
                    MainGpu = 0,
                    LowVram = false,
                    F16Kv = true,
                    VocabOnly = false,
                    UseMmap = true,
                    UseMlock = false,
                    NumThread = 8
                }
            };
        }

        /// <summary>
        /// Create Ollama chat completion request.
        /// </summary>
        /// <param name="model">Model name.</param>
        /// <param name="messages">Chat messages.</param>
        /// <param name="stream">Whether to stream the response.</param>
        /// <returns>Ollama chat completion request.</returns>
        internal static OllamaGenerateChatCompletionRequest CreateOllamaChatCompletionRequest(string model, List<OllamaChatMessage> messages, bool stream = false)
        {
            return new OllamaGenerateChatCompletionRequest
            {
                Model = model,
                Messages = messages,
                Stream = stream,
                Options = new OllamaCompletionOptions
                {
                    NumKeep = 5,
                    Seed = 42,
                    NumPredict = 100,
                    TopK = 20,
                    TopP = 0.9f,
                    MinP = 0.0f,
                    TfsZ = 0.5f,
                    TypicalP = 0.7f,
                    RepeatLastN = 33,
                    Temperature = 0.8f,
                    RepeatPenalty = 1.2f,
                    PresencePenalty = 1.5f,
                    FrequencyPenalty = 1.0f,
                    Mirostat = 1,
                    MirostatTau = 0.8f,
                    MirostatEta = 0.6f,
                    PenalizeNewline = true,
                    Numa = false,
                    NumCtx = 1024,
                    NumBatch = 2,
                    NumGpu = 1,
                    MainGpu = 0,
                    LowVram = false,
                    F16Kv = true,
                    VocabOnly = false,
                    UseMmap = true,
                    UseMlock = false,
                    NumThread = 8
                }
            };
        }

        /// <summary>
        /// Create OpenAI completion request.
        /// </summary>
        /// <param name="model">Model name.</param>
        /// <param name="prompt">Prompt text.</param>
        /// <param name="stream">Whether to stream the response.</param>
        /// <returns>OpenAI completion request.</returns>
        internal static OpenAIGenerateCompletionRequest CreateOpenAICompletionRequest(string model, string prompt, bool stream = false)
        {
            var request = new OpenAIGenerateCompletionRequest
            {
                Model = model,
                Stream = stream
            };
            request.SetPrompt(prompt);
            return request;
        }

        /// <summary>
        /// Create OpenAI chat completion request.
        /// </summary>
        /// <param name="model">Model name.</param>
        /// <param name="messages">Chat messages.</param>
        /// <param name="stream">Whether to stream the response.</param>
        /// <returns>OpenAI chat completion request.</returns>
        internal static OpenAIGenerateChatCompletionRequest CreateOpenAIChatCompletionRequest(string model, List<OpenAIChatMessage> messages, bool stream = false)
        {
            return new OpenAIGenerateChatCompletionRequest
            {
                Model = model,
                Messages = messages,
                Stream = stream
            };
        }

        /// <summary>
        /// Create Ollama pull model request.
        /// </summary>
        /// <param name="name">Model name to pull.</param>
        /// <param name="insecure">Whether to use insecure registry.</param>
        /// <param name="stream">Whether to stream the response.</param>
        /// <returns>Ollama pull model request.</returns>
        internal static OllamaPullModelRequest CreateOllamaPullModelRequest(string name, bool insecure = false, bool stream = true)
        {
            return new OllamaPullModelRequest
            {
                Name = name,
                Insecure = insecure,
                Stream = stream
            };
        }

        /// <summary>
        /// Create Ollama delete model request.
        /// </summary>
        /// <param name="name">Model name to delete.</param>
        /// <returns>Ollama delete model request.</returns>
        internal static OllamaDeleteModelRequest CreateOllamaDeleteModelRequest(string name)
        {
            return new OllamaDeleteModelRequest
            {
                Model = name
            };
        }

        /// <summary>
        /// Create sample chat messages for testing.
        /// </summary>
        /// <param name="userMessage">User message content.</param>
        /// <returns>List of chat messages.</returns>
        internal static List<OllamaChatMessage> CreateOllamaChatMessages(string userMessage)
        {
            return new List<OllamaChatMessage>
            {
                new OllamaChatMessage { Role = "user", Content = userMessage }
            };
        }

        /// <summary>
        /// Create sample OpenAI chat messages for testing.
        /// </summary>
        /// <param name="userMessage">User message content.</param>
        /// <returns>List of chat messages.</returns>
        internal static List<OpenAIChatMessage> CreateOpenAIChatMessages(string userMessage)
        {
            return new List<OpenAIChatMessage>
            {
                new OpenAIChatMessage { Role = "user", Content = userMessage }
            };
        }

        /// <summary>
        /// Wait for SharpAI server to be available.
        /// </summary>
        /// <param name="sdk">SharpAI SDK instance.</param>
        /// <param name="timeoutMs">Timeout in milliseconds.</param>
        /// <param name="intervalMs">Check interval in milliseconds.</param>
        /// <returns>True if server is available, false otherwise.</returns>
        internal static async Task<bool> WaitForSharpAIServer(SharpAISdk sdk, int timeoutMs = 30000, int intervalMs = 1000)
        {
            int waited = 0;

            while (waited < timeoutMs)
            {
                try
                {
                    // Try to list models as a health check
                    var models = await sdk.Ollama.ListLocalModels();
                    if (models != null)
                    {
                        Console.WriteLine("SharpAI server is available");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SharpAI server not ready: {ex.Message}");
                }

                await Task.Delay(intervalMs);
                waited += intervalMs;
            }

            Console.WriteLine($"SharpAI server not available after {timeoutMs}ms");
            return false;
        }

        /// <summary>
        /// Clean up test resources.
        /// </summary>
        /// <param name="cleanupLogs">Whether to cleanup logs.</param>
        internal static void Cleanup(bool cleanupLogs = true)
        {
            if (cleanupLogs && Directory.Exists("./logs"))
            {
                try
                {
                    Directory.Delete("./logs", true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to cleanup logs: {ex.Message}");
                }
            }
        }

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Validate embeddings result.
        /// </summary>
        /// <param name="result">Embeddings result.</param>
        /// <param name="expectedCount">Expected number of embeddings.</param>
        /// <returns>True if valid, false otherwise.</returns>
        internal static bool ValidateEmbeddingsResult(OllamaGenerateEmbeddingsResult? result, int expectedCount)
        {
            if (result == null) return false;
            if (result.Embeddings == null) return false;
            return result.GetEmbeddingCount() == expectedCount;
        }

        /// <summary>
        /// Validate OpenAI embeddings result.
        /// </summary>
        /// <param name="result">OpenAI embeddings result.</param>
        /// <param name="expectedCount">Expected number of embeddings.</param>
        /// <returns>True if valid, false otherwise.</returns>
        internal static bool ValidateOpenAIEmbeddingsResult(OpenAIGenerateEmbeddingsResult? result, int expectedCount)
        {
            if (result == null) return false;
            if (result.Data == null) return false;
            return result.Data.Count == expectedCount;
        }

        /// <summary>
        /// Validate completion result.
        /// </summary>
        /// <param name="result">Completion result.</param>
        /// <returns>True if valid, false otherwise.</returns>
        internal static bool ValidateCompletionResult(OllamaGenerateCompletionResult? result)
        {
            if (result == null) return false;
            return !string.IsNullOrEmpty(result.Response);
        }

        /// <summary>
        /// Validate chat completion result.
        /// </summary>
        /// <param name="result">Chat completion result.</param>
        /// <returns>True if valid, false otherwise.</returns>
        internal static bool ValidateChatCompletionResult(OllamaGenerateCompletionResult? result)
        {
            if (result == null) return false;
            return !string.IsNullOrEmpty(result.Response);
        }

        /// <summary>
        /// Validate OpenAI completion result.
        /// </summary>
        /// <param name="result">OpenAI completion result.</param>
        /// <returns>True if valid, false otherwise.</returns>
        internal static bool ValidateOpenAICompletionResult(OpenAIGenerateCompletionResult? result)
        {
            if (result == null) return false;
            if (result.Choices == null || result.Choices.Count == 0) return false;
            return !string.IsNullOrEmpty(result.Choices[0].Text);
        }

        /// <summary>
        /// Validate OpenAI chat completion result.
        /// </summary>
        /// <param name="result">OpenAI chat completion result.</param>
        /// <returns>True if valid, false otherwise.</returns>
        internal static bool ValidateOpenAIChatCompletionResult(OpenAIGenerateChatCompletionResult? result)
        {
            if (result == null) return false;
            if (result.Choices == null || result.Choices.Count == 0) return false;
            return result.Choices[0].Message?.Content != null;
        }

        #endregion

#pragma warning restore CS8603 // Possible null reference return.
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
