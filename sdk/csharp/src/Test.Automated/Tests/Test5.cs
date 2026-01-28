namespace Test.Automated.Tests
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Test5 - OpenAI Chat and Completion (Non-Streaming)
    /// </summary>
    public class Test5 : TestBase
    {
        public Test5()
        {
            Name = "Test5 - OpenAI Chat and Completion (Non-Streaming)";
        }

        public override async Task Run(TestResult result)
        {
            result.Name = Name;
            result.StartUtc = DateTime.UtcNow;
            result.Success = true;

            InitializeTestEnvironment();

            #region OpenAI-Completions

            // Test OpenAI completion
            ApiDetails completions = CreateApiDetails("OpenAI Completion");
            try
            {
                var request = Helpers.CreateOpenAICompletionRequest(TestEnvironment.CompletionsModel, "What is the capital of France?", false);
                var completionResult = await SharpAISdk.OpenAI.GenerateCompletionAsync(request);
                
                if (!Helpers.ValidateOpenAICompletionResult(completionResult))
                {
                    result.Success = false;
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
                CompleteApiDetails(completions, ex.Message, 500);
                result.ApiDetails.Add(completions);
            }

            #endregion

            #region OpenAI-Chat-Completions
            ApiDetails chatCompletions = CreateApiDetails("OpenAI Chat Completion");
            try
            {
                var messages = Helpers.CreateOpenAIChatMessages("Write a short poem about artificial intelligence.");
                var request = Helpers.CreateOpenAIChatCompletionRequest(TestEnvironment.ChatCompletionsModel, messages, false);
                var chatResult = await SharpAISdk.OpenAI.GenerateChatCompletionAsync(request);
                
                if (!Helpers.ValidateOpenAIChatCompletionResult(chatResult))
                {
                    result.Success = false;
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
                CompleteApiDetails(chatCompletions, ex.Message, 500);
                result.ApiDetails.Add(chatCompletions);
            }

            #endregion

            result.EndUtc = DateTime.UtcNow;
        }
    }
}