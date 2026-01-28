namespace Test.Automated.Tests
{
    using SharpAI.Sdk;

    /// <summary>
    /// Test3: Ollama embeddings and completions.
    /// This test verifies Ollama embeddings and completion functionality.
    /// </summary>
    public class Test3 : TestBase
    {
        public Test3()
        {
            Name = "Test3 - Ollama Embeddings and Completions";
        }

        public override async Task Run(TestResult result)
        {
            result.Name = Name;
            result.StartUtc = DateTime.UtcNow;
            result.Success = true;

            InitializeTestEnvironment();

            #region Embeddings

            // Test single embeddings
            ApiDetails singleEmbeddings = CreateApiDetails("Single Embeddings");
            try
            {
                var request = Helpers.CreateOllamaSingleEmbeddingsRequest(TestEnvironment.EmbeddingsModel, "test");
                var embeddingsResult = await SharpAISdk.Ollama.GenerateEmbeddings(request);

                if (embeddingsResult == null)
                {
                    result.Success = false;
                    Console.WriteLine("No response for single embeddings request");
                    CompleteApiDetails(singleEmbeddings, "null", 0);
                    result.ApiDetails.Add(singleEmbeddings);
                    return;
                }

                if (!Helpers.ValidateEmbeddingsResult(embeddingsResult, 1))
                {
                    result.Success = false;
                    Console.WriteLine("Invalid embeddings result for single embeddings request");
                    CompleteApiDetails(singleEmbeddings, "Invalid result", 200);
                    result.ApiDetails.Add(singleEmbeddings);
                    return;
                }

                CompleteApiDetails(singleEmbeddings, "Success", 200);
                result.ApiDetails.Add(singleEmbeddings);
            }
            catch (Exception ex)
            {
                result.Success = false;
                Console.WriteLine($"Error in single embeddings test: {ex.Message}");
                CompleteApiDetails(singleEmbeddings, ex.Message, 500);
                result.ApiDetails.Add(singleEmbeddings);
            }

            // Test multiple embeddings
            ApiDetails multiEmbeddings = CreateApiDetails("Multiple Embeddings");
            try
            {
                var request = Helpers.CreateOllamaMultipleEmbeddingsRequest(TestEnvironment.EmbeddingsModel, new List<string> { "hello", "world" });
                var embeddingsResult = await SharpAISdk.Ollama.GenerateMultipleEmbeddings(request);

                if (embeddingsResult == null)
                {
                    result.Success = false;
                    Console.WriteLine("No response for multiple embeddings request");
                    CompleteApiDetails(multiEmbeddings, "null", 0);
                    result.ApiDetails.Add(multiEmbeddings);
                    return;
                }

                if (!Helpers.ValidateEmbeddingsResult(embeddingsResult, 2))
                {
                    result.Success = false;
                    Console.WriteLine("Invalid embeddings result for multiple embeddings request");
                    CompleteApiDetails(multiEmbeddings, "Invalid result", 200);
                    result.ApiDetails.Add(multiEmbeddings);
                    return;
                }

                CompleteApiDetails(multiEmbeddings, "Success", 200);
                result.ApiDetails.Add(multiEmbeddings);
            }
            catch (Exception ex)
            {
                result.Success = false;
                Console.WriteLine($"Error in multiple embeddings test: {ex.Message}");
                CompleteApiDetails(multiEmbeddings, ex.Message, 500);
                result.ApiDetails.Add(multiEmbeddings);
            }

            #endregion

            #region Completions
            ApiDetails completions = CreateApiDetails("Completions");
            try
            {
                var request = Helpers.CreateOllamaCompletionRequest(TestEnvironment.CompletionsModel, "What is the capital of France?", false);
                var completionResult = await SharpAISdk.Ollama.GenerateCompletion(request);

                if (completionResult == null)
                {
                    result.Success = false;
                    Console.WriteLine("No response for completions request");
                    CompleteApiDetails(completions, "null", 0);
                    result.ApiDetails.Add(completions);
                    return;
                }

                if (!Helpers.ValidateCompletionResult(completionResult))
                {
                    result.Success = false;
                    Console.WriteLine("Invalid completion result");
                    CompleteApiDetails(completions, "Invalid result", 200);
                    result.ApiDetails.Add(completions);
                    return;
                }

                CompleteApiDetails(completions, "Success", 200);
                result.ApiDetails.Add(completions);
            }
            catch (Exception ex)
            {
                result.Success = false;
                Console.WriteLine($"Error in completions test: {ex.Message}");
                CompleteApiDetails(completions, ex.Message, 500);
                result.ApiDetails.Add(completions);
            }

            // Test chat completions
            ApiDetails chatCompletions = CreateApiDetails("Chat Completions");
            try
            {
                var messages = Helpers.CreateOllamaChatMessages("Hello, how are you?");
                var request = Helpers.CreateOllamaChatCompletionRequest(TestEnvironment.ChatCompletionsModel, messages, false);
                var chatResult = await SharpAISdk.Ollama.GenerateChatCompletion(request);

                if (chatResult == null)
                {
                    result.Success = false;
                    Console.WriteLine("No response for chat completions request");
                    CompleteApiDetails(chatCompletions, "null", 0);
                    result.ApiDetails.Add(chatCompletions);
                    return;
                }

                if (!Helpers.ValidateChatCompletionResult(chatResult))
                {
                    result.Success = false;
                    Console.WriteLine("Invalid chat completion result");
                    CompleteApiDetails(chatCompletions, "Invalid result", 200);
                    result.ApiDetails.Add(chatCompletions);
                    return;
                }

                CompleteApiDetails(chatCompletions, "Success", 200);
                result.ApiDetails.Add(chatCompletions);
            }
            catch (Exception ex)
            {
                result.Success = false;
                Console.WriteLine($"Error in chat completions test: {ex.Message}");
                CompleteApiDetails(chatCompletions, ex.Message, 500);
                result.ApiDetails.Add(chatCompletions);
            }

            #endregion

            result.EndUtc = DateTime.UtcNow;
        }
    }
}
