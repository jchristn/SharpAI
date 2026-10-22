namespace Test.Automated.Tests
{
    using SharpAI.Models.Ollama;
    using SharpAI.Models.OpenAI;
    using SharpAI.Sdk;

    /// <summary>Live consumer contracts using an already-imported local operations model.</summary>
    internal static class OperationsContracts
    {
        internal static async Task RunAsync(string endpoint)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var token = timeout.Token;
            using var sdk = new SharpAISdk(endpoint);
            var models = await sdk.Ollama.ListLocalModels(token);
            Require(models != null && models.Any(model => model.Name == "ops-story"), "model listing");

            var ollama = await sdk.Ollama.GenerateCompletion(new OllamaGenerateCompletionRequest
            {
                Model = "ops-story", Prompt = "Once upon a time", Stream = false,
                Options = new OllamaCompletionOptions { NumPredict = 4, Temperature = 0 }
            }, token);
            Require(ollama != null && ollama.EvalCount == 4 && ollama.PromptEvalCount > 0 &&
                ollama.EvalDuration > 0 && ollama.TotalDuration > 0, "Ollama counts and timings");

            var completion = await sdk.OpenAI.GenerateCompletionAsync(new OpenAIGenerateCompletionRequest
                { Model = "ops-story", Prompt = "Once upon a time", MaxTokens = 4, Temperature = 0, Stream = false }, token);
            Usage(completion?.Usage, "OpenAI completion usage");

            var chat = await sdk.OpenAI.GenerateChatCompletionAsync(new OpenAIGenerateChatCompletionRequest
            {
                Model = "ops-story", Messages = new List<OpenAIChatMessage> { new OpenAIChatMessage { Role = "user", Content = "Once upon a time" } },
                MaxTokens = 4, Temperature = 0, Stream = false
            }, token);
            Usage(chat?.Usage, "OpenAI chat usage");

            int chunks = 0; bool finished = false;
            await foreach (var chunk in sdk.OpenAI.GenerateCompletionStreamAsync(new OpenAIGenerateCompletionRequest
                { Model = "ops-story", Prompt = "Once upon a time", MaxTokens = 4, Temperature = 0 }, token))
            {
                chunks++;
                if (chunk.Choices?.Any(choice => !String.IsNullOrEmpty(choice.FinishReason)) == true) finished = true;
            }
            Require(chunks > 1 && finished, "OpenAI streaming terminal event");

            int ollamaChunks = 0; bool ollamaDone = false;
            await foreach (var chunk in sdk.Ollama.GenerateCompletionStream(new OllamaGenerateCompletionRequest
                { Model = "ops-story", Prompt = "Once upon a time", Stream = true, Options = new OllamaCompletionOptions { NumPredict = 4, Temperature = 0 } }, token))
            {
                ollamaChunks++; ollamaDone |= chunk.Done;
            }
            Require(ollamaChunks > 1 && ollamaDone, "Ollama NDJSON streaming terminal event");

            var settings = await sdk.Admin.GetSettingsAsync(token);
            Require(settings.HasValue && settings.Value.ValueKind == System.Text.Json.JsonValueKind.Object, "admin settings JSON");
        }

        private static void Usage(OpenAIUsage? usage, string name)
        {
            Require(usage != null && usage.PromptTokens > 0 && usage.CompletionTokens == 4 &&
                usage.TotalTokens == usage.PromptTokens + usage.CompletionTokens, name);
        }

        private static void Require(bool success, string name)
        {
            if (!success) throw new InvalidOperationException("C# SDK contract failed: " + name);
            Console.WriteLine("PASS: " + name);
        }
    }
}
