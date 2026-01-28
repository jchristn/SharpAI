namespace Test.Automated.Tests
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Test6 - OpenAI Streaming Completion
    /// </summary>
    public class Test6 : TestBase
    {
        public Test6()
        {
            Name = "Test6 - OpenAI Streaming Completion";
        }

        public override async Task Run(TestResult result)
        {
            result.Name = Name;
            result.StartUtc = DateTime.UtcNow;
            result.Success = true;

            InitializeTestEnvironment();

            #region OpenAI-Streaming-Completions

            // Test streaming completions
            ApiDetails streamingCompletions = CreateApiDetails("OpenAI Streaming Completions");
            try
            {
                var request = Helpers.CreateOpenAICompletionRequest(TestEnvironment.CompletionsModel, "Write a short story about a robot.", true);
                var streamResult = SharpAISdk.OpenAI.GenerateCompletionStreamAsync(request);
                
                if (streamResult == null)
                {
                    result.Success = false;
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
                CompleteApiDetails(streamingCompletions, ex.Message, 500);
                result.ApiDetails.Add(streamingCompletions);
            }

            #endregion

            result.EndUtc = DateTime.UtcNow;
        }
    }
}
