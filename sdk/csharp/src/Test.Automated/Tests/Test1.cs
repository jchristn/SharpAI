namespace Test.Automated.Tests
{
    using SharpAI.Models.Ollama;
    using SharpAI.Sdk;

    /// <summary>
    /// Test1: Pull required models for testing.
    /// This test pulls the required models before running other tests.
    /// </summary>
    public class Test1 : TestBase
    {
        public Test1()
        {
            Name = "Test1 - Pull Required Models";
        }

        public override async Task Run(TestResult result)
        {
            result.Name = Name;
            result.StartUtc = DateTime.UtcNow;
            result.Success = true;

            InitializeTestEnvironment();

            #region Pull-Models

            // Pull embeddings model
            ApiDetails pullEmbeddingsModel = CreateApiDetails("Pull Embeddings Model");
            try
            {
                var embeddingsModelName = TestEnvironment.EmbeddingsModel;

                var pullRequest = new OllamaPullModelRequest { Model = embeddingsModelName };
                var pullStream = SharpAISdk.Ollama.PullModel(pullRequest);

                bool pullSuccess = false;
                await foreach (var progress in pullStream)
                {
                    if (progress.HasError())
                    {
                        Console.WriteLine($"Error pulling {embeddingsModelName}: {progress.Error}");
                        break;
                    }

                    if (progress.IsComplete())
                    {
                        pullSuccess = true;
                        break;
                    }
                }

                if (!pullSuccess)
                {
                    result.Success = false;
                    Console.WriteLine("Failed to pull embeddings model");
                    CompleteApiDetails(pullEmbeddingsModel, "null", 0);
                    result.ApiDetails.Add(pullEmbeddingsModel);
                    return;
                }

                CompleteApiDetails(pullEmbeddingsModel, "Success", 200);
                result.ApiDetails.Add(pullEmbeddingsModel);
            }
            catch (Exception ex)
            {
                result.Success = false;
                Console.WriteLine($"Error pulling embeddings model: {ex.Message}");
                CompleteApiDetails(pullEmbeddingsModel, ex.Message, 500);
                result.ApiDetails.Add(pullEmbeddingsModel);
            }

            // Pull completions model
            ApiDetails pullCompletionsModel = CreateApiDetails("Pull Completions Model");
            try
            {
                var completionsModelName = TestEnvironment.CompletionsModel;

                var pullRequest = new OllamaPullModelRequest { Model = completionsModelName };
                var pullStream = SharpAISdk.Ollama.PullModel(pullRequest);

                bool pullSuccess = false;
                await foreach (var progress in pullStream)
                {
                    if (progress.HasError())
                    {
                        Console.WriteLine($"Error pulling {completionsModelName}: {progress.Error}");
                        break;
                    }

                    if (progress.IsComplete())
                    {
                        pullSuccess = true;
                        break;
                    }
                }

                if (!pullSuccess)
                {
                    result.Success = false;
                    Console.WriteLine("Failed to pull completions model");
                    CompleteApiDetails(pullCompletionsModel, "null", 0);
                    result.ApiDetails.Add(pullCompletionsModel);
                    return;
                }

                CompleteApiDetails(pullCompletionsModel, "Success", 200);
                result.ApiDetails.Add(pullCompletionsModel);
            }
            catch (Exception ex)
            {
                result.Success = false;
                Console.WriteLine($"Error pulling completions model: {ex.Message}");
                CompleteApiDetails(pullCompletionsModel, ex.Message, 500);
                result.ApiDetails.Add(pullCompletionsModel);
            }

            // Pull chat completions model (if different from completions model)
            if (TestEnvironment.ChatCompletionsModel != TestEnvironment.CompletionsModel)
            {
                ApiDetails pullChatCompletionsModel = CreateApiDetails("Pull Chat Completions Model");
                try
                {
                    var chatCompletionsModelName = TestEnvironment.ChatCompletionsModel;

                    var pullRequest = new OllamaPullModelRequest { Model = chatCompletionsModelName };
                    var pullStream = SharpAISdk.Ollama.PullModel(pullRequest);

                    bool pullSuccess = false;
                    await foreach (var progress in pullStream)
                    {
                        if (progress.HasError())
                        {
                            Console.WriteLine($"Error pulling {chatCompletionsModelName}: {progress.Error}");
                            break;
                        }

                        if (progress.IsComplete())
                        {
                            pullSuccess = true;
                            break;
                        }
                    }

                    if (!pullSuccess)
                    {
                        result.Success = false;
                        Console.WriteLine("Failed to pull chat completions model");
                        CompleteApiDetails(pullChatCompletionsModel, "null", 0);
                        result.ApiDetails.Add(pullChatCompletionsModel);
                        return;
                    }

                    CompleteApiDetails(pullChatCompletionsModel, "Success", 200);
                    result.ApiDetails.Add(pullChatCompletionsModel);
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    Console.WriteLine($"Error pulling chat completions model: {ex.Message}");
                    CompleteApiDetails(pullChatCompletionsModel, ex.Message, 500);
                    result.ApiDetails.Add(pullChatCompletionsModel);
                }
            }

            #endregion

            result.EndUtc = DateTime.UtcNow;
        }
    }
}
