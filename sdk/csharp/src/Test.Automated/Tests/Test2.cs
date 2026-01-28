namespace Test.Automated.Tests
{
    using SharpAI.Sdk;

    /// <summary>
    /// Test2: Ollama model list.
    /// This test verifies the ability to list local models.
    /// </summary>
    public class Test2 : TestBase
    {
        public Test2()
        {
            Name = "Test2 - Ollama Model List";
        }

        public override async Task Run(TestResult result)
        {
            result.Name = Name;
            result.StartUtc = DateTime.UtcNow;
            result.Success = true;

            InitializeTestEnvironment();

            #region List-Models

            // List local models
            ApiDetails listModels = CreateApiDetails("List Local Models");
            try
            {
                var models = await SharpAISdk.Ollama.ListLocalModels();

                if (models == null)
                {
                    result.Success = false;
                    Console.WriteLine("No response for list models request");
                    CompleteApiDetails(listModels, "null", 0);
                    result.ApiDetails.Add(listModels);
                    return;
                }


                CompleteApiDetails(listModels, $"Found {models.Count} models", 200);
                result.ApiDetails.Add(listModels);
            }
            catch (Exception ex)
            {
                result.Success = false;
                Console.WriteLine($"Error listing models: {ex.Message}");
                CompleteApiDetails(listModels, ex.Message, 500);
                result.ApiDetails.Add(listModels);
            }

            #endregion

            result.EndUtc = DateTime.UtcNow;
        }
    }
}
