namespace Test.LlamaSharpProvider
{
    using SharpAI.Engines;
    using SyslogLogging;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("AI Provider Demo");
            Console.WriteLine("================");

            EngineBase provider = null;

            try
            {
                while (true)
                {
                    Console.WriteLine("\nOptions:");
                    Console.WriteLine("1. List models");
                    Console.WriteLine("2. Initialize Provider");
                    Console.WriteLine("3. Determine Dimensionality");
                    Console.WriteLine("3. Generate Embeddings");
                    Console.WriteLine("4. Generate Text");
                    Console.WriteLine("5. Chat Completion");
                    Console.WriteLine("6. Streaming Chat");
                    Console.WriteLine();
                    Console.Write("Choice: ");

                    string choice = Console.ReadLine();

                    switch (choice)
                    {
                        case "1":
                            Console.WriteLine();
                            foreach (string file in Directory.GetFiles("models/"))
                                Console.WriteLine(Path.GetFileName(file));
                            break;

                        case "2":
                            provider = await InitializeProvider();
                            break;

                        case "3":
                            Console.WriteLine(provider.GetDimensionality().Result);
                            break;

                        case "4":
                            await GenerateEmbeddings(provider);
                            break;

                        case "5":
                            await GenerateText(provider);
                            break;

                        case "6":
                            await ChatCompletion(provider);
                            break;

                        case "7":
                            await StreamingChat(provider);
                            break;

                        default:
                            Console.WriteLine("Invalid choice.");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error:{Environment.NewLine}{ex.ToString()}");
            }
            finally
            {
                provider?.Dispose();
            }
        }

        private static async Task<EngineBase> InitializeProvider()
        {
            Console.Write("Enter model filename (GGUF): ");
            string modelFile = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(modelFile))
            {
                Console.WriteLine("Model filename cannot be empty.");
                return null;
            }

            string modelPath = Path.Combine("Models", modelFile);
            if (!File.Exists(modelPath))
            {
                Console.WriteLine($"Model file not found: {modelPath}");
                return null;
            }

            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = true;

            LlamaSharpEngine provider = new LlamaSharpEngine(logging);

            try
            {
                await provider.InitializeAsync(modelPath);

                Console.WriteLine($"Provider initialized successfully!");
                Console.WriteLine($"GPU Support: {provider.SupportsGpu}");
                Console.WriteLine($"Supports Embeddings: {provider.SupportsEmbeddings}");
                Console.WriteLine($"Supports Generation: {provider.SupportsGeneration}");
                Console.WriteLine($"Embedding Dimensions: {provider.EmbeddingDimensions}");

                return provider;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize provider:{Environment.NewLine}{ex.ToString()}");
                provider.Dispose();
                return null;
            }
        }

        private static async Task GenerateEmbeddings(EngineBase provider)
        {
            if (provider == null || !provider.IsInitialized)
            {
                Console.WriteLine("Provider not initialized. Please initialize first.");
                return;
            }

            if (!provider.SupportsEmbeddings)
            {
                Console.WriteLine("Provider does not support embeddings.");
                return;
            }

            Console.Write("Enter text to embed: ");
            string text = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(text))
            {
                Console.WriteLine("Text cannot be empty.");
                return;
            }

            try
            {
                float[] embeddings = await provider.GenerateEmbeddingsAsync(text);

                Console.WriteLine($"\nEmbedding generated successfully!");
                Console.WriteLine($"Dimensions: {embeddings.Length}");
                Console.WriteLine($"First 5 values: [{string.Join(", ", embeddings.Take(5).Select(x => x.ToString("F4")))}]");
                Console.WriteLine($"L2 Norm: {Math.Sqrt(embeddings.Sum(x => x * x)):F4}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to generate embeddings:{Environment.NewLine}{ex.ToString()}");
            }
        }

        private static async Task GenerateText(EngineBase provider)
        {
            if (provider == null || !provider.IsInitialized)
            {
                Console.WriteLine("Provider not initialized. Please initialize first.");
                return;
            }

            if (!provider.SupportsGeneration)
            {
                Console.WriteLine("Provider does not support text generation.");
                return;
            }

            Console.WriteLine("Be sure to format your prompt in a way appropriate for the selected model.");
            Console.Write("Prompt: ");
            string prompt = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(prompt))
            {
                Console.WriteLine("Prompt cannot be empty.");
                return;
            }

            Console.Write("Max tokens (default 512): ");
            string maxTokensStr = Console.ReadLine();
            int maxTokens = int.TryParse(maxTokensStr, out int tokens) ? tokens : 512;

            Console.Write("Temperature (default 0.7): ");
            string tempStr = Console.ReadLine();
            float temperature = float.TryParse(tempStr, out float temp) ? temp : 0.7f;

            try
            {
                Console.WriteLine("\nGenerating text...");
                string result = await provider.GenerateTextAsync(prompt, maxTokens, temperature);

                Console.WriteLine($"\nGenerated text:");
                Console.WriteLine($"================");
                Console.WriteLine(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to generate text:{Environment.NewLine}{ex.ToString()}");
            }
        }

        private static async Task ChatCompletion(EngineBase provider)
        {
            if (provider == null || !provider.IsInitialized)
            {
                Console.WriteLine("Provider not initialized. Please initialize first.");
                return;
            }

            if (!provider.SupportsGeneration)
            {
                Console.WriteLine("Provider does not support chat completion.");
                return;
            }

            Console.WriteLine("Be sure to format your prompt in a way appropriate for the selected model.");
            Console.Write("Prompt: ");
            string prompt = Console.ReadLine();

            try
            {
                Console.WriteLine("\nGenerating chat completion...");
                string result = await provider.GenerateChatCompletionAsync(prompt);

                Console.WriteLine();
                Console.WriteLine(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to generate chat completion:{Environment.NewLine}{ex.ToString()}");
            }
        }

        private static async Task StreamingChat(EngineBase provider)
        {
            if (provider == null || !provider.IsInitialized)
            {
                Console.WriteLine("Provider not initialized. Please initialize first.");
                return;
            }

            if (!provider.SupportsGeneration)
            {
                Console.WriteLine("Provider does not support streaming chat.");
                return;
            }

            Console.WriteLine("Be sure to format your prompt in a way appropriate for the selected model.");
            Console.Write("Prompt: ");
            string prompt = Console.ReadLine();

            try
            {
                Console.WriteLine();

                await foreach (string token in provider.GenerateChatCompletionStreamAsync(prompt))
                {
                    Console.Write(token);
                }

                Console.WriteLine("\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to generate chat completion:{Environment.NewLine}{ex.ToString()}");
            }
        }
    }
}