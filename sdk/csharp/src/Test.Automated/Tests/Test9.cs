namespace Test.Automated.Tests
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Test9 - Error Handling with Invalid Models
    /// </summary>
    public class Test9 : TestBase
    {
        public Test9()
        {
            Name = "Test9 - Error Handling with Invalid Models";
        }

        public override async Task Run(TestResult result)
        {
            result.Name = Name;
            result.StartUtc = DateTime.UtcNow;
            result.Success = true;

            InitializeTestEnvironment();

            #region Invalid-Embeddings-Model

            // Test invalid embeddings model
            ApiDetails invalidEmbeddings = CreateApiDetails("Invalid Embeddings Model");
            try
            {
                SharpAI.Models.Ollama.OllamaGenerateEmbeddingsRequest? request = Helpers.CreateOllamaSingleEmbeddingsRequest("invalid-model-name", "test");
                SharpAI.Models.Ollama.OllamaGenerateEmbeddingsResult? embeddingsResult = await SharpAISdk.Ollama.GenerateEmbeddings(request);
                
                if (embeddingsResult == null)
                {
                    CompleteApiDetails(invalidEmbeddings, "null", 200);
                    result.ApiDetails.Add(invalidEmbeddings);
                }
                else
                {
                    Console.WriteLine("Unexpected success with invalid embeddings model");
                    CompleteApiDetails(invalidEmbeddings, "Unexpected success", 200);
                    result.ApiDetails.Add(invalidEmbeddings);
                }
            }
            catch (Exception ex)
            {
                CompleteApiDetails(invalidEmbeddings, ex.Message, 500);
                result.ApiDetails.Add(invalidEmbeddings);
            }

            #endregion

            #region Invalid-Completions-Model

            ApiDetails invalidCompletions = CreateApiDetails("Invalid Completions Model");
            try
            {
                SharpAI.Models.Ollama.OllamaGenerateCompletionRequest? request = Helpers.CreateOllamaCompletionRequest("invalid-model-name", "What is the capital of France?", false);
                SharpAI.Models.Ollama.OllamaGenerateCompletionResult? completionResult = await SharpAISdk.Ollama.GenerateCompletion(request);
                
                if (completionResult == null)
                {
                    CompleteApiDetails(invalidCompletions, "null", 200);
                    result.ApiDetails.Add(invalidCompletions);
                }
                else
                {
                    Console.WriteLine("Unexpected success with invalid completions model");
                    CompleteApiDetails(invalidCompletions, "Unexpected success", 200);
                    result.ApiDetails.Add(invalidCompletions);
                }
            }
            catch (Exception ex)
            {
                CompleteApiDetails(invalidCompletions, ex.Message, 500);
                result.ApiDetails.Add(invalidCompletions);
            }

            #endregion

            #region Invalid-OpenAI-Model

            ApiDetails invalidOpenAI = CreateApiDetails("Invalid OpenAI Model");
            try
            {
                SharpAI.Models.OpenAI.OpenAIGenerateCompletionRequest? request = Helpers.CreateOpenAICompletionRequest("invalid-model-name", "What is the capital of France?", false);
                SharpAI.Models.OpenAI.OpenAIGenerateCompletionResult? completionResult = await SharpAISdk.OpenAI.GenerateCompletionAsync(request);
                
                if (completionResult == null)
                {
                    CompleteApiDetails(invalidOpenAI, "null", 200);
                    result.ApiDetails.Add(invalidOpenAI);
                }
                else
                {
                    Console.WriteLine("Unexpected success with invalid OpenAI model");
                    CompleteApiDetails(invalidOpenAI, "Unexpected success", 200);
                    result.ApiDetails.Add(invalidOpenAI);
                }
            }
            catch (Exception ex)
            {
                CompleteApiDetails(invalidOpenAI, ex.Message, 500);
                result.ApiDetails.Add(invalidOpenAI);
            }

            #endregion

            result.EndUtc = DateTime.UtcNow;
        }
    }
}
