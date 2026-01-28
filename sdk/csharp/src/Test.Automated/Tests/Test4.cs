namespace Test.Automated.Tests
{
    using SharpAI.Sdk;

    /// <summary>
    /// Test4: Ollama streaming completions.
    /// This test verifies Ollama streaming completion functionality.
    /// </summary>
    public class Test4 : TestBase
    {
        public Test4()
        {
            Name = "Test4 - Ollama Streaming Completions";
        }

        public override async Task Run(TestResult result)
        {
            result.Name = Name;
            result.StartUtc = DateTime.UtcNow;
            result.Success = true;

            InitializeTestEnvironment();

            #region Streaming-Completions

            ApiDetails streamingCompletions = CreateApiDetails("Streaming Completions");
            try
            {
                var request = Helpers.CreateOllamaCompletionRequest(TestEnvironment.CompletionsModel, "Tell me a short story about a robot.", true);
                var streamResult = SharpAISdk.Ollama.GenerateCompletionStream(request);

                if (streamResult == null)
                {
                    result.Success = false;
                    Console.WriteLine("No response for streaming completions request");
                    CompleteApiDetails(streamingCompletions, "null", 0);
                    result.ApiDetails.Add(streamingCompletions);
                    return;
                }

                // Consume the streaming response
                await foreach (var chunk in streamResult)
                {
                    // Just consume the stream without displaying content
                }

                CompleteApiDetails(streamingCompletions, "Success", 200);
                result.ApiDetails.Add(streamingCompletions);
            }
            catch (Exception ex)
            {
                result.Success = false;
                Console.WriteLine($"Error in streaming completions test: {ex.Message}");
                CompleteApiDetails(streamingCompletions, ex.Message, 500);
                result.ApiDetails.Add(streamingCompletions);
            }

            // Test streaming chat completions
            ApiDetails streamingChatCompletions = CreateApiDetails("Streaming Chat Completions");
            try
            {
                var messages = Helpers.CreateOllamaChatMessages("Write a poem about artificial intelligence.");
                var request = Helpers.CreateOllamaChatCompletionRequest(TestEnvironment.ChatCompletionsModel, messages, true);
                var streamResult = SharpAISdk.Ollama.GenerateChatCompletionStream(request);

                if (streamResult == null)
                {
                    result.Success = false;
                    Console.WriteLine("No response for streaming chat completions request");
                    CompleteApiDetails(streamingChatCompletions, "null", 0);
                    result.ApiDetails.Add(streamingChatCompletions);
                    return;
                }

                // Consume the streaming response
                await foreach (var chunk in streamResult)
                {
                    // Just consume the stream without displaying content
                }

                CompleteApiDetails(streamingChatCompletions, "Success", 200);
                result.ApiDetails.Add(streamingChatCompletions);
            }
            catch (Exception ex)
            {
                result.Success = false;
                Console.WriteLine($"Error in streaming chat completions test: {ex.Message}");
                CompleteApiDetails(streamingChatCompletions, ex.Message, 500);
                result.ApiDetails.Add(streamingChatCompletions);
            }

            #endregion

            result.EndUtc = DateTime.UtcNow;

            if (result.Success)
            {
                Console.WriteLine($"{Name} completed successfully in {result.Runtime.TotalMilliseconds}ms");
            }
            else
            {
                Console.WriteLine($"{Name} failed in {result.Runtime.TotalMilliseconds}ms");
            }
        }
    }
}
