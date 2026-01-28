namespace Test.Automated.Tests
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Test7 - OpenAI Streaming Chat Completions
    /// </summary>
    public class Test7 : TestBase
    {
        public Test7()
        {
            Name = "Test7 - OpenAI Streaming Chat Completions";
        }

        public override async Task Run(TestResult result)
        {
            result.Name = Name;
            result.StartUtc = DateTime.UtcNow;
            result.Success = true;

            InitializeTestEnvironment();

            #region OpenAI-Streaming-Chat-Completions

            // Test streaming chat completions
            ApiDetails streamingChatCompletions = CreateApiDetails("OpenAI Streaming Chat Completions");
            try
            {
                var messages = Helpers.CreateOpenAIChatMessages("Write a short poem about artificial intelligence.");
                var request = Helpers.CreateOpenAIChatCompletionRequest(TestEnvironment.ChatCompletionsModel, messages, true);
                var streamResult = SharpAISdk.OpenAI.GenerateChatCompletionStreamAsync(request);
                
                if (streamResult == null)
                {
                    result.Success = false;
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
                CompleteApiDetails(streamingChatCompletions, ex.Message, 500);
                result.ApiDetails.Add(streamingChatCompletions);
            }

            #endregion

            result.EndUtc = DateTime.UtcNow;
        }
    }
}