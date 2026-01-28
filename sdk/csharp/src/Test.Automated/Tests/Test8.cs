namespace Test.Automated.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Test8 - OpenAI Singular and Multiple Embeddings
    /// </summary>
    public class Test8 : TestBase
    {
        public Test8()
        {
            Name = "Test8 - OpenAI Singular and Multiple Embeddings";
        }

        public override async Task Run(TestResult result)
        {
            result.Name = Name;
            result.StartUtc = DateTime.UtcNow;
            result.Success = true;

            InitializeTestEnvironment();

            #region Single-Embeddings

            // Test single embeddings
            ApiDetails singleEmbeddings = CreateApiDetails("OpenAI Single Embeddings");
            try
            {
                var request = Helpers.CreateOpenAISingleEmbeddingsRequest(TestEnvironment.EmbeddingsModel, "This is a test sentence for generating embeddings.");
                var embeddingsResult = await SharpAISdk.OpenAI.GenerateEmbeddingsAsync(request);
                
                if (!Helpers.ValidateOpenAIEmbeddingsResult(embeddingsResult, 1))
                {
                    result.Success = false;
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
                CompleteApiDetails(singleEmbeddings, ex.Message, 500);
                result.ApiDetails.Add(singleEmbeddings);
            }

            #endregion

            #region Multiple-Embeddings

            ApiDetails multiEmbeddings = CreateApiDetails("OpenAI Multiple Embeddings");
            try
            {
                var inputs = new List<string> { "First test sentence", "Second test sentence" };
                var request = Helpers.CreateOpenAIMultipleEmbeddingsRequest(TestEnvironment.EmbeddingsModel, inputs);
                var embeddingsResult = await SharpAISdk.OpenAI.GenerateEmbeddingsAsync(request);
                
                if (!Helpers.ValidateOpenAIEmbeddingsResult(embeddingsResult, 2))
                {
                    result.Success = false;
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
                CompleteApiDetails(multiEmbeddings, ex.Message, 500);
                result.ApiDetails.Add(multiEmbeddings);
            }

            #endregion

            result.EndUtc = DateTime.UtcNow;
        }
    }
}