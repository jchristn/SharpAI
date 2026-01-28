namespace Test.SharpAISdk
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using GetSomeInput;
    using SharpAI.Models.Ollama;
    using SharpAI.Models.OpenAI;
    using SharpAI.Sdk;

    /// <summary>
    /// Test application for SharpAI SDK demonstrating all available Ollama methods.
    /// </summary>
    public static class Program
    {
        #region Private-Members

        private static bool _RunForever = true;
        private static bool _Debug = false;
        private static SharpAISdk? _Sdk = null;
        private static string _Endpoint = "http://localhost:8000";
        private static string? _CurrentModel = null;

        #endregion

        #region Main-Entry

        /// <summary>
        /// Main entry point for the test application.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static async Task Main(string[] args)
        {
            Console.WriteLine("SharpAI SDK Test Application");
            Console.WriteLine("============================");
            Console.WriteLine();

            // Initialize SDK
            InitializeSdk();

            while (_RunForever)
            {
                string userInput = Inputty.GetString("Command [? for help]:", null, false);

                if (userInput.Equals("?")) ShowMenu();
                else if (userInput.Equals("q")) _RunForever = false;
                else if (userInput.Equals("cls")) Console.Clear();
                else if (userInput.Equals("debug")) ToggleDebug();
                else if (userInput.Equals("endpoint")) SetEndpoint();
                else if (userInput.Equals("model")) await SetCurrentModel();
                else if (userInput.Equals("models")) await TestListModels();
                else if (userInput.Equals("pull")) await TestPullModel();
                else if (userInput.Equals("delete")) await TestDeleteModel();
                else if (userInput.Equals("completion")) await TestOllamaTextCompletion();
                else if (userInput.Equals("chat")) await TestOllamaChatCompletion();
                else if (userInput.Equals("stream-completion")) await TestOllamaStreamingTextCompletion();
                else if (userInput.Equals("stream-chat")) await TestOllamaStreamingChatCompletion();
                else if (userInput.Equals("ollama-embeddings-single")) await TestOllamaEmbeddingsSingle();
                else if (userInput.Equals("ollama-embeddings-multiple")) await TestOllamaEmbeddingsMultiple();
                else if (userInput.Equals("openai-completion")) await TestOpenAICompletion();
                else if (userInput.Equals("openai-chat")) await TestOpenAIChatCompletion();
                else if (userInput.Equals("openai-stream-completion")) await TestOpenAIStreamingCompletion();
                else if (userInput.Equals("openai-stream-chat")) await TestOpenAIStreamingChatCompletion();
                else if (userInput.Equals("openai-embeddings-single")) await TestOpenAIEmbeddingsSingle();
                else if (userInput.Equals("openai-embeddings-multiple")) await TestOpenAIEmbeddingsMultiple();
                else
                {
                    Console.WriteLine("Unknown command. Type '?' for help.");
                }
            }

            // Cleanup
            _Sdk?.Dispose();
            Console.WriteLine("Goodbye!");
        }

        #endregion

        #region Private-Methods

        private static void InitializeSdk()
        {
            try
            {
                _Endpoint = Inputty.GetString("SharpAI server endpoint:", _Endpoint, false);
                _Sdk = new SharpAISdk(_Endpoint);
                _Sdk.LogRequests = _Debug;
                _Sdk.LogResponses = _Debug;
                _Sdk.Logger = (level, message) =>
                {
                    if (_Debug || level.Equals("WARN", StringComparison.OrdinalIgnoreCase) || level.Equals("ERROR", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"[{level}] {message}");
                    }
                };

                Console.WriteLine($"SDK initialized with endpoint: {_Endpoint}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing SDK: {ex.Message}");
            }
        }

        private static void ShowMenu()
        {
            Console.WriteLine();
            Console.WriteLine("Available commands:");
            Console.WriteLine("  ?                   help, this menu");
            Console.WriteLine("  q                   quit");
            Console.WriteLine("  cls                 clear the screen");
            Console.WriteLine("  debug               enable or disable debug (enabled: " + _Debug + ")");
            Console.WriteLine("  endpoint            set the SharpAI server endpoint (currently: " + _Endpoint + ")");
            Console.WriteLine("  model               set the current model (currently: " + (_CurrentModel ?? "none") + ")");
            Console.WriteLine();
            Console.WriteLine("Model Management:");
            Console.WriteLine("  models                        list available models");
            Console.WriteLine("  pull                          pull a model with streaming progress");
            Console.WriteLine("  delete                        delete a model");
            Console.WriteLine();
            Console.WriteLine("AI Operations (Ollama API):");
            Console.WriteLine("  completion                    generate text completion (non-streaming, continuous conversation)");
            Console.WriteLine("  chat                          generate chat completion (non-streaming, continuous conversation)");
            Console.WriteLine("  stream-completion             generate streaming text completion (continuous conversation)");
            Console.WriteLine("  stream-chat                   generate streaming chat completion (continuous conversation)");
            Console.WriteLine("  ollama-embeddings-single      generate Ollama embeddings for single text");
            Console.WriteLine("  ollama-embeddings-multiple    generate Ollama embeddings for multiple texts");
            Console.WriteLine();
            Console.WriteLine("AI Operations (OpenAI API):");
            Console.WriteLine("  openai-completion             generate OpenAI text completion (non-streaming)");
            Console.WriteLine("  openai-chat                   generate OpenAI chat completion (non-streaming)");
            Console.WriteLine("  openai-stream-completion      generate OpenAI streaming text completion");
            Console.WriteLine("  openai-stream-chat            generate OpenAI streaming chat completion");
            Console.WriteLine("  openai-embeddings-single      generate OpenAI embeddings for single text");
            Console.WriteLine("  openai-embeddings-multiple    generate OpenAI embeddings for multiple texts");
            Console.WriteLine();
            Console.WriteLine("Note: Type 'q' to exit conversations");
            Console.WriteLine();
        }

        private static void ToggleDebug()
        {
            _Debug = !_Debug;
            if (_Sdk != null)
            {
                _Sdk.LogRequests = _Debug;
                _Sdk.LogResponses = _Debug;
            }
            Console.WriteLine("Debug mode: " + (_Debug ? "enabled" : "disabled"));
        }

        private static void SetEndpoint()
        {
            string newEndpoint = Inputty.GetString("SharpAI server endpoint:", _Endpoint, false);
            if (!string.IsNullOrEmpty(newEndpoint))
            {
                _Endpoint = newEndpoint;
                InitializeSdk();
            }
        }

        private static async Task SetCurrentModel()
        {
            if (_Sdk == null)
            {
                Console.WriteLine("SDK not initialized.");
                return;
            }

            try
            {
                var models = await _Sdk.Ollama.ListLocalModels();
                if (models == null || models.Count == 0)
                {
                    Console.WriteLine("No models available. Use 'pull' command to download a model.");
                    return;
                }

                Console.WriteLine("Available models:");
                for (int i = 0; i < models.Count; i++)
                {
                    Console.WriteLine($"  {i + 1}. {models[i].Name}");
                }

                int selection = Inputty.GetInteger("Select model number:", 1, true, true);
                if (selection > 0 && selection <= models.Count)
                {
                    _CurrentModel = models[selection - 1].Name;
                    Console.WriteLine($"Current model set to: {_CurrentModel}");
                }
                else
                {
                    Console.WriteLine("Invalid selection.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting current model: {ex.Message}");
            }
        }

        private static async Task TestListModels()
        {
            if (_Sdk == null)
            {
                Console.WriteLine("SDK not initialized.");
                return;
            }

            try
            {
                Console.WriteLine("Listing available models...");
                var models = await _Sdk.Ollama.ListLocalModels();

                if (models != null && models.Count > 0)
                {
                    Console.WriteLine($"Found {models.Count} models:");
                    foreach (var model in models)
                    {
                        Console.WriteLine($"  - {model.Name} (Size: {model.Size} bytes)");
                        if (model.Details != null)
                        {
                            Console.WriteLine($"    Format: {model.Details.Format}");
                            Console.WriteLine($"    Family: {model.Details.Family}");
                            Console.WriteLine($"    Parameter Size: {model.Details.ParameterSize}");
                        }
                    }

                    // Auto-select the first model if none is currently selected
                    if (string.IsNullOrEmpty(_CurrentModel))
                    {
                        _CurrentModel = models[0].Name;
                        Console.WriteLine($"Auto-selected model: {_CurrentModel}");
                    }
                }
                else
                {
                    Console.WriteLine("No models found. Use 'pull' command to download a model.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listing models: {ex.Message}");
            }
        }

        private static async Task TestPullModel()
        {
            if (_Sdk == null)
            {
                Console.WriteLine("SDK not initialized.");
                return;
            }

            try
            {
                string modelName = Inputty.GetString("Model name to pull (e.g., TheBloke/Llama-2-7B-Chat-GGUF):", null, false);
                if (string.IsNullOrEmpty(modelName))
                {
                    Console.WriteLine("Model name is required.");
                    return;
                }

                Console.WriteLine($"Pulling model with streaming progress: {modelName}...");
                var request = new OllamaPullModelRequest
                {
                    Model = modelName
                };

                Console.WriteLine("Download progress:");

                await foreach (var progress in _Sdk.Ollama.PullModel(request))
                {
                    if (!string.IsNullOrEmpty(progress.Status))
                    {
                        Console.Write($"\rStatus: {progress.Status}");

                        if (progress.Downloaded.HasValue && progress.Percent.HasValue)
                        {
                            var percentage = progress.GetProgressPercentage();
                            var progressStr = progress.GetFormattedProgress();
                            Console.Write($" - {progressStr}");
                        }
                        Console.Write("                    ");
                    }

                    if (progress.IsComplete())
                    {
                        Console.WriteLine($"\nPull completed successfully with status: {progress.Status}");
                        if (progress.Downloaded.HasValue && progress.Percent.HasValue)
                        {
                            var percentage = progress.GetProgressPercentage();
                            var progressStr = progress.GetFormattedProgress();
                            Console.WriteLine($"Final progress: {progressStr}");
                        }

                        if (string.IsNullOrEmpty(_CurrentModel))
                        {
                            _CurrentModel = modelName;
                            Console.WriteLine($"Auto-selected newly pulled model: {_CurrentModel}");
                        }
                        break;
                    }

                    if (progress.HasError())
                    {
                        Console.WriteLine($"\nError: {progress.Error}");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error pulling model: {ex.Message}");
            }
        }

        private static async Task TestDeleteModel()
        {
            if (_Sdk == null)
            {
                Console.WriteLine("SDK not initialized.");
                return;
            }

            try
            {
                string modelName = Inputty.GetString("Model name to delete:", null, false);
                if (string.IsNullOrEmpty(modelName))
                {
                    Console.WriteLine("Model name is required.");
                    return;
                }

                bool confirm = Inputty.GetBoolean($"Are you sure you want to delete '{modelName}'?", false);
                if (!confirm)
                {
                    Console.WriteLine("Delete cancelled.");
                    return;
                }

                Console.WriteLine($"Deleting model: {modelName}...");
                var request = new OllamaDeleteModelRequest
                {
                    Model = modelName
                };
                await _Sdk.Ollama.DeleteModel(request);
                Console.WriteLine("Model deleted successfully.");

                if (_CurrentModel == modelName)
                {
                    _CurrentModel = null;
                    Console.WriteLine("Current model selection cleared.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting model: {ex.Message}");
            }
        }

        private static async Task TestOllamaTextCompletion()
        {
            if (_Sdk == null)
            {
                Console.WriteLine("SDK not initialized.");
                return;
            }

            if (string.IsNullOrEmpty(_CurrentModel))
            {
                Console.WriteLine("No model selected. Use 'model' command to select a model first.");
                return;
            }

            try
            {
                string modelName = _CurrentModel;

                float temperature = (float)Inputty.GetDecimal("Temperature (0.0-1.0):", 0.7m, true, true);
                int numPredict = Inputty.GetInteger("Number of tokens to predict:", 100, true, false);

                Console.WriteLine($"Starting non-streaming text completion session with model '{modelName}'...");
                Console.WriteLine("Type 'q' to exit the conversation.");
                Console.WriteLine();

                bool continueConversation = true;
                while (continueConversation)
                {
                    string prompt = Inputty.GetString("Prompt:", null, false);
                    if (string.IsNullOrEmpty(prompt))
                    {
                        Console.WriteLine("Prompt cannot be empty. Please try again.");
                        continue;
                    }

                    if (prompt.ToLower() == "q")
                    {
                        Console.WriteLine("Exiting text completion session.");
                        break;
                    }

                    Console.WriteLine($"Generating completion with model '{modelName}'...");
                    var request = new OllamaGenerateCompletionRequest
                    {
                        Model = modelName,
                        Prompt = prompt,
                        Options = new OllamaCompletionOptions
                        {
                            Temperature = temperature,
                            NumPredict = numPredict
                        }
                    };

                    var result = await _Sdk.Ollama.GenerateCompletion(request);
                    if (result != null)
                    {
                        Console.WriteLine($"Completion: {result.Response}");
                    }
                    else
                    {
                        Console.WriteLine("No completion result received.");
                    }

                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating completion: {ex.Message}");
            }
        }

        private static async Task TestOllamaChatCompletion()
        {
            if (_Sdk == null)
            {
                Console.WriteLine("SDK not initialized.");
                return;
            }

            if (string.IsNullOrEmpty(_CurrentModel))
            {
                Console.WriteLine("No model selected. Use 'model' command to select a model first.");
                return;
            }

            try
            {
                string modelName = _CurrentModel;

                float temperature = (float)Inputty.GetDecimal("Temperature (0.0-1.0):", 0.7m, true, true);
                int numPredict = Inputty.GetInteger("Number of tokens to predict:", 100, true, false);

                Console.WriteLine($"Starting non-streaming chat completion session with model '{modelName}'...");
                Console.WriteLine("Type 'q' to exit the conversation.");
                Console.WriteLine();

                // Initialize conversation history
                var conversationHistory = new List<OllamaChatMessage>();
                bool continueConversation = true;

                while (continueConversation)
                {
                    string userMessage = Inputty.GetString("User message:", null, false);
                    if (string.IsNullOrEmpty(userMessage))
                    {
                        Console.WriteLine("Message cannot be empty. Please try again.");
                        continue;
                    }

                    if (userMessage.ToLower() == "q")
                    {
                        Console.WriteLine("Exiting chat completion session.");
                        break;
                    }

                    // Add user message to conversation history
                    conversationHistory.Add(new OllamaChatMessage { Role = "user", Content = userMessage });

                    Console.WriteLine($"Generating chat completion with model '{modelName}'...");
                    var request = new OllamaGenerateChatCompletionRequest
                    {
                        Model = modelName,
                        Messages = conversationHistory,
                        Options = new OllamaCompletionOptions
                        {
                            Temperature = temperature,
                            NumPredict = numPredict
                        }
                    };

                    var result = await _Sdk.Ollama.GenerateChatCompletion(request);
                    if (result?.Response != null)
                    {
                        Console.WriteLine($"Assistant: {result.Response}");

                        // Add assistant response to conversation history
                        conversationHistory.Add(new OllamaChatMessage { Role = "assistant", Content = result.Response });
                    }
                    else
                    {
                        Console.WriteLine("No chat completion result received.");
                    }

                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating chat completion: {ex.Message}");
            }
        }

        private static async Task TestOllamaStreamingTextCompletion()
        {
            if (_Sdk == null)
            {
                Console.WriteLine("SDK not initialized.");
                return;
            }

            if (string.IsNullOrEmpty(_CurrentModel))
            {
                Console.WriteLine("No model selected. Use 'model' command to select a model first.");
                return;
            }

            try
            {
                string modelName = _CurrentModel;

                float temperature = (float)Inputty.GetDecimal("Temperature (0.0-1.0):", 0.7m, true, true);
                int numPredict = Inputty.GetInteger("Number of tokens to predict:", 100, true, false);

                Console.WriteLine($"Starting streaming text completion session with model '{modelName}'...");
                Console.WriteLine("Type 'q' to exit the conversation.");
                Console.WriteLine();

                bool continueConversation = true;
                while (continueConversation)
                {
                    string prompt = Inputty.GetString("Prompt:", null, false);
                    if (string.IsNullOrEmpty(prompt))
                    {
                        Console.WriteLine("Prompt cannot be empty. Please try again.");
                        continue;
                    }

                    if (prompt.ToLower() == "q")
                    {
                        Console.WriteLine("Exiting streaming text completion session.");
                        break;
                    }

                    Console.WriteLine($"Generating streaming completion with model '{modelName}'...");
                    var request = new OllamaGenerateCompletionRequest
                    {
                        Model = modelName,
                        Prompt = prompt,
                        Options = new OllamaCompletionOptions
                        {
                            Temperature = temperature,
                            NumPredict = numPredict
                        }
                    };

                    Console.WriteLine("Streaming completion:");
                    Console.Write("Completion: ");

                    await foreach (var chunk in _Sdk.Ollama.GenerateCompletionStream(request))
                    {
                        if (chunk.Response != null)
                        {
                            Console.Write(chunk.Response);
                        }
                    }
                    Console.WriteLine();
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating streaming completion: {ex.Message}");
            }
        }

        private static async Task TestOllamaStreamingChatCompletion()
        {
            if (_Sdk == null)
            {
                Console.WriteLine("SDK not initialized.");
                return;
            }

            if (string.IsNullOrEmpty(_CurrentModel))
            {
                Console.WriteLine("No model selected. Use 'model' command to select a model first.");
                return;
            }

            try
            {
                string modelName = _CurrentModel;

                float temperature = (float)Inputty.GetDecimal("Temperature (0.0-1.0):", 0.7m, true, true);
                int numPredict = Inputty.GetInteger("Number of tokens to predict:", 100, true, false);

                Console.WriteLine($"Starting streaming chat completion session with model '{modelName}'...");
                Console.WriteLine("Type 'q' to exit the conversation.");
                Console.WriteLine();

                // Initialize conversation history
                var conversationHistory = new List<OllamaChatMessage>();
                bool continueConversation = true;

                while (continueConversation)
                {
                    string userMessage = Inputty.GetString("User message:", null, false);
                    if (string.IsNullOrEmpty(userMessage))
                    {
                        Console.WriteLine("Message cannot be empty. Please try again.");
                        continue;
                    }

                    if (userMessage.ToLower() == "q")
                    {
                        Console.WriteLine("Exiting streaming chat completion session.");
                        break;
                    }

                    // Add user message to conversation history
                    conversationHistory.Add(new OllamaChatMessage { Role = "user", Content = userMessage });

                    Console.WriteLine($"Generating streaming chat completion with model '{modelName}'...");
                    var request = new OllamaGenerateChatCompletionRequest
                    {
                        Model = modelName,
                        Messages = conversationHistory,
                        Options = new OllamaCompletionOptions
                        {
                            Temperature = temperature,
                            NumPredict = numPredict
                        }
                    };

                    Console.WriteLine("Streaming chat completion:");
                    Console.Write("Assistant: ");

                    string assistantResponse = "";
                    await foreach (var chunk in _Sdk.Ollama.GenerateChatCompletionStream(request))
                    {
                        if (chunk.Message?.Content != null)
                        {
                            Console.Write(chunk.Message.Content);
                            assistantResponse += chunk.Message.Content;
                        }
                    }
                    Console.WriteLine();

                    // Add assistant response to conversation history
                    if (!string.IsNullOrEmpty(assistantResponse))
                    {
                        conversationHistory.Add(new OllamaChatMessage { Role = "assistant", Content = assistantResponse });
                    }

                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating streaming chat completion: {ex.Message}");
            }
        }

        private static async Task TestOllamaEmbeddingsSingle()
        {
            if (_Sdk == null)
            {
                Console.WriteLine("SDK not initialized.");
                return;
            }

            if (string.IsNullOrEmpty(_CurrentModel))
            {
                Console.WriteLine("No model selected. Use 'model' command to select a model first.");
                return;
            }

            try
            {
                string modelName = _CurrentModel;

                string input = Inputty.GetString("Input text for embeddings:", "This is a test sentence for generating embeddings.", false);

                Console.WriteLine($"Generating embeddings with model '{modelName}'...");
                var request = new OllamaGenerateEmbeddingsRequest
                {
                    Model = modelName,
                    Input = input
                };

                var result = await _Sdk.Ollama.GenerateEmbeddings(request);
                if (result != null)
                {
                    Console.WriteLine("=== Complete JSON Response ===");
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    }));
                    Console.WriteLine("=== End JSON Response ===");
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine("No embeddings result received.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating embeddings: {ex.Message}");
            }
        }

        private static async Task TestOllamaEmbeddingsMultiple()
        {
            if (_Sdk == null)
            {
                Console.WriteLine("SDK not initialized.");
                return;
            }

            if (string.IsNullOrEmpty(_CurrentModel))
            {
                Console.WriteLine("No model selected. Use 'model' command to select a model first.");
                return;
            }

            try
            {
                Console.WriteLine("Enter multiple texts for embeddings (press ENTER on empty line to finish):");
                var inputs = new List<string>();

                while (true)
                {
                    string input = Inputty.GetString($"Text {inputs.Count + 1}:", null, true);
                    if (string.IsNullOrEmpty(input))
                        break;
                    inputs.Add(input);
                }

                if (inputs.Count == 0)
                {
                    Console.WriteLine("No texts provided.");
                    return;
                }

                Console.WriteLine($"Generating Ollama embeddings for {inputs.Count} texts with model '{_CurrentModel}'...");
                var request = new OllamaGenerateEmbeddingsRequest
                {
                    Model = _CurrentModel
                };
                request.SetInputs(inputs.ToList());

                var result = await _Sdk.Ollama.GenerateMultipleEmbeddings(request);
                if (result != null)
                {
                    Console.WriteLine("=== Complete JSON Response ===");
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    }));
                    Console.WriteLine("=== End JSON Response ===");
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine("No embeddings result received.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating Ollama embeddings: {ex.Message}");
            }
        }

        private static async Task TestOpenAICompletion()
        {
            if (_Sdk == null)
            {
                Console.WriteLine("SDK not initialized.");
                return;
            }

            if (string.IsNullOrEmpty(_CurrentModel))
            {
                Console.WriteLine("No model selected. Use 'model' command to select a model first.");
                return;
            }

            try
            {
                string prompt = Inputty.GetString("Prompt:", null, false);
                if (string.IsNullOrEmpty(prompt))
                {
                    Console.WriteLine("Prompt cannot be empty. Please try again.");
                    return;
                }

                int maxTokens = Inputty.GetInteger("Max tokens:", 100, true, false);
                float temperature = (float)Inputty.GetDecimal("Temperature (0.0-2.0):", 0.7m, true, true);

                Console.WriteLine($"Generating OpenAI completion with model '{_CurrentModel}'...");
                var request = new OpenAIGenerateCompletionRequest
                {
                    Model = _CurrentModel,
                    Prompt = prompt,
                    MaxTokens = maxTokens,
                    Temperature = temperature
                };

                var result = await _Sdk.OpenAI.GenerateCompletionAsync(request);
                if (result?.Choices != null && result.Choices.Count > 0)
                {
                    Console.WriteLine($"Completion: {result.Choices[0].Text}");
                }
                else
                {
                    Console.WriteLine("No completion result received.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating OpenAI completion: {ex.Message}");
            }
        }

        private static async Task TestOpenAIChatCompletion()
        {
            if (_Sdk == null)
            {
                Console.WriteLine("SDK not initialized.");
                return;
            }

            if (string.IsNullOrEmpty(_CurrentModel))
            {
                Console.WriteLine("No model selected. Use 'model' command to select a model first.");
                return;
            }

            try
            {
                string userMessage = Inputty.GetString("User message:", null, false);
                if (string.IsNullOrEmpty(userMessage))
                {
                    Console.WriteLine("Message cannot be empty. Please try again.");
                    return;
                }

                int maxTokens = Inputty.GetInteger("Max tokens:", 100, true, false);
                float temperature = (float)Inputty.GetDecimal("Temperature (0.0-2.0):", 0.7m, true, true);

                Console.WriteLine($"Generating OpenAI chat completion with model '{_CurrentModel}'...");
                var request = new OpenAIGenerateChatCompletionRequest
                {
                    Model = _CurrentModel,
                    Messages = new List<OpenAIChatMessage>
                    {
                        new OpenAIChatMessage
                        {
                            Role = "user",
                            Content = userMessage
                        }
                    },
                    MaxTokens = maxTokens,
                    Temperature = temperature
                };

                var result = await _Sdk.OpenAI.GenerateChatCompletionAsync(request);
                if (result?.Choices != null && result.Choices.Count > 0)
                {
                    Console.WriteLine($"Assistant: {result.Choices[0].Message?.Content}");
                }
                else
                {
                    Console.WriteLine("No chat completion result received.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating OpenAI chat completion: {ex.Message}");
            }
        }

        private static async Task TestOpenAIStreamingCompletion()
        {
            if (_Sdk == null)
            {
                Console.WriteLine("SDK not initialized.");
                return;
            }

            if (string.IsNullOrEmpty(_CurrentModel))
            {
                Console.WriteLine("No model selected. Use 'model' command to select a model first.");
                return;
            }

            try
            {
                int maxTokens = Inputty.GetInteger("Max tokens:", 100, true, false);
                float temperature = (float)Inputty.GetDecimal("Temperature (0.0-2.0):", 0.7m, true, true);

                Console.WriteLine($"Starting OpenAI streaming completion session with model '{_CurrentModel}'...");
                Console.WriteLine("Type 'q' to exit the conversation.");
                Console.WriteLine();

                bool continueConversation = true;
                while (continueConversation)
                {
                    string prompt = Inputty.GetString("Prompt:", null, false);
                    if (string.IsNullOrEmpty(prompt))
                    {
                        Console.WriteLine("Prompt cannot be empty. Please try again.");
                        continue;
                    }

                    if (prompt.ToLower() == "q")
                    {
                        Console.WriteLine("Exiting OpenAI streaming completion session.");
                        break;
                    }

                    Console.WriteLine($"Generating OpenAI streaming completion with model '{_CurrentModel}'...");
                    var request = new OpenAIGenerateCompletionRequest
                    {
                        Model = _CurrentModel,
                        Prompt = prompt,
                        MaxTokens = maxTokens,
                        Temperature = temperature
                    };

                    Console.WriteLine("Streaming completion:");
                    Console.Write("Completion: ");

                    await foreach (var chunk in _Sdk.OpenAI.GenerateCompletionStreamAsync(request))
                    {
                        if (chunk?.Choices != null && chunk.Choices.Count > 0)
                        {
                            Console.Write(chunk.Choices[0].Text);
                        }
                    }
                    Console.WriteLine();
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating OpenAI streaming completion: {ex.Message}");
            }
        }

        private static async Task TestOpenAIStreamingChatCompletion()
        {
            if (_Sdk == null)
            {
                Console.WriteLine("SDK not initialized.");
                return;
            }

            if (string.IsNullOrEmpty(_CurrentModel))
            {
                Console.WriteLine("No model selected. Use 'model' command to select a model first.");
                return;
            }

            try
            {
                int maxTokens = Inputty.GetInteger("Max tokens:", 100, true, false);
                float temperature = (float)Inputty.GetDecimal("Temperature (0.0-2.0):", 0.7m, true, true);

                Console.WriteLine($"Starting OpenAI streaming chat completion session with model '{_CurrentModel}'...");
                Console.WriteLine("Type 'q' to exit the conversation.");
                Console.WriteLine();

                // Initialize conversation history
                var conversationHistory = new List<OpenAIChatMessage>();
                bool continueConversation = true;

                while (continueConversation)
                {
                    string userMessage = Inputty.GetString("User message:", null, false);
                    if (string.IsNullOrEmpty(userMessage))
                    {
                        Console.WriteLine("Message cannot be empty. Please try again.");
                        continue;
                    }

                    if (userMessage.ToLower() == "q")
                    {
                        Console.WriteLine("Exiting OpenAI streaming chat completion session.");
                        break;
                    }

                    // Add user message to conversation history
                    conversationHistory.Add(new OpenAIChatMessage { Role = "user", Content = userMessage });

                    Console.WriteLine($"Generating OpenAI streaming chat completion with model '{_CurrentModel}'...");
                    var request = new OpenAIGenerateChatCompletionRequest
                    {
                        Model = _CurrentModel,
                        Messages = conversationHistory,
                        MaxTokens = maxTokens,
                        Temperature = temperature
                    };

                    Console.WriteLine("Streaming chat completion:");
                    Console.Write("Assistant: ");

                    string assistantResponse = "";
                    await foreach (var chunk in _Sdk.OpenAI.GenerateChatCompletionStreamAsync(request))
                    {
                        if (chunk?.Choices != null && chunk.Choices.Count > 0)
                        {
                            string text = chunk.Choices[0].Text;
                            if (text != null)
                            {
                                Console.Write(text);
                                assistantResponse += text;
                            }
                        }
                    }
                    Console.WriteLine();

                    // Add assistant response to conversation history
                    if (!string.IsNullOrEmpty(assistantResponse))
                    {
                        conversationHistory.Add(new OpenAIChatMessage { Role = "assistant", Content = assistantResponse });
                    }

                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating OpenAI streaming chat completion: {ex.Message}");
            }
        }

        private static async Task TestOpenAIEmbeddingsSingle()
        {
            if (_Sdk == null)
            {
                Console.WriteLine("SDK not initialized.");
                return;
            }

            if (string.IsNullOrEmpty(_CurrentModel))
            {
                Console.WriteLine("No model selected. Use 'model' command to select a model first.");
                return;
            }

            try
            {
                string input = Inputty.GetString("Input text for embeddings:", "This is a test sentence for generating embeddings.", false);

                Console.WriteLine($"Generating OpenAI embeddings for single text with model '{_CurrentModel}'...");
                var request = new OpenAIGenerateEmbeddingsRequest
                {
                    Model = _CurrentModel,
                    Input = input
                };

                var result = await _Sdk.OpenAI.GenerateEmbeddingsAsync(request);
                if (result != null)
                {
                    Console.WriteLine("=== Complete JSON Response ===");
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    }));
                    Console.WriteLine("=== End JSON Response ===");
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine("No embeddings result received.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating OpenAI embeddings: {ex.Message}");
            }
        }

        private static async Task TestOpenAIEmbeddingsMultiple()
        {
            if (_Sdk == null)
            {
                Console.WriteLine("SDK not initialized.");
                return;
            }

            if (string.IsNullOrEmpty(_CurrentModel))
            {
                Console.WriteLine("No model selected. Use 'model' command to select a model first.");
                return;
            }

            try
            {
                Console.WriteLine("Enter multiple texts for embeddings (press ENTER on empty line to finish):");
                var inputs = new List<string>();

                while (true)
                {
                    string input = Inputty.GetString($"Text {inputs.Count + 1}:", null, true);
                    if (string.IsNullOrEmpty(input))
                        break;
                    inputs.Add(input);
                }

                if (inputs.Count == 0)
                {
                    Console.WriteLine("No texts provided.");
                    return;
                }

                Console.WriteLine($"Generating OpenAI embeddings for {inputs.Count} texts with model '{_CurrentModel}'...");
                var request = new OpenAIGenerateEmbeddingsRequest
                {
                    Model = _CurrentModel
                };
                request.SetInputs(inputs.ToArray());

                var result = await _Sdk.OpenAI.GenerateMultipleEmbeddingsAsync(request);
                if (result != null)
                {
                    Console.WriteLine("=== Complete JSON Response ===");
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    }));
                    Console.WriteLine("=== End JSON Response ===");
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine("No embeddings result received.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating OpenAI embeddings: {ex.Message}");
            }
        }

        #endregion
    }
}
