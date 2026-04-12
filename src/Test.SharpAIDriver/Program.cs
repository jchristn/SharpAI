namespace Test.SharpAIDriver
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using SyslogLogging;
    using GetSomeInput;
    using SharpAI;

    public static class Program
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

        static bool _RunForever = true;
        static bool _Debug = false;
        static AIDriver _AIDriver = null;
        static LoggingModule _Logging = null;
        static string _DatabaseFilename = "./sharpai.db";
        static string _HuggingFaceApiKey = null;
        static string _ModelDirectory = "./models/";
        static string _CurrentModel = null;

        public static async Task Main(string[] args)
        {
            InitializeDriver();

            while (_RunForever)
            {
                string userInput = Inputty.GetString("Command [? for help]:", null, false);

                if (userInput.Equals("?")) Menu();
                else if (userInput.Equals("q")) _RunForever = false;
                else if (userInput.Equals("cls")) Console.Clear();
                else if (userInput.Equals("debug")) ToggleDebug();
                else if (userInput.Equals("model")) SetCurrentModel();
                else
                {
                    string[] parts = userInput.Split(new char[] { ' ' }, 2);

                    if (parts.Length >= 1)
                    {
                        if (parts[0].Equals("models"))
                        {
                            if (parts.Length == 2)
                            {
                                if (parts[1].Equals("add")) await AddModel();
                                else if (parts[1].Equals("list")) ListModels();
                                else if (parts[1].Equals("delete")) DeleteModel();
                                else if (parts[1].Equals("info")) ModelInfo();
                            }
                            else
                            {
                                Console.WriteLine("Please specify a models command: add, list, delete, info");
                            }
                        }
                        else if (parts[0].Equals("embeddings"))
                        {
                            if (parts.Length == 2)
                            {
                                if (parts[1].Equals("generate")) await GenerateEmbeddings();
                                else if (parts[1].Equals("batch")) await GenerateEmbeddingsBatch();
                            }
                            else
                            {
                                Console.WriteLine("Please specify an embeddings command: generate, batch");
                            }
                        }
                        else if (parts[0].Equals("completion"))
                        {
                            if (parts.Length == 2)
                            {
                                if (parts[1].Equals("generate")) await GenerateCompletion();
                                else if (parts[1].Equals("stream")) await GenerateCompletionStream();
                            }
                            else
                            {
                                Console.WriteLine("Please specify a completion command: generate, stream");
                            }
                        }
                        else if (parts[0].Equals("chat"))
                        {
                            if (parts.Length == 2)
                            {
                                if (parts[1].Equals("generate")) await GenerateChatCompletion();
                                else if (parts[1].Equals("stream")) await GenerateChatCompletionStream();
                            }
                            else
                            {
                                Console.WriteLine("Please specify a chat command: generate, stream, interactive");
                            }
                        }
                        else if (parts[0].Equals("vision"))
                        {
                            Console.WriteLine("Vision support has been removed (LLamaSharp 0.26.0 API change).");
                        }
                        else if (parts[0].Equals("test"))
                        {
                            if (parts.Length == 2)
                            {
                                if (parts[1].Equals("all")) await TestAll();
                                else if (parts[1].Equals("embeddings")) await TestEmbeddings();
                                else if (parts[1].Equals("completion")) await TestCompletion();
                                else if (parts[1].Equals("chat")) await TestChat();
                                else if (parts[1].Equals("parallel")) await TestParallelEmbeddings();
                            }
                            else
                            {
                                Console.WriteLine("Please specify a test command: all, embeddings, completion, chat, parallel");
                            }
                        }
                    }
                }
            }
        }

        static void InitializeDriver()
        {
            Console.WriteLine("");
            Console.WriteLine("SharpAI Driver Test Program");
            Console.WriteLine("===========================");
            Console.WriteLine("");

            _DatabaseFilename = Inputty.GetString("Database filename:", _DatabaseFilename, false);
            _HuggingFaceApiKey = Inputty.GetString("HuggingFace API key:", _HuggingFaceApiKey, false);
            _ModelDirectory = Inputty.GetString("Model directory:", _ModelDirectory, false);
            _Debug = Inputty.GetBoolean("Enable debug logging:", _Debug);

            _Logging = new LoggingModule();
            _Logging.Settings.EnableConsole = true;

            Console.WriteLine("");
            Console.WriteLine("Initializing AI Driver...");

            try
            {
                _AIDriver = new AIDriver(
                    _Logging,
                    _DatabaseFilename,
                    _HuggingFaceApiKey,
                    _ModelDirectory);

                Console.WriteLine("AI Driver initialized successfully!");
                Console.WriteLine("");

                // Check for existing models
                var models = _AIDriver.Models.All();
                if (models.Count > 0)
                {
                    Console.WriteLine($"Found {models.Count} existing model(s):");
                    foreach (var model in models)
                    {
                        Console.WriteLine($"  - {model.Name} ({model.ContentLength / 1024 / 1024} MB)");
                    }
                    Console.WriteLine("");

                    if (models.Count == 1)
                    {
                        _CurrentModel = models.First().Name;
                        Console.WriteLine($"Auto-selected model: {_CurrentModel}");
                    }
                    else
                    {
                        Console.WriteLine("Use 'model' command to select a model");
                    }
                }
                else
                {
                    Console.WriteLine("No models found. Use 'models add' to download a model.");
                    Console.WriteLine();
                    Console.WriteLine("Two recommended models:");
                    Console.WriteLine("- For embeddings  : leliuga/all-MiniLM-L6-v2-GGUF");
                    Console.WriteLine("- For completions : QuantFactory/Qwen2.5-3B-GGUF");
                    Console.WriteLine();
                }
                Console.WriteLine("");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing AI Driver:{Environment.NewLine}{ex.ToString()}");
                Environment.Exit(1);
            }
        }

        static void Menu()
        {
            Console.WriteLine("");
            Console.WriteLine("Available commands:");
            Console.WriteLine("  ?               help, this menu");
            Console.WriteLine("  q               quit");
            Console.WriteLine("  cls             clear the screen");
            Console.WriteLine("  debug           enable or disable debug (enabled: " + _Debug + ")");
            Console.WriteLine("  model           set the current model (currently: " + (_CurrentModel ?? "none") + ")");
            Console.WriteLine("");
            Console.WriteLine("Model Management:");
            Console.WriteLine("  models add      download and add a model from HuggingFace");
            Console.WriteLine("  models list     list all available models");
            Console.WriteLine("  models delete   delete a model");
            Console.WriteLine("  models info     show detailed information about a model");
            Console.WriteLine("");
            Console.WriteLine("Embeddings:");
            Console.WriteLine("  embeddings generate   generate embeddings for a single input");
            Console.WriteLine("  embeddings batch      generate embeddings for multiple inputs");
            Console.WriteLine("");
            Console.WriteLine("Completions:");
            Console.WriteLine("  completion generate   generate a completion (non-streaming)");
            Console.WriteLine("  completion stream     generate a completion (streaming)");
            Console.WriteLine("");
            Console.WriteLine("Chat:");
            Console.WriteLine("  chat generate    generate a chat completion (non-streaming)");
            Console.WriteLine("  chat stream      generate a chat completion (streaming)");
            Console.WriteLine("");
            Console.WriteLine("Tests:");
            Console.WriteLine("  test all         run all tests");
            Console.WriteLine("  test embeddings  test embeddings generation");
            Console.WriteLine("  test completion  test completion generation");
            Console.WriteLine("  test chat        test chat completion generation");
            Console.WriteLine("  test parallel    test parallel embedding generation (threading issues)");
            Console.WriteLine("");
        }

        static void ToggleDebug()
        {
            _Debug = !_Debug;
            _Logging.Settings.EnableConsole = !_Logging.Settings.EnableConsole;
            Console.WriteLine($"Debug logging: {(_Debug ? "enabled" : "disabled")}");
        }

        static void SetCurrentModel()
        {
            var models = _AIDriver.Models.All();
            if (models.Count == 0)
            {
                Console.WriteLine("No models available. Use 'models add' to download a model.");
                return;
            }

            Console.WriteLine("Available models:");
            for (int i = 0; i < models.Count; i++)
            {
                Console.WriteLine($"  {i + 1}. {models[i].Name}");
            }

            int selection = Inputty.GetInteger("Select model number:", 1, true, true);
            if (selection > 0)
            {
                _CurrentModel = models[selection - 1].Name;
                Console.WriteLine($"Current model set to: {_CurrentModel}");
            }
        }

        #region Model-Management

        static async Task AddModel()
        {
            string modelName = Inputty.GetString("Model name (e.g., 'TheBloke/Llama-2-7B-Chat-GGUF'):", null, false);

            Console.WriteLine($"Attempting to download model '{modelName}'...");
            Console.WriteLine("This may take several minutes depending on the model size and your internet connection.");

            try
            {
                Action<string, long, decimal> progressCallback = (filename, bytesDownloaded, percentComplete) =>
                {
                    if (percentComplete < 0)
                    {
                        Console.WriteLine($"\n\n❌ Download failed: {filename}");
                    }
                    else if (percentComplete >= 1.0m)
                    {
                        Console.WriteLine($"\n\n✅ Download complete: {filename}");
                        Console.WriteLine($"   Total size: {bytesDownloaded}");
                    }
                    else
                    {
                        Console.Write($"\rDownloaded: {bytesDownloaded}        ");
                    }
                };

                var model = await _AIDriver.Models.Add(modelName, new Dictionary<string, int>(), progressCallback);
                Console.WriteLine($"Successfully added model: {model.Name}");
                Console.WriteLine($"  GUID: {model.GUID}");
                Console.WriteLine($"  Size: {model.ContentLength / 1024 / 1024} MB");
                Console.WriteLine($"  Quantization: {model.Quantization}");
                Console.WriteLine($"  SHA256: {model.SHA256Hash}");

                if (String.IsNullOrEmpty(_CurrentModel))
                {
                    _CurrentModel = model.Name;
                    Console.WriteLine($"Auto-selected as current model: {_CurrentModel}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding model:{Environment.NewLine}{ex.ToString()}");
            }
        }

        static void ListModels()
        {
            var models = _AIDriver.Models.All();
            if (models.Count == 0)
            {
                Console.WriteLine("No models found.");
                return;
            }

            Console.WriteLine($"Found {models.Count} model(s):");
            Console.WriteLine("");
            foreach (var model in models)
            {
                Console.WriteLine($"Name: {model.Name}");
                Console.WriteLine($"  GUID: {model.GUID}");
                Console.WriteLine($"  Size: {model.ContentLength / 1024 / 1024} MB");
                Console.WriteLine($"  Quantization: {model.Quantization}");
                Console.WriteLine($"  Created: {model.CreatedUtc}");
                if (model.Name == _CurrentModel)
                {
                    Console.WriteLine("  ** CURRENT MODEL **");
                }
                Console.WriteLine("");
            }
        }

        static void DeleteModel()
        {
            var models = _AIDriver.Models.All();
            if (models.Count == 0)
            {
                Console.WriteLine("No models available to delete.");
                return;
            }

            Console.WriteLine("Available models:");
            for (int i = 0; i < models.Count; i++)
            {
                Console.WriteLine($"  {i + 1}. {models[i].Name}");
            }

            int selection = Inputty.GetInteger("Select model number to delete:", 1, true, true);
            if (selection > 0)
            {
                var modelToDelete = models[selection - 1];
                bool confirm = Inputty.GetBoolean($"Are you sure you want to delete '{modelToDelete.Name}'?", false);

                if (confirm)
                {
                    try
                    {
                        _AIDriver.Models.Delete(modelToDelete.Name);
                        Console.WriteLine($"Successfully deleted model: {modelToDelete.Name}");

                        if (_CurrentModel == modelToDelete.Name)
                        {
                            _CurrentModel = null;
                            Console.WriteLine("Current model selection cleared.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deleting model:{Environment.NewLine}{ex.ToString()}");
                    }
                }
            }
        }

        static void ModelInfo()
        {
            string modelName = _CurrentModel;
            if (String.IsNullOrEmpty(modelName))
            {
                modelName = Inputty.GetString("Model name:", null, false);
            }

            try
            {
                var model = _AIDriver.Models.GetByName(modelName);
                if (model == null)
                {
                    Console.WriteLine($"Model '{modelName}' not found.");
                    return;
                }

                Console.WriteLine($"Model Information for: {model.Name}");
                Console.WriteLine($"  GUID: {model.GUID}");
                Console.WriteLine($"  Format: {model.Format}");
                Console.WriteLine($"  Family: {model.Family}");
                Console.WriteLine($"  Parent Model: {model.ParentModel ?? "N/A"}");
                Console.WriteLine($"  Parameter Size: {model.ParameterCount.ToString()}");
                Console.WriteLine($"  Quantization: {model.Quantization}");
                Console.WriteLine($"  Size: {model.ContentLength / 1024 / 1024} MB ({model.ContentLength} bytes)");
                Console.WriteLine($"  MD5: {model.MD5Hash}");
                Console.WriteLine($"  SHA1: {model.SHA1Hash}");
                Console.WriteLine($"  SHA256: {model.SHA256Hash}");
                Console.WriteLine($"  Source URL: {model.SourceUrl}");
                Console.WriteLine($"  Model Creation: {model.ModelCreationUtc?.ToString() ?? "Unknown"}");
                Console.WriteLine($"  Downloaded: {model.CreatedUtc}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving model info:{Environment.NewLine}{ex.ToString()}");
            }
        }

        #endregion

        #region Embeddings

        static async Task GenerateEmbeddings()
        {
            if (!EnsureModelSelected()) return;

            string input = Inputty.GetString("Enter text to generate embeddings for:", null, false);

            Console.WriteLine($"Generating embeddings using model '{_CurrentModel}'...");

            try
            {
                var embeddings = await _AIDriver.Embeddings.Generate(_CurrentModel, input);

                Console.WriteLine($"Generated {embeddings.Length} dimensional embeddings:");
                Console.WriteLine("");

                // Show first 10 values
                int displayCount = Math.Min(10, embeddings.Length);
                for (int i = 0; i < displayCount; i++)
                {
                    Console.WriteLine($"  [{i}]: {embeddings[i]:F6}");
                }

                if (embeddings.Length > displayCount)
                {
                    Console.WriteLine($"  ... ({embeddings.Length - displayCount} more values)");
                }

                Console.WriteLine("");
                Console.WriteLine($"Embeddings summary:");
                Console.WriteLine($"  Dimensions: {embeddings.Length}");
                Console.WriteLine($"  Min value: {embeddings.Min():F6}");
                Console.WriteLine($"  Max value: {embeddings.Max():F6}");
                Console.WriteLine($"  Mean value: {embeddings.Average():F6}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating embeddings:{Environment.NewLine}{ex.ToString()}");
            }
        }

        static async Task GenerateEmbeddingsBatch()
        {
            if (!EnsureModelSelected()) return;

            Console.WriteLine("Enter texts to generate embeddings for (empty line to finish):");
            var inputs = new List<string>();

            while (true)
            {
                string input = Inputty.GetString($"Text {inputs.Count + 1}:", null, true);
                if (String.IsNullOrEmpty(input)) break;
                inputs.Add(input);
            }

            if (inputs.Count == 0)
            {
                Console.WriteLine("No inputs provided.");
                return;
            }

            Console.WriteLine($"Generating embeddings for {inputs.Count} texts using model '{_CurrentModel}'...");

            try
            {
                var embeddingsBatch = await _AIDriver.Embeddings.Generate(_CurrentModel, inputs.ToArray());

                Console.WriteLine($"Generated embeddings for {embeddingsBatch.Length} texts:");
                Console.WriteLine("");

                for (int i = 0; i < embeddingsBatch.Length; i++)
                {
                    Console.WriteLine($"Text {i + 1}: \"{inputs[i]}\"");
                    Console.WriteLine($"  Dimensions: {embeddingsBatch[i].Length}");
                    Console.WriteLine($"  First 5 values: [{string.Join(", ", embeddingsBatch[i].Take(5).Select(v => v.ToString("F4")))}]");
                    Console.WriteLine("");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating embeddings batch:{Environment.NewLine}{ex.ToString()}");
            }
        }

        #endregion

        #region Completions

        static async Task GenerateCompletion()
        {
            if (!EnsureModelSelected()) return;

            string prompt = Inputty.GetString("Enter prompt:", null, false);
            int maxTokens = Inputty.GetInteger("Max tokens:", 512, true, false);
            float temperature = (float)Inputty.GetDecimal("Temperature (0.0-1.0):", 0.7m, true, true);

            Console.WriteLine($"Generating completion using model '{_CurrentModel}'...");
            Console.WriteLine("");

            try
            {
                var completion = await _AIDriver.Completion.GenerateCompletion(
                    _CurrentModel,
                    prompt,
                    maxTokens,
                    temperature);

                Console.WriteLine("Completion:");
                Console.WriteLine("===========");
                Console.WriteLine(completion);
                Console.WriteLine("===========");
                Console.WriteLine($"Length: {completion.Length} characters");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating completion:{Environment.NewLine}{ex.ToString()}");
            }
        }

        static async Task GenerateCompletionStream()
        {
            if (!EnsureModelSelected()) return;

            string prompt = Inputty.GetString("Enter prompt:", null, false);
            int maxTokens = Inputty.GetInteger("Max tokens:", 512, true, false);
            float temperature = (float)Inputty.GetDecimal("Temperature (0.0-1.0):", 0.7m, true, true);

            Console.WriteLine($"Generating completion using model '{_CurrentModel}'...");
            Console.WriteLine("");
            Console.WriteLine("Completion:");
            Console.WriteLine("===========");

            try
            {
                int charCount = 0;
                await foreach (var chunk in _AIDriver.Completion.GenerateCompletionStreaming(
                    _CurrentModel,
                    prompt,
                    maxTokens,
                    temperature))
                {
                    Console.Write(chunk);
                    charCount += chunk.Length;
                }

                Console.WriteLine("");
                Console.WriteLine("============");
                Console.WriteLine($"Length: {charCount} characters");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating completion stream:{Environment.NewLine}{ex.ToString()}");
            }
        }

        #endregion

        #region Chat

        static async Task GenerateChatCompletion()
        {
            if (!EnsureModelSelected()) return;

            Console.WriteLine();
            Console.WriteLine("Be sure to construct your prompt using a format understandable by the selected model.");
            string prompt = Inputty.GetString("Prompt:", null, true);
            if (String.IsNullOrEmpty(prompt)) return;

            int maxTokens = Inputty.GetInteger("Max tokens:", 512, true, false);
            float temperature = (float)Inputty.GetDecimal("Temperature (0.0-1.0):", 0.7m, true, true);

            Console.WriteLine($"Generating chat completion using model '{_CurrentModel}'...");
            Console.WriteLine("");

            try
            {
                var completion = await _AIDriver.Chat.GenerateCompletion(
                    _CurrentModel,
                    prompt,
                    maxTokens,
                    temperature);

                Console.WriteLine("Assistant Response:");
                Console.WriteLine("==================");
                Console.WriteLine(completion);
                Console.WriteLine("==================");
                Console.WriteLine($"Length: {completion.Length} characters");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating chat completion:{Environment.NewLine}{ex.ToString()}");
            }
        }

        static async Task GenerateChatCompletionStream()
        {
            if (!EnsureModelSelected()) return;

            Console.WriteLine();
            Console.WriteLine("Be sure to construct your prompt using a format understandable by the selected model.");
            string prompt = Inputty.GetString("Prompt:", null, true);
            if (String.IsNullOrEmpty(prompt)) return;

            int maxTokens = Inputty.GetInteger("Max tokens:", 512, true, false);
            float temperature = (float)Inputty.GetDecimal("Temperature (0.0-1.0):", 0.7m, true, true);

            Console.WriteLine($"Generating chat completion using model '{_CurrentModel}'...");
            Console.WriteLine("");
            Console.WriteLine("Assistant Response:");
            Console.WriteLine("==================");

            try
            {
                int charCount = 0;
                await foreach (var chunk in _AIDriver.Chat.GenerateCompletionStreaming(
                    _CurrentModel,
                    prompt,
                    maxTokens,
                    temperature))
                {
                    Console.Write(chunk);
                    charCount += chunk.Length;
                }

                Console.WriteLine("");
                Console.WriteLine("==================");
                Console.WriteLine($"Length: {charCount} characters");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating chat completion stream:{Environment.NewLine}{ex.ToString()}");
            }
        }

        #endregion

        #region Tests

        static async Task TestAll()
        {
            await TestEmbeddings();
            await TestCompletion();
            await TestChat();
            await TestParallelEmbeddings();
        }

        static async Task TestEmbeddings()
        {
            if (!EnsureModelSelected()) return;

            Console.WriteLine("=== Testing Embeddings ===");

            try
            {
                // Test single embedding
                Console.WriteLine("Test 1: Single text embedding");
                var embedding = await _AIDriver.Embeddings.Generate(_CurrentModel, "Hello, world!");
                Console.WriteLine($"✓ Generated embedding with {embedding.Length} dimensions");

                // Test batch embeddings
                Console.WriteLine("\nTest 2: Batch embeddings");
                var batch = await _AIDriver.Embeddings.Generate(_CurrentModel, new[] { "First text", "Second text", "Third text" });
                Console.WriteLine($"✓ Generated {batch.Length} embeddings");

                // Test similarity
                Console.WriteLine("\nTest 3: Semantic similarity");
                var similar1 = await _AIDriver.Embeddings.Generate(_CurrentModel, "The cat sat on the mat");
                var similar2 = await _AIDriver.Embeddings.Generate(_CurrentModel, "A feline rested on the rug");
                var different = await _AIDriver.Embeddings.Generate(_CurrentModel, "The stock market crashed today");

                double sim1 = CosineSimilarity(similar1, similar2);
                double sim2 = CosineSimilarity(similar1, different);

                Console.WriteLine($"✓ Similarity (similar texts): {sim1:F4}");
                Console.WriteLine($"✓ Similarity (different texts): {sim2:F4}");

                Console.WriteLine("\n✓ All embedding tests passed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Embedding test failed:{Environment.NewLine}{ex.ToString()}");
            }
        }

        static async Task TestCompletion()
        {
            if (!EnsureModelSelected()) return;

            Console.WriteLine("\n=== Testing Completions ===");

            try
            {
                // Test non-streaming
                Console.WriteLine("Test 1: Non-streaming completion");
                var completion = await _AIDriver.Completion.GenerateCompletion(
                    _CurrentModel,
                    "The capital of France is",
                    100,
                    0.1f);
                Console.WriteLine($"✓ Generated completion: {completion.Trim()}");

                // Test streaming
                Console.WriteLine("\nTest 2: Streaming completion");
                Console.Write("✓ Streaming response: ");
                await foreach (var chunk in _AIDriver.Completion.GenerateCompletionStreaming(
                    _CurrentModel,
                    "List three colors:",
                    100,
                    0.5f))
                {
                    Console.Write(".");
                }
                Console.WriteLine(" Complete!");

                Console.WriteLine("\n✓ All completion tests passed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Completion test failed:{Environment.NewLine}{ex.ToString()}");
            }
        }

        static async Task TestChat()
        {
            if (!EnsureModelSelected()) return;

            Console.WriteLine("\n=== Testing Chat ===");

            try
            {
                string prompt = """
                    system: You are a helpful assistant. Introduce yourself as 'Steve' when you respond.
                    user: What is 2+2?
                    assistant: 
                    """;

                // Test non-streaming
                Console.WriteLine("Test 1: Non-streaming chat");
                var response = await _AIDriver.Chat.GenerateCompletion(
                    _CurrentModel,
                    prompt,
                    100,
                    0.1f);
                Console.WriteLine($"✓ Generated response: {response.Trim()}");

                // Test streaming
                Console.WriteLine("\nTest 2: Streaming chat");
                prompt = $"""
                    system: You are a helpful assistant.  Introduce yourself as 'Steve'.
                    user: What is 2+2?
                    assistant: {response}
                    user: Could you explain why?
                    assistant: 
                    """;

                Console.Write("✓ Streaming response: ");
                await foreach (var chunk in _AIDriver.Chat.GenerateCompletionStreaming(
                    _CurrentModel,
                    prompt,
                    200,
                    0.5f))
                {
                    Console.Write(".");
                }
                Console.WriteLine(" Complete!");

                Console.WriteLine("\n✓ All chat tests passed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Chat test failed:{Environment.NewLine}{ex.ToString()}");
            }
        }

        static async Task TestParallelEmbeddings()
        {
            if (!EnsureModelSelected()) return;

            Console.WriteLine("\n=== Testing Parallel Embedding Generation ===");
            try
            {
                Console.WriteLine("\n--- Test 1: Test Embedding Generation ---");
                await TestIntensiveParallelEmbeddings();

                Console.WriteLine("\n✓ All parallel embedding tests completed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Parallel embedding test failed:{Environment.NewLine}{ex.ToString()}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        #region Parallel Embedding Test Methods

        static async Task TestIntensiveParallelEmbeddings()
        {
            try
            {
                var texts = new string[500];
                List<string> textList = new List<string>
    {
        @"8/23/24, 8:59 PM Botulinum toxin - Wikipedia",
        @"Botulinum toxin",
        @"Botulinum toxin, or botulinum neurotoxin (commonly called botox), is a highly potent neurotoxic protein produced by the bacterium Clostridium [23] and related species. It prevents the botulinum release of the neurotransmitter acetylcholine from axon endings at the neuromuscular junction, thus causing [24] flaccid paralysis. The toxin causes the disease [25] . The toxin is also used commercially for botulism [26][27] medical and cosmetic purposes. Botulinum toxin is an acetylcholine release inhibitor and a [1][22] neuromuscular blocking agent.",
        @"Botulinum toxin A",
        @"The seven main types of botulinum toxin are named [26][28] types A to G (A, B, C1, C2, D, E, F and G). New [29][30] types are occasionally found. Types A and B are capable of causing disease in humans, and are also used [31][32][33] commercially and medically. Types C–G are less common; types E and F can cause disease in humans, while the other types cause disease in other [34] animals.",
        @"Ribbon diagram of tertiary structure of BotA (P0DPI1 (https://www.uniprot.org/uniprot/P0DP I1)). PDB entry 3BTA (https://www.ebi.ac.uk/pd be/entry/pdb/3BTA). Clinical data Trade names Botox, Myobloc, Jeuveau, others Other names BoNT, botox Biosimilars abobotulinumtoxinA, daxibotulinumtoxinA, daxibotulinumtoxinA-lanm, evabotulinumtoxinA, incobotulinumtoxinA, letibotulinumtoxinA, [1] letibotulinumtoxinA-wlbg, onabotulinumtoxinA, prabotulinumtoxinA, relabotulinumtoxinA, rimabotulinumtoxinB AHFS/Drugs.com abobotulinumtoxinA Monograph (https://www.dr ugs.com/monograph/abob otulinumtoxina.html)",
        @"Botulinum toxins are among the most potent toxins [35][36] known to science. Intoxication can occur naturally as a result of either wound or intestinal infection or by ingesting formed toxin in food. The estimated human median lethal dose of type A toxin is 1.3–2.1 ng/kg intravenously or intramuscularly, 10– 13 ng/kg when inhaled, or 1000 ng/kg when taken by [37] mouth.",
        @"Medical uses",
        @"Botulinum toxin is used to treat a number of therapeutic indications, many of which are not part of the approved [27] drug label.",
        @"Muscle spasticity",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 1/33",
        @"botulinum toxin wikipedia botulinum toxin used treat number disorder daxibotulinumtoxina characteriz overact muscle movem include monograph http www cerebr palsy spastic com monograph daxib post stroke post otulinumtoxina html spin cord injury spastic the head and spasm eyelid vagina voc neck limb jaw and incobotulinumtoxina cord similar botulinum toxin used relax the monograph http www clench muscl include those the com monograph incob esophagus jaw lower urin tract and otulinumtoxina html clench the anus which can bladder onabotulinumtoxina anal fissure exacerbate botulinum toxin appear monograph http www refractory overact bladder effect for com monograph onab otulinumtoxina html prabotulinumtoxina other muscle disorder monograph http www strabismus otherw know improper eye com monograph prabo alignm caus imbalanc the action tulinumtoxina xvf html muscl that rotate the thi condition can rimabotulinumtoxinb sometim reliev weaken muscle that pull monograph http www too strong pull against one that have been com monograph rimab weaken disease trauma muscl weaken otulinumtoxinb html html toxin injection recover from paralysi after sever medlineplus a619021 http medlinepl month injection might seem nee repeat gov druginfo a619 but muscl adapt the length which they 021 html chronical held that paralyz muscle stretch antagonist grow longer while the license datum dailym botulinum antagonist shorten yield perman effect toxin http dailym nlm nih gov dailym search janu 2014 botulinum toxin approv labeltype all query medicin and healthcare product regulatory agency tulinum toxin for treatm restrict ankle motion due lower pregnancy limb spastic associat with stroke adult category rout intramuscular july 2016 the food and drug administr administr subcutane intraderm",
        @"http dailym nlm nih gov dailym search janu 2014 botulinum toxin approv labeltype all query medicin and healthcare product regulatory agency tulinum toxin for treatm restrict ankle motion due lower pregnancy limb spastic associat with stroke adult category rout intramuscular july 2016 the food and drug administr administr subcutane intraderm fda approv abobotulinumtoxina dysport for atc code injection for the treatm lower limb spastic botulinum toxin pediatr patient two year age and older m03ax01 who http abobotulinumtoxina the first and only fda approv www whocc atc ddd botulinum toxin for the treatm pediatr lower index code m03ax01 limb spastic the the the text fda approv leg status the label prescription medicin and for which leg status prescription medic condition the drug manufacturer may sell the only drug however prescriber may free prescribe them only schedule for any condition they wish also know off label pom prescription only",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 2/33",
        @"8/23/24, 8:59 PM Botulinum toxin - Wikipedia [55] use. Botulinum toxins have been used off-label for several pediatric conditions, including infantile [56] esotropia.",
        @"[4] US: WARNING Rx- [18][19][20][21][22][1] only EU: Rx-only",
        @"Identifiers CAS Number Botulinum toxin A: 93384- 43-1 (https://commonchem istry.cas.org/detail?cas_rn =93384-43-1) Botulinum toxin B: 93384- 43-2 (https://commonchem istry.cas.org/detail?cas_rn =93384-43-2) DrugBank Botulinum toxin A: DB00083 (https://www.d rugbank.ca/drugs/DB0008 3) Botulinum toxin B: DB00042 (https://www.d rugbank.ca/drugs/DB0004 2) ChemSpider Botulinum toxin A: none UNII Botulinum toxin A: E211KPY694 (https://pr ecision.fda.gov/uniisearch/ srs/unii/E211KPY694) Botulinum toxin B: 0Y70779M1F (https://pr ecision.fda.gov/uniisearch/ srs/unii/0Y70779M1F) KEGG Botulinum toxin A: D00783 (https://www.kegg.jp/entry/ D00783) Botulinum toxin B: D08957 (https://www.kegg.jp/entry/ D08957) ECHA InfoCard 100.088.372 (https://echa. europa.eu/substance-infor mation/-/substanceinfo/10 0.088.372) Chemical and physical data Formula C H N O S 6760 10447 1743 2010 32",
        @"Excessive sweating AbobotulinumtoxinA has been approved for the treatment of axillary hyperhidrosis, which cannot be [42][57] managed by topical agents.",
        @"Migraine In 2010, the FDA approved intramuscular botulinum toxin injections for prophylactic treatment of chronic [58] migraine headache . However, the use of botulinum toxin injections for episodic migraine has not been [59] approved by the FDA.",
        @"Cosmetic uses In cosmetic applications, botulinum toxin is considered [60] relatively safe and effective for reduction of facial wrinkles, especially in the uppermost third of the [61] face. Commercial forms are marketed under the brand names Botox Cosmetic/Vistabel from Allergan, Dysport/Azzalure from Galderma and Ipsen, Xeomin/Bocouture from Merz, Jeuveau/Nuceiva from [62] Evolus, manufactured by in South Korea. Daewoong The effects of botulinum toxin injections for glabellar lines ('11's lines' between the eyes) typically last two to four months and in some cases, product-dependent, with some patients experiencing a longer duration of [61] effect of up to six months or longer. Injection of botulinum toxin into the muscles under facial wrinkles causes relaxation of those muscles, resulting in the [61] smoothing of the overlying skin. Smoothing of wrinkles is usually visible three to five days after injection, with maximum effect typically a week [61] following injection. Muscles can be treated [61] repeatedly to maintain the smoothed appearance.",
        @"DaxibotulinumtoxinA (Daxxify) was approved for medical use in the United States in September [22][63] 2022. It is indicated for the temporary improvement in the appearance of moderate to severe glabellar lines (wrinkles between the",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 3/33",
        @"botulinum toxin wikipedia molar mass eyebrow daxibotulinumtoxina 149323 g·mol acetylcholine release inhibitor and neuromuscular what thi ver block agent the fda approv daxibotulinumtoxina bas evid from two clinic trial bontoxilysin study and 609 adult with moderate severe identifier glabellar lin the trial conduct sit the http www unit stat and canada both trial enroll participant zyme database org quer year old with moderate severe glabellar lin php participant receiv single intramuscular injection databas daxibotulinumtoxina placebo five sit within the muscl intenz intenz view http www between the eyebrow the many common side effect daxibotulinumtoxina headache droop eyelid and ebi intenz query searchec weak faci muscl letibotulinumtoxina letybo approv for medic use brenda brenda entry http the unit stat febru 2024 indicat brenda enzym org temporari improve the appear moderate severe nzyme php ecno glabellar lin the fda approv letibotulinumtoxina bas evid from three clinic trial bless expasy nicezyme view http nct02677298 bless nct02677805 and bless iii nzyme expasy org nct03985982 271 participant with moderate severe wrinkl between the eyebrow for efficacy and safe kegg kegg entry http assessm these trial conduct sit the genome dbget bin unit stat and the european union all three trial bget enzyme enroll participant year old with moderate severe glabellar lin wrinkl between the eyebrow participant metacyc metabol pathway http receiv single intramuscular injection letibotulinumtoxina biocyc org meta sub placebo five sit within the muscl between the tre search type nil eyebrow the many common side effect bject letibotulinumtoxina headache droop eyelid and brow and twitch eyelid priam profile http priam",
        @"wrinkl between the eyebrow participant metacyc metabol pathway http receiv single intramuscular injection letibotulinumtoxina biocyc org meta sub placebo five sit within the muscl between the tre search type nil eyebrow the many common side effect bject letibotulinumtoxina headache droop eyelid and brow and twitch eyelid priam profile http priam prabi cgi bin priam profil currentrelease other botulinum toxin also used treat disorder hyperact pdb rcsb pdb http www nerv include excess sweat neuropath pain structur rcsb org search rcsb allergy and some symptom addition these polymer ent rcsb botulinum toxin bee evaluat for use treat chron neage pain study show that botulinum toxin may inject into pdbe http www ebi arthrit shoulder joint reduce chron pain and improve pdbe entry search dex number pdbsum http www ebi thornton srv dat abas cgi bin enzym",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 4/33",
        @"botulinum toxin wikipedia getp number range motion the use botulinum toxin child with cerebr palsy safe the upper and lower limb muscl gene amigo http amigo gen ontol eontol org amigo ter 0006508 side effect quickgo http www quickgo term while botulinum toxin general consider safe clinic 0006508 sett seri side effect from use can occur many search common botulinum toxin can inject into the wrong pmc articl http www ncbi nlm muscle group with time spread from the injection site nih gov entrez query fcgi caus tempor paralysi unintend muscl ubm term 5be 20number 20an side effect from cosmet use general result from unintend 20pubm 20pmc 20loc paralysi faci muscl these include parti faci paralysi 5bsb muscle weak and trouble swallow side effect not pubm articl http www ncbi nlm limit direct paralysi however and can also include nih gov entrez query fcgi headach flu like symptom and allerg reaction just ubm term 5be cosmet treatment only last number month paralysi 20number side effect can have the same duration least some ncbi protein http www ncbi nlm cas these effect report dissipate the week after nih gov protein term 5bec 20number treatm bruis the site injection not side effect the toxin but rather the mode administr and report prevent the clinician apply pressure the injection site when occur report specific cas last 7–11 day when inject the masseter muscle the jaw loss muscle function can result loss reduction power chew solid food with continu high dos the muscl can atrophy lose strength research have show",
        @"administr and report prevent the clinician apply pressure the injection site when occur report specific cas last 7–11 day when inject the masseter muscle the jaw loss muscle function can result loss reduction power chew solid food with continu high dos the muscl can atrophy lose strength research have show that those muscl rebuild after break from botox",
        @"Side effects from therapeutic use can be much more varied depending on the location of injection and the dose of toxin injected. In general, side effects from therapeutic use can be more serious than those that arise during cosmetic use. These can arise from paralysis of critical muscle groups and can include arrhythmia, heart attack, and in some cases, seizures, respiratory [71] arrest, and death. Additionally, side effects common in cosmetic use are also common in therapeutic use, including trouble swallowing, muscle weakness, allergic reactions, and flu- [71] like syndromes.",
        @"Botulinum toxin being injected in the human face",
        @"In response to the occurrence of these side effects, in 2008, the FDA notified the public of the potential dangers of the botulinum toxin as a therapeutic. Namely, the toxin can spread to areas distant from the site of injection and paralyze unintended muscle groups, [76] especially when used for treating muscle spasticity in children treated for cerebral palsy. In 2009,",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 5/33",
        @"8/23/24, 8:59 PM Botulinum toxin - Wikipedia the FDA announced that boxed warnings would be added to available botulinum toxin products, [77][78][79][80] warning of their ability to spread from the injection site. However, the clinical use of botulinum toxin A in cerebral palsy children has been proven to be safe with minimal side [31][32] effects. Additionally, the FDA announced name changes to several botulinum toxin products, to emphasize that the products are not interchangeable and require different doses for proper use. Botox and Botox Cosmetic were given the generic name of onabotulinumtoxinA, Myobloc as [81][77] rimabotulinumtoxinB, and Dysport retained its generic name of abobotulinumtoxinA. In conjunction with this, the FDA issued a communication to health care professionals reiterating the [82] Health Canada new drug names and the approved uses for each. A similar warning was issued by [83] in 2009, warning that botulinum toxin products can spread to other parts of the body.",
        @"Role in disease",
        @"Botulinum toxin produced by Clostridium botulinum (an anaerobic, gram-positive bacterium) is the [25] cause of botulism. Humans most commonly ingest the toxin from eating improperly canned foods in which C. botulinum has grown. However, the toxin can also be introduced through an infected wound. In infants, the bacteria can sometimes grow in the intestines and produce botulinum toxin [84] within the intestine and can cause a condition known as . In all cases, the floppy baby syndrome toxin can then spread, blocking nerves and muscle function. In severe cases, the toxin can block [23] nerves controlling the respiratory system or heart, resulting in death.",
        @"Botulism can be difficult to diagnose, as it may appear similar to diseases such as Guillain–Barré syndrome, myasthenia gravis, and stroke. Other tests, such as brain scan and spinal fluid examination, may help to rule out other causes. If the symptoms of botulism are diagnosed early, various treatments can be administered. In an effort to remove contaminated food that remains in the [85] gut, enemas or induced vomiting may be used. For wound infections, infected material may be [85] removed surgically. Botulinum antitoxin is available and may be used to prevent the worsening of symptoms, though it will not reverse existing nerve damage. In severe cases, mechanical respiration [85] may be used to support people with respiratory failure. The nerve damage heals over time, [86] generally over weeks to months. With proper treatment, the case fatality rate for botulinum [85] poisoning can be greatly reduced.",
        @"Two preparations of botulinum antitoxins are available for treatment of botulism. Trivalent (serotypes A, B, E) botulinum antitoxin is derived from equine sources using whole antibodies. The second antitoxin is heptavalent botulinum antitoxin (serotypes A, B, C, D, E, F, G), which is derived from equine antibodies that have been altered to make them less immunogenic. This antitoxin is effective [87][30] against all main strains of botulism.",
        @"Mechanism of action",
        @"Botulinum toxin exerts its effect by cleaving key proteins required for nerve activation. First, the toxin binds specifically to presynaptic surface of neurons that use the neurotransmitter acetylcholine. Once bound to the nerve terminal, the neuron takes up the toxin into a vesicle by receptor-mediated [89] endocytosis. As the vesicle moves farther into the cell, it acidifies, activating a portion of the toxin",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 6/33",
        @"8/23/24, 8:59 PM Botulinum toxin - Wikipedia that triggers it to push across the vesicle membrane and into the cell [23] cytoplasm. Botulinum neurotoxins recognize distinct classes of receptors simultaneously (gangliosides, synaptotagmin and [90] ). Once inside the cytoplasm, SV2 the toxin cleaves SNARE proteins (proteins that mediate vesicle fusion, with their target membrane bound compartments) meaning that the acetylcholine vesicles cannot bind to [89] the intracellular cell membrane, preventing the cell from releasing vesicles of neurotransmitter. This stops nerve signaling, leading to [23][90] . flaccid paralysis Target molecules of botulinum neurotoxin (abbreviated BoNT) and The toxin itself is released from the [88] tetanus neurotoxin (TeNT), toxins acting inside the axon terminal bacterium as a single chain, then becomes activated when cleaved by [42] protein kDa its own proteases. The active form consists of a two-chain composed of a 100- heavy [91] polypeptide disulfide bond chain joined via to a 50-kDa light chain polypeptide. The heavy chain contains domains with several functions; it has the domain responsible for binding specifically to presynaptic nerve terminals, as well as the domain responsible for mediating translocation of the light [23][91] chain into the cell cytoplasm as the vacuole acidifies. The light chain is a M27-family zinc metalloprotease and is the active part of the toxin. It is translocated into the host cell cytoplasm where it cleaves the host protein SNAP-25, a member of the SNARE protein family, which is responsible for fusion. The cleaved SNAP-25 cannot mediate fusion of vesicles with the host cell membrane, thus [23] preventing the release of the acetylcholine from axon endings. This blockage is neurotransmitter slowly reversed as the toxin loses activity and the SNARE proteins are slowly regenerated by the [23] affected cell.",
        @"The seven toxin serotypes (A–G) are traditionally separated by their antigenicity. They have different [91][92] tertiary structures and sequence differences. While the different toxin types all target members [88] of the SNARE family, different toxin types target different SNARE family members. The A, B, and E serotypes cause human botulism, with the activities of types A and B enduring longest in vivo (from [91] several weeks to months). Existing toxin types can recombine to create ""hybrid"" (mosaic, chimeric) types. Examples include BoNT/CD, BoNT/DC, and BoNT/FA, with the first letter indicating the light [93] chain type and the latter indicating the heavy chain type. BoNT/FA received considerable attention under the name ""BoNT/H"", as it was mistakenly thought it could not be neutralized by any existing [30] antitoxin.",
        @"Botulinum toxins are AB toxins and closely related to Anthrax toxin, Diphtheria toxin, and in particular tetanus toxin. The two are collectively known as Clostridium neurotoxins and the light chain is classified by MEROPS as family M27 (https://www.ebi.ac.uk/merops/cgi-bin/famsum?famil",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 7/33",
        @"8/23/24, 8:59 PM Botulinum toxin - Wikipedia y=M27). Nonclassical types include BoNT/X (P0DPK1 (https://www.uniprot.org/uniprot/P0DPK1)), [29] which is toxic in mice and possibly in humans; a BoNT/J ( A0A242DI27 (https://www.uniprot.or [94] g/uniprot/A0A242DI27) Enterococcus A0A069CUU9 (https://w ) found in cow ; and a BoNT/Wo ( [93] ww.uniprot.org/uniprot/A0A069CUU9) Weissella oryzae ) found in the rice-colonizing .",
        @"History",
        @"Initial descriptions and discovery of Clostridium botulinum",
        @"One of the earliest recorded outbreaks of foodborne botulism occurred in 1793 in the village of Wildbad in what is now Baden-Württemberg, Germany. Thirteen people became sick and six died after eating pork stomach filled with blood sausage, a local delicacy. Additional cases of fatal food poisoning in Württemberg led the authorities to issue a public warning against consuming smoked [95] blood sausages in 1802 and to collect case reports of ""sausage poisoning"". Between 1817 and 1822, the German physician Justinus Kerner published the first complete description of the symptoms of botulism, based on extensive clinical observations and animal experiments. He concluded that the toxin develops in bad sausages under anaerobic conditions, is a biological substance, acts on the [95] nervous system, and is lethal even in small amounts. Kerner hypothesized that this ""sausage toxin"" could be used to treat a variety of diseases caused by an overactive nervous system, making him the [96] first to suggest that it could be used therapeutically. In 1870, the German physician Müller coined the term botulism to describe the disease caused by sausage poisoning, from the Latin word botulus, [96] meaning 'sausage'.",
        @"In 1895 Émile van Ermengem, a Belgian microbiologist, discovered what is now called Clostridium [97] botulinum and confirmed that a toxin produced by the bacteria causes botulism. On 14 December 1895, there was a large outbreak of botulism in the Belgian village of Ellezelles that occurred at a funeral where people ate pickled and smoked ham; three of them died. By examining the contaminated ham and performing autopsies on the people who died after eating it, van Ermengem [95] isolated an anaerobic microorganism that he called Bacillus botulinus. He also performed experiments on animals with ham extracts, isolated bacterial cultures, and toxins extracts from the bacteria. From these he concluded that the bacteria themselves do not cause foodborne botulism, but [98] rather produce a toxin that causes the disease after it is ingested. As a result of Kerner's and van Ermengem's research, it was thought that only contaminated meat or fish could cause botulism. This idea was refuted in 1904 when a botulism outbreak occurred in Darmstadt, Germany, because of canned white beans. In 1910, the German microbiologist J. Leuchs published a paper showing that the outbreaks in Ellezelles and Darmstadt were caused by different strains of Bacillus botulinus and that [95] the toxins were serologically distinct. In 1917, Bacillus botulinus was renamed Clostridium botulinum, as it was decided that term Bacillus should only refer to a group of aerobic microorganisms, while Clostridium would be only used to describe a group of anaerobic [97] Georgina Burke microorganisms. In 1919, used toxin-antitoxin reactions to identify two strains of [97] Clostridium botulinum, which she designated A and B.",
        @"Food canning",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 8/33",
        @"8/23/24, 8:59 PM Botulinum toxin - Wikipedia Over the next three decades, 1895–1925, as food canning was approaching a billion-dollar-a-year industry, botulism was becoming a public health hazard. Karl Friedrich Meyer, a Swiss-American veterinary scientist, created a center at the Hooper Foundation in San Francisco, where he developed techniques for growing the organism and extracting the toxin, and conversely, for preventing organism growth and toxin production, and inactivating the toxin by heating. The California canning [99] industry was thereby preserved.",
        @"World War II",
        @"With the outbreak of World War II, weaponization of botulinum toxin was investigated at Fort Detrick [100] in Maryland. Carl Lamanna and James Duff developed the concentration and crystallization techniques that Edward J. Schantz used to create the first clinical product. When the Army's Chemical Corps was disbanded, Schantz moved to the Food Research Institute in Wisconsin, where he manufactured toxin for experimental use and provided it to the academic community.",
        @"The mechanism of botulinum toxin action – blocking the release of the neurotransmitter acetylcholine [101] from nerve endings – was elucidated in the mid-20th century, and remains an important research topic. Nearly all toxin treatments are based on this effect in various body tissues.",
        @"Strabismus",
        @"Ophthalmologists specializing in eye muscle disorders (strabismus) had developed the method of EMG-guided injection (using the electromyogram, the electrical signal from an activated muscle, to guide injection) of local anesthetics as a diagnostic technique for evaluating an individual muscle's [102] strabismus surgery contribution to an eye movement. Because frequently needed repeating, a search was undertaken for non-surgical, injection treatments using various anesthetics, alcohols, enzymes, enzyme blockers, and snake neurotoxins. Finally, inspired by Daniel B. Drachman's work [103] with chicks at Johns Hopkins, and colleagues injected botulinum toxin into monkey Alan B. Scott [104] extraocular muscles. The result was remarkable; a few picograms induced paralysis that was confined to the target muscle, long in duration, and without side effects.",
        @"After working out techniques for freeze-drying, buffering with albumin, and assuring sterility, potency, and safety, Scott applied to the FDA for investigational drug use, and began manufacturing botulinum type A neurotoxin in his San Francisco lab. He injected the first strabismus patients in [105] 1977, reported its clinical utility in 1980, and had soon trained hundreds of ophthalmologists in EMG-guided injection of the drug he named Oculinum (""eye aligner"").",
        @"In 1986, Oculinum Inc, Scott's micromanufacturer and distributor of botulinum toxin, was unable to obtain product liability insurance, and could no longer supply the drug. As supplies became exhausted, people who had come to rely on periodic injections became desperate. For four months, as liability issues were resolved, American blepharospasm patients traveled to Canadian eye centers for [106] their injections.",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 9/33",
        @"8/23/24, 8:59 PM Botulinum toxin - Wikipedia Based on data from thousands of people collected by 240 investigators, Oculinum Inc (which was soon acquired by Allergan) received FDA approval in 1989 to market Oculinum for clinical use in the United States to treat adult strabismus and blepharospasm. Allergan then began using the trademark [107] [108] 1983 US Orphan Drug Act Botox. This original approval was granted under the .",
        @"Cosmetics The effect of botulinum toxin type-A on reducing and eliminating forehead wrinkles was first described and published by Richard Clark, MD, a plastic surgeon from Sacramento, California. In 1987 Clark was challenged with eliminating the disfigurement caused by only the right side of the forehead muscles functioning after the left side of the forehead was paralyzed during a facelift procedure. This patient had desired to look better from her facelift, but was experiencing bizarre unilateral right forehead eyebrow elevation Doctor performing Botulinum toxin while the left eyebrow drooped, and she constantly demonstrated injection deep expressive right forehead wrinkles while the left side was perfectly smooth due to the paralysis. Clark was aware that Botulinum toxin was safely being used to treat babies with strabismus and he requested and was granted FDA approval to experiment with Botulinum toxin to paralyze the moving and wrinkling normal functioning right forehead muscles to make both sides of the forehead appear the same. This study and case report of the cosmetic use of Botulinum toxin to treat a cosmetic complication of a cosmetic surgery was the first report on the specific treatment of wrinkles and was published in the [109] journal Plastic and Reconstructive Surgery in 1989. Editors of the journal of the American Society of Plastic Surgeons have clearly stated ""the first described use of the toxin in aesthetic [110] circumstances was by Clark and Berris in 1989.""",
        @"Also in 1987, Jean and Alastair Carruthers, both doctors in Vancouver, British Columbia, observed that blepharospasm patients who received injections around the eyes and upper face also enjoyed diminished facial glabellar lines (""frown lines"" between the eyebrows). Alastair Carruthers reported that others at the time also noticed these effects and discussed the cosmetic potential of botulinum [111] toxin. Unlike other investigators, the Carruthers did more than just talk about the possibility of using botulinum toxin cosmetically. They conducted a clinical study on otherwise normal individuals whose only concern was their eyebrow furrow. They performed their study between 1987 and 1989 and presented their results at the 1990 annual meeting of the American Society for Dermatologic [112] Surgery. Their findings were subsequently published in 1992.",
        @"Chronic pain",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 10/33",
        @"8/23/24, 8:59 PM Botulinum toxin - Wikipedia William J. Binder reported in 2000 that people who had cosmetic injections around the face reported [113] relief from chronic headache. This was initially thought to be an indirect effect of reduced muscle tension, but the toxin is now known to inhibit release of peripheral nociceptive neurotransmitters, [114][115] suppressing the central pain processing systems responsible for headache. migraine",
        @"Society and culture",
        @"Economics",
        @"As of 2018, botulinum toxin injections are the most common cosmetic operation, with 7.4 million [116] American Society of Plastic Surgeons procedures in the United States, according to the .",
        @"The global market for botulinum toxin products, driven by their cosmetic applications, was forecast to reach $2.9 billion by 2018. The facial aesthetics market, of which they are a component, was forecast [117] to reach $4.7 billion ($2 billion in the US) in the same timeframe.",
        @"US market [118] In 2020, 4,401,536 botulinum toxin Type A procedures were administered. In 2019 the botulinum [119] toxin market made US$3.19 billion.",
        @"Botox cost Botox cost is generally determined by the number of units administered (avg. $10–30 per unit) or by the area ($200–1000) and depends on expertise of a physician, clinic location, number of units, and [120] treatment complexity.",
        @"Insurance In the US, botox for medical purposes is usually covered by insurance if deemed medically necessary by a doctor and covers a plethora of medical problems including overactive bladder (OAB), urinary incontinence due to neurologic conditions, headaches and migraines, TMJ, spasticity in adults, cervical dystonia in adults, severe axillary hyperhidrosis (or other areas of the body), blepharospasm, [121][122] upper or lower limb spasticity.",
        @"Hyperhidrosis [70] Botox for excessive sweating is FDA approved.",
        @"Cosmetic",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 11/33",
        @"8/23/24, 8:59 PM Botulinum toxin - Wikipedia Standard areas for aesthetics botox injections include facial and other areas that can form fine lines and wrinkles due to every day muscle contractions and/or facial expressions such as smiling, frowning, squinting, and raising eyebrows. These areas include the glabellar region between the eyebrows, horizontal lines on the forehead, crow's feet around the eyes, and even circular bands that [123] form around the neck secondary to platysmal hyperactivity.",
        @"Bioterrorism [124] bioterrorism Botulinum toxin has been recognized as a potential agent for use in . It can be [125] absorbed through the eyes, mucous membranes, respiratory tract, and non-intact skin. The effects of botulinum toxin are different from those of nerve agents involved insofar in that botulism symptoms develop relatively slowly (over several days), while nerve agent effects are generally much more rapid. Evidence suggests that nerve exposure (simulated by injection of atropine and [126] pralidoxime) will increase mortality by enhancing botulinum toxin's mechanism of toxicity. With regard to detection, protocols using NBC detection equipment (such as M-8 paper or the ICAM) will [127] not indicate a ""positive"" when samples containing botulinum toxin are tested. To confirm a diagnosis of botulinum toxin poisoning, therapeutically or to provide evidence in death investigations, botulinum toxin may be quantitated by immunoassay of human biological fluids; serum levels of 12– [128] 24 mouse LD units per milliliter have been detected in poisoned people. 50",
        @"During the early 1980s, German and French newspapers reported that the police had raided a Baader- Meinhof gang safe house in Paris and had found a makeshift laboratory that contained flasks full of Clostridium botulinum, which makes botulinum toxin. Their reports were later found to be incorrect; [129] no such lab was ever found.",
        @"Brand names [18][81][130] Commercial forms are marketed under the brand names Botox (onabotulinumtoxinA), [81][131] [1][2][132] Dysport/Azzalure (abobotulinumtoxinA), Letybo (letibotulinumtoxinA), Myobloc [20][81] [133] (rimabotulinumtoxinB), Xeomin/Bocouture (incobotulinumtoxinA), and Jeuveau [134][62] (prabotulinumtoxinA).",
        @"Botulinum toxin A is sold under the brand names Jeuveau, Botox, and Xeomin. Botulinum toxin B is [20] sold under the brand name Myobloc.",
        @"In the United States, botulinum toxin products are manufactured by a variety of companies, for both therapeutic and cosmetic use. A US supplier reported in its company materials in 2011 that it could ""supply the world's requirements for 25 indications approved by Government agencies around the [135] world"" with less than one gram of raw botulinum toxin. Myobloc or Neurobloc, a botulinum toxin type B product, is produced by Solstice Neurosciences, a subsidiary of US WorldMeds. AbobotulinumtoxinA), a therapeutic formulation of the type A toxin manufactured by Galderma in the United Kingdom, is licensed for the treatment of focal dystonias and certain cosmetic uses in the US [82] and other countries. LetibotulinumtoxinA (Letybo) was approved for medical use in the United [1] States in February 2024.",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 12/33",
        @"8/23/24, 8:59 PM Botulinum toxin - Wikipedia Besides the three primary US manufacturers, numerous other botulinum toxin producers are known. Xeomin, manufactured in Germany by Merz, is also available for both therapeutic and cosmetic use in [136] the US. Lanzhou Institute of Biological Products in China manufactures a botulinum toxin type-A [136] product; as of 2014, it was the only botulinum toxin type-A approved in China. Botulinum toxin [137] type-A is also sold as Lantox and Prosigne on the global market. Neuronox, a botulinum toxin [138] type-A product, was introduced by Medy-Tox of South Korea in 2009.",
        @"Toxin production",
        @"Botulism toxins are produced by bacteria of the genus Clostridium, namely C. botulinum, [139] , and , which are widely distributed, including in soil and C. butyricum C. baratii C. argentinense dust. Also, the bacteria can be found inside homes on floors, carpet, and countertops even after [140] cleaning. Complicating the problem is that the taxonomy for C. botulinum remains chaotic. The toxin has likely been horizontally transferred across lineages, contributing to the multi-species patten [141][142] seen today.",
        @"Food-borne botulism results, indirectly, from ingestion of food contaminated with Clostridium spores, where exposure to an anaerobic environment allows the spores to germinate, after which the [140] bacteria can multiply and produce toxin. Critically, ingestion of toxin rather than spores or [140] vegetative bacteria causes . Botulism is nevertheless known to be transmitted through botulism [140] canned foods not cooked correctly before canning or after can opening, so is preventable. Infant botulism arising from consumption of honey or any other food that can carry these spores can be [143] prevented by eliminating these foods from diets of children less than 12 months old.",
        @"Organism and toxin susceptibilities [144] Proper refrigeration at temperatures below 4.4 °C (39.9 °F) slows the growth of C. botulinum. The [34] organism is also susceptible to high salt, high oxygen, and low pH levels. The toxin itself is rapidly [145] destroyed by heat, such as in thorough cooking. The spores that produce the toxin are heat- [146] tolerant and will survive boiling water for an extended period of time.",
        @"The botulinum toxin is denatured and thus deactivated at temperatures greater than 85 °C (185 °F) [34] metalloprotease for five minutes. As a zinc (see below), the toxin's activity is also susceptible, post- [91][147] inhibition protease inhibitors hydroxamates exposure, to by , e.g., zinc-coordinating .",
        @"Research",
        @"Blepharospasm and strabismus",
        @"University-based ophthalmologists in the US and Canada further refined the use of botulinum toxin as a therapeutic agent. By 1985, a scientific protocol of injection sites and dosage had been empirically [148] blepharospasm determined for treatment of and strabismus. Side effects in treatment of this",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 13/33",
        @"8/23/24, 8:59 PM Botulinum toxin - Wikipedia [149] condition were deemed to be rare, mild and treatable. The beneficial effects of the injection lasted only four to six months. Thus, blepharospasm patients required re-injection two or three times a [150] year.",
        @"In 1986, Scott's micromanufacturer and distributor of Botox was no longer able to supply the drug because of an inability to obtain product liability insurance. People became desperate, as supplies of Botox were gradually consumed, forcing him to abandon people who would have been due for their next injection. For a period of four months, American blepharospasm patients had to arrange to have their injections performed by participating doctors at Canadian eye centers until the liability issues [106] could be resolved.",
        @"In December 1989, Botox was approved by the US FDA for the treatment of strabismus, [107] blepharospasm, and in people over 12 years old. hemifacial spasm",
        @"In the case of treatment of infantile esotropia in people younger than 12 years of age, several studies [56][151] have yielded differing results.",
        @"Cosmetic",
        @"The effect of botulinum toxin type-A on reducing and eliminating forehead wrinkles was first described and published by Richard Clark, MD, a plastic surgeon from Sacramento, California. In 1987 Clark was challenged with eliminating the disfigurement caused by only the right side of the forehead muscles functioning after the left side of the forehead was paralyzed during a facelift procedure. This patient had desired to look better from her facelift, but was experiencing bizarre unilateral right forehead eyebrow elevation while the left eyebrow drooped and she emoted with deep expressive right forehead wrinkles while the left side was perfectly smooth due to the paralysis. Clark was aware that botulinum toxin was safely being used to treat babies with strabismus and he requested and was granted FDA approval to experiment with botulinum toxin to paralyze the moving and wrinkling normal functioning right forehead muscles to make both sides of the forehead appear the same. This study and case report on the cosmetic use of botulinum toxin to treat a cosmetic complication of a cosmetic surgery was the first report on the specific treatment of wrinkles and was [109] published in the journal Plastic and Reconstructive Surgery in 1989. Editors of the journal of the American Society of Plastic Surgeons have clearly stated ""the first described use of the toxin in [110] aesthetic circumstances was by Clark and Berris in 1989.""",
        @"J. D. and J. A. Carruthers also studied and reported in 1992 the use of botulinum toxin type-A as a cosmetic treatment.[78] They conducted a study of participants whose only concern was their glabellar forehead wrinkle or furrow. Study participants were otherwise normal. Sixteen of seventeen participants available for follow-up demonstrated a cosmetic improvement. This study was reported [112] at a meeting in 1991. The study for the treatment of frown lines was published in 1992. glabellar This result was subsequently confirmed by other groups (Brin, and the Columbia University group [152] under Monte Keen ). The FDA announced regulatory approval of botulinum toxin type A (Botox Cosmetic) to temporarily improve the appearance of moderate-to-severe frown lines between the [153] eyebrows (glabellar lines) in 2002 after extensive clinical trials. Well before this, the cosmetic use [154] of botulinum toxin type A became widespread. The results of Botox Cosmetic can last up to four",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 14/33",
        @"8/23/24, 8:59 PM Botulinum toxin - Wikipedia [155] Food and Drug Administration months and may vary with each patient. The US (FDA) approved an alternative product-safety testing method in response to increasing public concern that LD50 [156][157] testing was required for each batch sold in the market.",
        @"[158] gummy Botulinum toxin type-A has also been used in the treatment of smiles; the material is injected into the hyperactive muscles of upper lip, which causes a reduction in the upward movement [159] gingiva of lip thus resulting in a smile with a less exposure of . Botox is usually injected in the three lip elevator muscles that converge on the lateral side of the ala of the nose; the levator labii superioris (LLS), the levator labii superioris alaeque nasi muscle (LLSAN), and the zygomaticus minor [160][161] (ZMi).",
        @"Upper motor neuron syndrome Botulinum toxin type-A is now a common treatment for muscles affected by the upper motor neuron [31] cerebral palsy syndrome (UMNS), such as , for muscles with an impaired ability to effectively lengthen. Muscles affected by UMNS frequently are limited by weakness, loss of reciprocal inhibition, decreased movement control, and hypertonicity (including spasticity). In January 2014, Botulinum toxin was approved by UK's Medicines and Healthcare products Regulatory Agency (MHRA) for the [50] treatment of ankle disability due to lower limb spasticity associated with stroke in adults. Joint motion may be restricted by severe muscle imbalance related to the syndrome, when some muscles are markedly hypertonic, and lack effective active lengthening. Injecting an overactive muscle to decrease its level of contraction can allow improved reciprocal motion, so improved ability to move [31] and exercise.",
        @"Sialorrhea Sialorrhea is a condition where oral secretions are unable to be eliminated, causing pooling of saliva in the mouth. This condition can be caused by various neurological syndromes such as Bell's palsy, intellectual disability, and cerebral palsy. Injection of botulinum toxin type-A into salivary glands is [162] useful in reducing the secretions.",
        @"Cervical dystonia Botulinum toxin type-A is used to treat cervical dystonia, but it can become ineffective after a time. Botulinum toxin type B received FDA approval for treatment of cervical dystonia in December 2000. Brand names for botulinum toxin type-B include Myobloc in the United States and Neurobloc in the [136] European Union.",
        @"Chronic migraine",
        @"Onabotulinumtoxin A (trade name Botox) received FDA approval for treatment of chronic migraines on 15 October 2010. The toxin is injected into the head and neck to treat these chronic headaches. Approval followed evidence presented to the agency from two studies funded by Allergan showing a very slight improvement in incidence of chronic migraines for those with migraines undergoing the [163][164] Botox treatment.",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 15/33",
        @"8/23/24, 8:59 PM Botulinum toxin - Wikipedia Since then, several randomized control trials have shown botulinum toxin type A to improve headache [165] symptoms and quality of life when used prophylactically for participants with chronic migraine who exhibit headache characteristics consistent with: pressure perceived from outside source, shorter total duration of chronic migraines (<30 years), ""detoxification"" of participants with coexisting chronic daily headache due to medication overuse, and no current history of other preventive [166] headache medications.",
        @"Depression [167][168] A few small trials have found benefits in people with . Research is based on the depression [169] facial feedback hypothesis.",
        @"Premature ejaculation The drug for the treatment of premature ejaculation has been under development since August 2013, [168][170] Phase II and is in trials.",
        @"References",
        @"letybo letibotulinumtoxina wlbg for injection for intramuscular use http www accessdata gov drugsatfda doc label 2024 761225s000lbl pdf pdf archive http web archive org 20240302034713 http www accessdata fda gov drugsatfda doc label 2024 761225s000lbl pdf from the origin march 2024 retriev march 2024 letybo therapeut good administr tga http www tga gov resourc auspmd let ybo archive http web archive org web 20221218020505 http www tga gov resourc uspmd letybo from the origin december 2022 retriev december 2022 nuceiva http www tga gov resourc auspmd nuceiva therapeut good administr tga febru 2023 retriev april 2023 fda sourc list all drug with black box warne use download full result and view query link http nctr fda gov fdalabel spl summary criterion 343802 nctr fda gov fda retriev october 2023 nuceiva ppd australia pty ltd http www tga gov resourc prescription medicin regist ration nuceiva ppd australia pty ltd therapeut good administr tga febru 2023 archive http web archive org web 20230318023528 http www tga gov resourc rescription medicin registration nuceiva ppd australia pty ltd from the origin march 2023 retriev april 2023 nuceiva prabotulinumtoxina 100 unit powder for solution for injection vial 381094 http tga gov resourc artg 381094 therapeut good administr tga janu 2023 archive http web archive org web 20230408041216 http www tga gov resourc artg 1094 from the origin april 2023 retriev april 2023 prescription medicin registr new chemic entity australia 2014 http www tga resourc resource guid prescription medicin registr new chemic entity aust ralia 2014 therapeut good administr tga june 2022",
        @"http web archive org web 20230408041216 http www tga gov resourc artg 1094 from the origin april 2023 retriev april 2023 prescription medicin registr new chemic entity australia 2014 http www tga resourc resource guid prescription medicin registr new chemic entity aust ralia 2014 therapeut good administr tga june 2022 archive http web archiv org web 20230410065838 http www tga gov resourc resource guid prescription dicin registr new chemic entity australia 2014 from the origin april 2023 retriev april 2023",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 16/33",
        @"botulinum toxin wikipedia auspar letybo therapeut good administr tga http www tga gov resourc uspar auspar letybo archive http web archive org web 20240331043040 http www tga resourc auspar auspar letybo from the origin march 2024 retriev march 2024 regulatory decision summ botox http hpr reg cont regulatory decision mmary detail php lang linkid rds00792 health canada october 2014 archive http web archive org web 20220612064147 http hpr reg cont regulatory decision summ detail php lang linkid rds00792 from the origin june 2022 retriev june 2022 regulatory decision summ nuceiva http hpr reg cont regulatory decision summ detail php lang linkid rds00405 health canada october 2014 archive htt web archive org web 20220607080128 http hpr reg cont regulatory decision summ detail php lang linkid rds00405 from the origin june 2022 retriev june 2022 regulatory decision summ for xeomin http dhpp hpfb dgpsa review document resour rds1709049767533 drug and health product port march 2022 retriev april 2024 regulatory decision summ for botox http dhpp hpfb dgpsa review document resourc rds1708464933703 drug and health product port febru 2024 archive http archive org web 20240402033454 http dhpp hpfb dgpsa review document resource 1708464933703 from the origin april 2024 retriev april 2024 health canada new drug authorization 2016 highlight http www canada health can ada servic publication drug health product health canada new drug authorization 2016 hig hlight html health canada march 2017 archive http web archive org web 20240407045 431 http www canada health canada servic publication drug health product health anada new drug authorization 2016 highlight html from",
        @"www canada health can ada servic publication drug health product health canada new drug authorization 2016 hig hlight html health canada march 2017 archive http web archive org web 20240407045 431 http www canada health canada servic publication drug health product health anada new drug authorization 2016 highlight html from the origin april 2024 retriev april 2024 azzalure summ product characteristic smpc http www medicin org emc prod uct 6584 smpc emc august 2022 archive http web archive org web 20221218024754 http www medicin org emc product 6584 smpc from the origin december 2022 retriev december 2022 alluzi 200 speywood unit solution for injection summ product characteristic smpc http www medicin org emc product 13798 smpc emc october 2022 archive http web archive org web 20221218024756 http www medicin org emc produ 13798 smpc from the origin december 2022 retriev december 2022 letybo unit powder for solution for injection summ product characteristic smpc http www medicin org emc product 13707 smpc emc may 2022 archive http archive org web 20221218024749 http www medicin org emc product 13707 smpc from the origin december 2022 retriev december 2022 xeomin unit powder for solution for injection summ product characteristic smpc http www medicin org emc product 4609 smpc emc july 2022 archive http archive org web 20221218024747 http www medicin org emc product 4609 smpc from the origin december 2022 retriev december 2022 botox onabotulinumtoxina injection powder lyophiliz for solution http dailym nlm nih dailym druginfo cfm setid 33d066a9 34ff 4a1a b38b d10983df3300 dailym july 2021 archive http web",
        @"july 2022 archive http archive org web 20221218024747 http www medicin org emc product 4609 smpc from the origin december 2022 retriev december 2022 botox onabotulinumtoxina injection powder lyophiliz for solution http dailym nlm nih dailym druginfo cfm setid 33d066a9 34ff 4a1a b38b d10983df3300 dailym july 2021 archive http web archive org web 20220602233512 http dailym nlm nih gov dailym druginfo cfm setid 33d066a9 34ff 4a1a b38b d10983df3300 from the origin june 2022 retriev june 2022",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 17/33",
        @"botulinum toxin wikipedia botox cosmet onabotulinumtoxina injection powder lyophiliz for solution http dailym nlm nih gov dailym lookup cfm setid 485d9b71 6881 42c5 a620 a4360c7192ab dailym febru 2021 archive http web archive org web 20221218022735 http dailym nlm nih dailym lookup cfm setid 485d9b71 6881 42c5 a620 a4360c7192ab from the origin december 2022 retriev december 2022 myobloc rimabotulinumtoxinb injection solution http dailym nlm nih gov dailym druginfo cfm setid 675cb354 9d13 482e 8ac2 22f709c58b4f dailym march 2021 archive http web archive org web 20220602233512 http dailym nlm nih gov dailym druginfo cfm set 675cb354 9d13 482e 8ac2 22f709c58b4f from the origin june 2022 retriev june 2022 dysport botulinum toxin type injection powder lyophiliz for solution http dailym nlm gov dailym druginfo cfm setid 71313a04 1349 4c26 b840 a39e4a3dda dailym febru 2022 archive http web archive org web 20220602233513 http dailym nlm nih dailym druginfo cfm setid 71313a04 1349 4c26 b840 a39e4a3dda from the origin june 2022 retriev june 2022 daxx botulinum toxin type injection powder lyophiliz for solution http dailym nlm gov dailym druginfo cfm setid 3aaa6e14 a3f7 4fb2 b9f9 d3a9c3ae1f74 dailym september 2022 archive http web archive org web 20220928041736 http dailym nlm gov dailym druginfo cfm setid 3aaa6e14 a3f7 4fb2 b9f9 d3a9c3ae1f74 from the origin september 2022 retriev september 2022 montecucco molgó june 2005 botulin neurotoxin reviv old killer curr opinion pharmacol 274–279 doi 1016 coph 2004 006 http doi org 101 2fj coph 2004 006 pmid 15907915 http pubm ncbi nlm nih gov 15907915 figgitt noble 2002 botulinum toxin review therapeut potenti the",
        @"origin september 2022 retriev september 2022 montecucco molgó june 2005 botulin neurotoxin reviv old killer curr opinion pharmacol 274–279 doi 1016 coph 2004 006 http doi org 101 2fj coph 2004 006 pmid 15907915 http pubm ncbi nlm nih gov 15907915 figgitt noble 2002 botulinum toxin review therapeut potenti the manage cervic dystonia drug 705–722 doi 2165 00003495 200262040 00011 http doi org 2165 2f00003495 200262040 00011 pmid 11893235 http pubme ncbi nlm nih gov 11893235 s2cid 46981635 http api semanticscholar org corpusid 46981 635 shukla sharma 2005 clostridium botulinum bug with beau and weapon critic review microbiol 11–18 doi 1080 10408410590912952 http doi org 108 2f10408410590912952 pmid 15839401 http pubm ncbi nlm nih gov 15839401 s2cid 2855356 http api semanticscholar org corpusid 2855356 jan connor moradi alghoul april 2021 curr use cosmet toxin improve faci aesthetic plast and reconstruct surgery 147 644e–657e doi 1097 0000000000007762 http doi org 1097 2fpr 0000000000007762 pmid 33776040 http pubm ncbi nlm nih gov 33776040 s2cid 232408799 http api sem anticscholar org corpusid 232408799 ghamdi alghanemy joharji qahtani alghamdi janu 2015 botulinum toxin non cosmet and off label dermatologic http doi org 1016 2fj jdd 2014 002 journ dermatol dermatolog surgery 1–8 doi 1016 jdd 2014 002 http doi org 1016 2fj jdd 2014 002 rosal bigalke dressler febru 2006 pharmacol botulinum toxin differenc between type preparation european journ neurol suppl 2–10 doi 1111 1468 1331 2006 01438 http doi org 1111 2fj 1468 1331 2006 01438 pmid 16417591 http pubm ncbi nlm nih gov 16417591 s2cid 32387953 http api",
        @"1016 2fj jdd 2014 002 rosal bigalke dressler febru 2006 pharmacol botulinum toxin differenc between type preparation european journ neurol suppl 2–10 doi 1111 1468 1331 2006 01438 http doi org 1111 2fj 1468 1331 2006 01438 pmid 16417591 http pubm ncbi nlm nih gov 16417591 s2cid 32387953 http api sema nticscholar org corpusid 32387953 botulism toxin time update the textbook thank genom sequenc http answer hildrenshospit org botulinum toxin discover boston child hospit august 2017 archive http web archive org web 20210914040619 http answer childrenshospit org botu linum toxin discover from the origin september 2021 retriev october 2019",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 18/33",
        @"botulinum toxin wikipedia study novel botulinum toxin less danger than think http www cidrap umn edu new rspect 2015 study novel botulinum toxin less danger think cidrap univers minnesota june 2015 archive http web archive org web 20191028181916 http www cidr umn edu new perspect 2015 study novel botulinum toxin less danger think from the origin october 2019 retriev october 2019 farag mohamm sobky elkadery elzohiery march 2020 botulinum toxin injection treatm upper limb spastic child with cerebr palsy systemat review randomiz controll trial http doi org 2106 2fjbj rvw 0119 jbj review e0119 doi 2106 jbj rvw 00119 http doi org 2106 jbj rvw 00119 pmc 7161716 http www ncbi nlm nih gov pmc articl pmc7161716 pmid 32224633 http pubm ncbi nlm nih gov 32224633 blumetti belloti tamaoki pinto october 2019 botulinum toxin type the treatm lower limb spastic child with cerebr palsy http www ncbi nlm nih gov articl pmc6779591 the cochrane database systemat review 2019 cd001408 doi 1002 14651858 cd001408 pub2 http doi org 1002 2f14651858 cd001408 pub2 pmc 6779591 http www ncbi nlm nih gov pmc articl pmc6779591 pmid 31591703 http pubm ncbi nlm nih gov 31591703 american socie health system pharmacist october 2011 onabotulinumtoxina botulinum toxin type monograph for professional http www drug com monograph onab otulinumtoxina html drug com archive http web archive org web 20150906194001 http drug com monograph onabotulinumtoxina html from the origin september 2015 retriev march 2015 fact sheet botulism http www who int new room fact sheet detail botulism world health organiz janu 2018 archive http",
        @"www drug com monograph onab otulinumtoxina html drug com archive http web archive org web 20150906194001 http drug com monograph onabotulinumtoxina html from the origin september 2015 retriev march 2015 fact sheet botulism http www who int new room fact sheet detail botulism world health organiz janu 2018 archive http web archive org web 20190323162924 www who int new room fact sheet detail botulism from the origin march 2019 retriev march 2019 košenina masuyer zhang dong stenmark june 2019 cryst structure the catalyt domain the weissella oryzae botulinum like toxin http doi org 1002 2f1873 13446 feb letter 593 1403–1410 doi 1002 1873 3468 13446 http doi org 1002 2f1873 3468 13446 pmid 31111466 http pubm ncbi nlm nih gov 31111466 dhak singh singh gupta november 2010 botulinum toxin bioweapon mag drug http www ncbi nlm nih gov pmc articl pmc3028942 indian journ medic research 132 489–503 pmc 3028942 http www ncbi nlm nih gov pmc articl pmc3028 942 pmid 21149997 http pubm ncbi nlm nih gov 21149997 arnon schechter inglesby henderson bartlett ascher febru 2001 botulinum toxin biologic weapon medic and public health manage jama 285 1059–1070 doi 1001 jama 285 1059 http doi org 1001 2fjama 285 1059 pmid 11209178 http pubm ncbi nlm nih gov 11209178 ozcakir sivrioglu june 2007 botulinum toxin poststroke spastic http www ncbi nih gov pmc articl pmc1905930 clinic medicine research 132–138 doi 3121 cmr 2007 716 http doi org 3121 2fcmr 2007 716 pmc 1905930 http ncbi nlm nih gov pmc articl pmc1905930 pmid 17607049 http pubm ncbi",
        @"nih gov 11209178 ozcakir sivrioglu june 2007 botulinum toxin poststroke spastic http www ncbi nih gov pmc articl pmc1905930 clinic medicine research 132–138 doi 3121 cmr 2007 716 http doi org 3121 2fcmr 2007 716 pmc 1905930 http ncbi nlm nih gov pmc articl pmc1905930 pmid 17607049 http pubm ncbi nlm nih gov 17607049 yan lan liu miao november 2018 efficacy and safe botulinum toxin type spastic caus spin cord injury randomiz controll tri http www ncbi nlm gov pmc articl pmc6243868 medic sci monitor 8160–8171 doi 12659 msm 911296 http doi org 12659 2fmsm 911296 pmc 6243868 http ncbi nlm nih gov pmc articl pmc6243868 pmid 30423587 http pubm ncbi nlm nih gov 30423587",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 19/33",
        @"botulinum toxin wikipedia cervic dystonia symptom and caus http www mayoclin org diseas condition cervi cal dystonia symptom caus syc 20354123 mayo clin janu 2014 archive http archive org web 20181212142423 http www mayoclin org diseas condition cervic tonia symptom caus syc 20354123 from the origin december 2018 retriev october 2015 pacik december 2009 botox treatm for vaginismus http doi org 1097 2fpr 013e3181bf7f11 plast and reconstruct surgery 124 455e–456e doi 1097 0b013e3181bf7f11 http doi org 1097 2fpr 0b013e3181bf7f11 pmid 19952618 http pubm ncbi nlm nih gov 19952618 felber october 2006 botulinum toxin prim care medicine the journ the american osteopath associ 106 609–614 pmid 17122031 http pubm ncbi nlm nih gov 17122031 s2cid 245177279 http api semanticscholar org corpusid 245177279 stavropoulo friedel modayil iqb grendell march 2013 endoscop approach treatm achalasia http www ncbi nlm nih gov pmc articl pmc3589133 therapeut advanc gastroenterol 115–135 doi 1177 1756283x12468039 http doi org 1177 2f1756283x12468039 pmc 3589133 http www ncbi nlm nih gov pmc art icl pmc3589133 pmid 23503707 http pubm ncbi nlm nih gov 23503707 long liao wang liao lai febru 2012 efficacy botulinum toxin bruxism evid bas review http doi org 1111 2fj 1875 595x 2011 00085 internation dent journ 1–5 doi 1111 1875 595x 2011 00085 http doi org 1111 2fj 595x 2011 00085 pmc 9374973 http www ncbi nlm nih gov pmc articl pmc9374973 pmid 22251031 http pubm ncbi nlm nih gov 22251031 mangera andersson apostolidi chapple dasgupta giannantoni october 2011 contemporary manage lower urin tract disease with botulinum toxin systemat review botox onabotulinumtoxina",
        @"00085 http doi org 1111 2fj 595x 2011 00085 pmc 9374973 http www ncbi nlm nih gov pmc articl pmc9374973 pmid 22251031 http pubm ncbi nlm nih gov 22251031 mangera andersson apostolidi chapple dasgupta giannantoni october 2011 contemporary manage lower urin tract disease with botulinum toxin systemat review botox onabotulinumtoxina and dysport abobotulinumtoxina european urol 784–795 doi 1016 eururo 2011 001 http doi org 1016 2fj eururo 011 001 pmid 21782318 http pubm ncbi nlm nih gov 21782318 villalba villalba abba 2007 anal fissure common cause anal pain http www ncbi nlm nih gov pmc articl pmc3048443 the permanente journ 62–65 doi 7812 tpp 072 http doi org 7812 2ftpp 2f07 072 pmc 3048443 http www cbi nlm nih gov pmc articl pmc3048443 pmid 21412485 http pubm ncbi nlm nih gov 412485 duthie vinc herbison wilson wilson december 2011 duthie botulinum toxin injection for adult with overact bladder syndrome the cochrane database systemat review cd005493 doi 1002 14651858 cd005493 pub3 http doi org 1002 2f14651858 cd005493 pub3 pmid 22161392 http pubm ncbi nlm nih gov 22161 392 scott 1994 change eye muscle sarcomer accord eye position journ pediatr ophthalmol and strabismus 85–88 doi 3928 0191 3913 19940301 htt doi org 3928 2f0191 3913 19940301 pmid 8014792 http pubm ncbi nlm nih 8014792 simpson december 2012 botulinum neurotoxin and tetanus toxin http book google book fiwbx6obdcmc strabismus 20paralyz 20muscle 20i 20stretch 20th 20antagonist 20shorten 20perman pa400 elsevier isbn 978 323 14160 archive http web archive org web 20210828010046 http book google com book fiwb x6obdcmc strabismus paralyz",
        @"http pubm ncbi nlm nih 8014792 simpson december 2012 botulinum neurotoxin and tetanus toxin http book google book fiwbx6obdcmc strabismus 20paralyz 20muscle 20i 20stretch 20th 20antagonist 20shorten 20perman pa400 elsevier isbn 978 323 14160 archive http web archive org web 20210828010046 http book google com book fiwb x6obdcmc strabismus paralyz muscle stretch the antagonist shorten permanen pa400 from the origin august 2021 retriev october 2020 approv new botox use http www dddmag com new 2014 approv new botox cid 3751256 rid 657808477 type cta archive http web archive org web 20140222 135115 http www dddmag com new 2014 approv new botox use cid 3751256 rid 657808477 type cta febru 2014 the wayback machine dddmag com febru 2014 http wikipedia org wiki botulinum toxin",
        @"botulinum toxin wikipedia mhra approv botox for treatm ankle disabil stroke survivor http www thep harmaletter com article mhra approv botox for treatm ankle disabil stroke surviv the pharma letter archive http web archive org web 20200727033634 http www the pharmaletter com article mhra approv botox for treatm ankle disabil stroke survi vor from the origin july 2020 retriev march 2020 fda approv drug product dysport http www accessdata fda gov script cder daf index cfm event overview process applno 125274 food and drug administr fda archive http web archive org web 20161108054053 http www accessdata fda gov script daf index cfm event overview process applno 125274 from the origin november 2016 retriev november 2016 thi article incorporat text from thi source which the public domain pavone testa restivo cannavò condorelli portinaro febru 2016 botulinum toxin treatm for limb spastic childhood cerebr palsy http doi 3389 2ffphar 2016 00029 frontier pharmacol doi 3389 fphar 2016 00029 http doi org 3389 2ffphar 2016 00029 pmc 4759702 http www ncbi nlm nih gov pmc articl pmc4759702 pmid 26924985 http pubm ncbi nlm gov 26924985 sye august 2017 abobotulinumtoxina review pediatr lower limb spastic paediatr drug 367–373 doi 1007 s40272 017 0242 http doi org 1007 40272 017 0242 pmid 28623614 http pubm ncbi nlm nih gov 28623614 s2cid 24857218 http api semanticscholar org corpusid 24857218 wittich burkle lanier october 2012 ten common question and their answer about off label drug use http www ncbi nlm nih gov pmc articl pmc3538391 mayo clin proceed 982–990 doi 1016 mayocp 2012",
        @"pmid 28623614 http pubm ncbi nlm nih gov 28623614 s2cid 24857218 http api semanticscholar org corpusid 24857218 wittich burkle lanier october 2012 ten common question and their answer about off label drug use http www ncbi nlm nih gov pmc articl pmc3538391 mayo clin proceed 982–990 doi 1016 mayocp 2012 017 http doi org 1016 2fj mayocp 2012 017 pmc 3538391 http www ncbi nlm nih gov pmc articl pmc3538391 pmid 22877654 http pubm ncbi nlm nih gov 22877654 ocampo foster may 2012 infantile esotropia treatm manage http eme dicine medscape com article 1198876 treatm showall medscape archive http web archiv org web 20141128091146 http emedicine medscape com article 1198876 treatm showall from the origin november 2014 retriev april 2014 eisenach atkinson fealey may 2005 hyperhidrosi evolv therapy for well establish phenomenon http doi org 4065 2f80 657 mayo clin proceed 657–666 doi 4065 657 http doi org 4065 2f80 657 pmid 15887434 http bme ncbi nlm nih gov 15887434 fda approv botox treat chron migrain http www webmd com migrain headach new 20101018 fda approv botox treat chron migrain webmd archive http web archive org web 20170505183032 http www webmd com migrain headach new 20101018 approv botox treat chron migrain from the origin may 2017 retriev may 2017 highlight prescrib inform these highlight not include all the inform need use botox® safe and effective see full prescrib inform for botox http www accessdata fda gov drugsatfda doc label 2011 103000s5236lbl pdf pdf accessdata fda gov 2011 archive http web archive org web 20240216083806 http www",
        @"retriev may 2017 highlight prescrib inform these highlight not include all the inform need use botox® safe and effective see full prescrib inform for botox http www accessdata fda gov drugsatfda doc label 2011 103000s5236lbl pdf pdf accessdata fda gov 2011 archive http web archive org web 20240216083806 http www accessdata fda gov drugsatfda doc label 2011 103000s5236lbl pdf pdf from the origin febru 2024 retriev april 2024 satriyasa april 2019 botulinum toxin botox for reduc the appear faci wrinkl literature review clinic use and pharmacologic aspect http www ncbi nlm nih gov pmc articl pmc6489637 clinic cosmet and investigation dermatol 223–228 doi 2147 ccid s202919 http doi org 2147 2fccid s202919 pmc 6489637 http ncbi nlm nih gov pmc articl pmc6489637 pmid 31114283 http pubm ncbi nlm nih 31114283",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 21/33",
        @"botulinum toxin wikipedia small august 2014 botulinum toxin injection for faci wrinkl american fami physician 168–175 pmid 25077722 http pubm ncbi nlm nih gov 25077722 krause june 2019 jeuveau the many afford wrinkle inject http www refinery com jeuveau newtox injection for wrinkl refinery29 com archive http web archi org web 20210318023749 http www refinery29 com jeuveau newtox injection for wri nkl from the origin march 2021 retriev july 2019 drug trial snapshot daxx http www fda gov drug drug approval and databas drug tri snapshot daxx food and drug administr fda september 2022 archive htt web archive org web 20240201162717 http www fda gov drug drug approval and databa drug trial snapshot daxx from the origin febru 2024 retriev march 2024 thi article incorporat text from thi source which the public domain rev announc fda approv daxx daxibotulinumtoxina lanm for injection the first and only peptide formulat neuromodulator with long last result http www busi wire com new home 20220908005320 rev announc fda approv daxx daxibotulinumtoxina lanm for injection the first and only peptide formulat neuro modulator with long last result press release rev september 2022 archive htt web archive org web 20220910163940 http www businesswire com new home 202209080 05320 rev announc fda approv daxx daxibotulinumtoxina for injection the first and only peptide formulat neuromodulator with long last resul from the origin september 2022 retriev september 2022 via busi wire drug trial snapshot letybo http www fda gov drug drug approval and databas drug ial snapshot letybo food and drug administr fda febru 2024",
        @"daxx daxibotulinumtoxina for injection the first and only peptide formulat neuromodulator with long last resul from the origin september 2022 retriev september 2022 via busi wire drug trial snapshot letybo http www fda gov drug drug approval and databas drug ial snapshot letybo food and drug administr fda febru 2024 archive htt web archive org web 20240323195046 http www fda gov drug drug approval and databa drug trial snapshot letybo from the origin march 2024 retriev march 2024 thi article incorporat text from thi source which the public domain novel drug approval for 2024 http www fda gov drug novel drug approval fda novel drug approval 2024 food and drug administr fda april 2024 archive http web rch org web 20240430031024 http www fda gov drug novel drug approval fda novel drug approval 2024 from the origin april 2024 retriev april 2024 mitt safarpour jabbari febru 2016 botulinum toxin treatm neuropath pain seminar neurol 73–83 doi 1055 0036 1571953 http doi org 105 0036 1571953 pmid 26866499 http pubm ncbi nlm nih gov 26866499 s2cid 41120474 http api semanticscholar org corpusid 41120474 charl november 2004 botulinum neurotoxin serotype clinic update non cosmet http doi org 1093 2fajhp 2f61 suppl s11 american journ health system pharmacy suppl s11–s23 doi 1093 ajhp suppl s11 http doi org 1093 2fajhp 2f61 suppl s11 pmid 15598005 http pubm ncbi nlm nih gov 1559800 singh fitzgerald september 2010 botulinum toxin for shoulder pain the cochrane database systemat review cd008271 doi 1002 14651858 cd008271 pub2 http org 1002 2f14651858 cd008271 pub2 pmid",
        @"s11–s23 doi 1093 ajhp suppl s11 http doi org 1093 2fajhp 2f61 suppl s11 pmid 15598005 http pubm ncbi nlm nih gov 1559800 singh fitzgerald september 2010 botulinum toxin for shoulder pain the cochrane database systemat review cd008271 doi 1002 14651858 cd008271 pub2 http org 1002 2f14651858 cd008271 pub2 pmid 20824874 http pubm ncbi nlm nih gov 20824874 nigam nigam 2010 botulinum toxin http www ncbi nlm nih gov pmc articl pmc285 6357 indian journ dermatol 8–14 doi 4103 0019 5154 60343 http doi org 4103 2f0019 5154 60343 pmc 2856357 http www ncbi nlm nih gov pmc articl pmc28 56357 pmid 20418969 http pubm ncbi nlm nih gov 20418969",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 22/33",
        @"botulinum toxin wikipedia coté mohan polder walton braun september 2005 botulinum toxin type injection adverse event report the food and drug administr therapeut and cosmet cas http zenodo org record 1259075 journ the american academy dermatol 407–415 doi 1016 jaad 2005 011 http doi org 1016 2fj jaad 005 011 pmid 16112345 http pubm ncbi nlm nih gov 16112345 archive http web rch org web 20220523173847 http zenodo org record 1259075 from the origin may 2022 retriev december 2021 witmanowski błochowiak december 2020 the whole truth about botulinum toxin review http www ncbi nlm nih gov pmc articl pmc7874868 postepy dermatologii alergologii 853–861 doi 5114 ada 2019 82795 http doi org 5114 2fada 2019 2795 pmc 7874868 http www ncbi nlm nih gov pmc articl pmc7874868 pmid 33603602 http pubm ncbi nlm nih gov 33603602 witmanowski błochowiak december 2020 the whole truth about botulinum toxin review http www ncbi nlm nih gov pmc articl pmc7874868 postepy dermatologii alergologii 853–861 doi 5114 ada 2019 82795 http doi org 5114 2fada 2019 2795 pmc 7874868 http www ncbi nlm nih gov pmc articl pmc7874868 pmid 33603602 http pubm ncbi nlm nih gov 33603602 hamman goldman august 2013 minimiz bruis follow filler and other cosmet injectabl http www ncbi nlm nih gov pmc articl pmc3760599 the journ clinic and aesthet dermatol 16–18 pmc 3760599 http www ncbi nlm nih gov articl pmc3760599 pmid 24003345 http pubm ncbi nlm nih gov 24003345 schiffer april 2021 how bare there botox become the norm http www nytim com 021 style self care",
        @"nlm nih gov pmc articl pmc3760599 the journ clinic and aesthet dermatol 16–18 pmc 3760599 http www ncbi nlm nih gov articl pmc3760599 pmid 24003345 http pubm ncbi nlm nih gov 24003345 schiffer april 2021 how bare there botox become the norm http www nytim com 021 style self care how bare there botox become the norm html the new york tim issn 0362 4331 http search worldcat org issn 0362 4331 archive http ghostarch org rch 20211228 http www nytim com 2021 style self care how bare there botox beca the norm html from the origin december 2021 retriev november 2021 fda notify public adverse reaction link botox use http web archive org web 201 20302084857 http www fda gov newsevent newsroom pressannouncement 2008 ucm11685 htm food and drug administr fda febru 2008 archive from the origin htt www fda gov newsevent newsroom pressannouncement 2008 ucm116857 htm march 2012 retriev may 2012 thi article incorporat text from thi source which the public domain fda give update botulinum toxin safe warne establish nam drug chang http www pharmaceuticalonline com doc fda give update botulinum toxin safe 0001 pharmaceutic online august 2009 archive http web archive org web 20190706132201 www pharmaceuticalonline com doc fda give update botulinum toxin safe 0001 from the origin july 2019 retriev july 2019 fda give update botulinum toxin safe warne establish nam drug chang http web archive org web 20150924140939 http www fda gov newsevent newsroom pressa nnouncement 2009 ucm175013 htm press release food and drug administr fda august 2009 archive from",
        @"toxin safe 0001 from the origin july 2019 retriev july 2019 fda give update botulinum toxin safe warne establish nam drug chang http web archive org web 20150924140939 http www fda gov newsevent newsroom pressa nnouncement 2009 ucm175013 htm press release food and drug administr fda august 2009 archive from the origin http www fda gov newsevent newsroom pressanno uncement 2009 ucm175013 htm september 2015 retriev december 2022 update safe review onabotulinumtoxina market botox botox cosmet abobotulinumtoxina market dysport and rimabotulinumtoxinb market myobloc htt web archive org web 20150701032216 http www fda gov drug drugsafe postmarketdrug safetyinformationforpatientsandprovider drugsafetyinformationforheathcareprofessional ucm1 74959 htm food and drug administr fda august 2009 archive from the origin http www fda gov drug drugsafe postmarketdrugsafetyinformationforpatientsandprovider drugsafetyinformationforheathcareprofessional ucm174959 htm july 2015 retriev december 2022",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 23/33",
        @"botulinum toxin wikipedia follow the febru 2008 ear communic about ongo safe review botox and botox cosmet botulinum toxin type and myobloc botulinum toxin type http web archive org web 20150602084706 http www fda gov drug drugsafe postmarketdrug afetyinformationforpatientsandprovider drugsafetyinformationforheathcareprofessional ucm14 3819 htm food and drug administr fda febru 2008 archive from the origin http www fda gov drug drugsafe postmarketdrugsafetyinformationforpatientsandprovider drugsafetyinformationforheathcareprofessional ucm143819 htm june 2015 retriev december 2022 onabotulinumtoxina market botox botox cosmet abobotulinumtoxina market dysport and rimabotulinumtoxinb market myobloc inform http www fda gov drug postmarket drug safe inform patient and provider onabotulinumtoxina market botoxb otox cosmet abobotulinumtoxina market dysport and food and drug administr fda november 2018 archive http web archive org web 20221218020508 http www gov drug postmarket drug safe inform patient and provider onabotulinumtoxina marke botoxbotox cosmet abobotulinumtoxina market dysport and from the origin december 2022 retriev december 2022 inform for healthcare professional onabotulinumtoxina market botox botox cosmet abobotulinumtoxina market dysport and rimabotulinumtoxinb market myobloc http web archive org web 20150913185039 http www fda gov drug drugsafe ostmarketdrugsafetyinformationforpatientsandprovider drugsafetyinformationforheathcareprof essional ucm174949 htm food and drug administr fda september 2015 archive from the origin http www fda gov drug drugsafe postmarketdrugsafetyinformati onforpatientsandprovider drugsafetyinformationforheathcareprofessional ucm174949 htm september 2015 retriev september 2015 thi article incorporat text from thi source which the public domain botox chemic may spread health canada confirm http web archive org web 20090221144 310 http www cbc consumer story 2009 botox html cbc new janu 2009 archive from the origin http www cbc consumer story 2009 botox html febru 2009 kind",
        @"incorporat text from thi source which the public domain botox chemic may spread health canada confirm http web archive org web 20090221144 310 http www cbc consumer story 2009 botox html cbc new janu 2009 archive from the origin http www cbc consumer story 2009 botox html febru 2009 kind botulism http www cdc gov botulism definition html center for disease control and prevention cdc archive http web archive org web 20161005195008 http cdc gov botulism definition html from the origin october 2016 retriev october 2016 botulism diagnosi and treatm http www cdc gov botulism test treatm html center for disease control and prevention cdc archive http web archive org web 201610 05200323 http www cdc gov botulism test treatm html from the origin october 2016 retriev october 2016 botulism diagnosi and treatm http www mayoclin org diseas condition botulism dia gnosi treatm drc 20370266 mayo clin archive http web archive org web 20231101010 856 http www mayoclin org diseas condition botulism diagnosi treatm drc 20370266 from the origin november 2023 retriev november 2023 barash arnon janu 2014 novel strain clostridium botulinum that produc type and type botulinum toxin the journ infecti diseas 209 183–191 doi 1093 infdi jit449 http doi org 1093 2finfdi 2fjit449 pmid 24106296 http pub ncbi nlm nih gov 24106296 barr moura boyer woolfitt kalb pavlopoulo october 2005 botulinum neurotoxin detection and differenti mass spectrometry http www ncbi nlm gov pmc articl pmc3366733 emerg infecti diseas 1578–1583 doi 3201 eid1110 041279 http doi org 3201 2feid1110 041279 pmc 3366733 http www ncbi",
        @"24106296 http pub ncbi nlm nih gov 24106296 barr moura boyer woolfitt kalb pavlopoulo october 2005 botulinum neurotoxin detection and differenti mass spectrometry http www ncbi nlm gov pmc articl pmc3366733 emerg infecti diseas 1578–1583 doi 3201 eid1110 041279 http doi org 3201 2feid1110 041279 pmc 3366733 http www ncbi nlm nih gov pmc articl pmc3366733 pmid 16318699 http pubm ncbi nlm nih 16318699",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 24/33",
        @"botulinum toxin wikipedia dressler saberi barbosa march 2005 botulinum toxin mechanism action http doi org 1159 2f000083259 arquivo neuro psiquiatria 180–185 doi 1159 000083259 http doi org 1159 2f000083259 pmid 15830090 http pubme ncbi nlm nih gov 15830090 s2cid 16307223 http api semanticscholar org corpusid 16307 223 dong masuyer stenmark june 2019 botulinum and tetanus neurotoxin http www ncbi nlm nih gov pmc articl pmc7539302 annu review biochemistry 811–837 doi 1146 annurev biochem 013118 111654 http doi org 1146 2fannurev biochem 0131 111654 pmc 7539302 http www ncbi nlm nih gov pmc articl pmc7539302 pmid 30388027 http pubm ncbi nlm nih gov 30388027 peet butler burnett moir bowlin december 2010 small molecule inhibitor countermeasur for botulinum neurotoxin intoxic http doi org 3390 2fm olecules16010202 molecul 202–220 doi 3390 molecules16010202 http doi org 3390 2fmolecules16010202 pmc 6259422 http www ncbi nlm nih gov pmc articl pmc 6259422 pmid 21193845 http pubm ncbi nlm nih gov 21193845 hill smith 2013 genet divers within clostridium botulinum serotyp botulinum neurotoxin gene cluster and toxin subtyp rummel binz botulinum neurotoxin curr topic microbiol and immunol vol 364 springer 1–20 doi 1007 978 642 33570 http doi org 1007 2f978 642 33570 isbn 978 642 33569 pmid 23239346 http pubm ncbi nlm nih gov 23239346 davy liu acharya october 2018 variation the botulinum neurotoxin bind domain and the potenti for novel therapeutic http doi org 3390 2ftoxins10100421 toxin 421 doi 3390 toxins10100421 http doi org 3390 2ftoxins10100421 pmc 6215321 http www ncbi nlm nih gov pmc articl pmc6215321 pmid 30347838 http pubm",
        @"nih gov 23239346 davy liu acharya october 2018 variation the botulinum neurotoxin bind domain and the potenti for novel therapeutic http doi org 3390 2ftoxins10100421 toxin 421 doi 3390 toxins10100421 http doi org 3390 2ftoxins10100421 pmc 6215321 http www ncbi nlm nih gov pmc articl pmc6215321 pmid 30347838 http pubm ncbi nlm nih gov 30347838 brunt carter stringer peck febru 2018 identific novel botulinum neurotoxin gene cluster enterococcus http doi org 1002 2f1873 3468 12969 feb letter 592 310–317 doi 1002 1873 3468 12969 http doi org 1002 2f1873 3468 2969 pmc 5838542 http www ncbi nlm nih gov pmc articl pmc5838542 pmid 29323697 http pubm ncbi nlm nih gov 29323697 erbguth march 2004 historic not botulism clostridium botulinum botulinum toxin and the idea the therapeut use the toxin movem disorder supplem s2–s6 doi 1002 20003 http doi org 1002 2fmd 20003 pmid 15027048 http pubme ncbi nlm nih gov 15027048 s2cid 8190807 http api semanticscholar org corpusid 819080 erbguth naumann november 1999 historic aspect botulinum toxin justinus kerner 1786 1862 and the saus poison neurol 1850–1853 doi 1212 wnl 1850 http doi org 1212 2fwnl 1850 pmid 10563638 http bme ncbi nlm nih gov 10563638 s2cid 46559225 http api semanticscholar org corpusid 559225 monheit pickett may 2017 abobotulinumtoxina year history http www ncbi nih gov pmc articl pmc5434488 aesthet surgery journ suppl s4–s11 doi 1093 asj sjw284 http doi org 1093 2fasj 2fsjw284 pmc 5434488 http www cbi nlm nih gov pmc articl pmc5434488 pmid 28388718 http pubm ncbi nlm nih gov 388718 pellett june",
        @"may 2017 abobotulinumtoxina year history http www ncbi nih gov pmc articl pmc5434488 aesthet surgery journ suppl s4–s11 doi 1093 asj sjw284 http doi org 1093 2fasj 2fsjw284 pmc 5434488 http www cbi nlm nih gov pmc articl pmc5434488 pmid 28388718 http pubm ncbi nlm nih gov 388718 pellett june 2012 learn from the past historic aspect bacteri toxin pharmaceutical curr opinion microbiol 292–299 doi 1016 mib 2012 005 http doi org 1016 2fj mib 2012 005 pmid 22651975 ttp pubm ncbi nlm nih gov 22651975",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 25/33",
        @"botulinum toxin wikipedia home cann and botulism http www cdc gov foodsafe communic home cann and botulism html june 2022 archive http web archive org web 20220802021326 http cdc gov foodsafe communic home cann and botulism html from the origin august 2022 retriev august 2022 100 lamanna mcelroy eklund may 1946 the purific and crystalliz clostridium botulinum type toxin sci 103 2681 613–614 bibcode 1946sci 103 613l http adsab harvard edu 1946sci 103 613l doi 1126 sci 103 2681 613 http doi org 1126 2fsci 103 2681 613 pmid 21026141 http pubm ncbi nlm nih gov 21026141 101 burgen dicken zatman august 1949 the action botulinum toxin the neuro muscular junction http www ncbi nlm nih gov pmc articl pmc1392572 the journ physiol 109 1–2 10–24 doi 1113 jphysiol 1949 sp004364 http doi org 1113 2fjph ysiol 1949 sp004364 pmc 1392572 http www ncbi nlm nih gov pmc articl pmc1392572 pmid 15394302 http pubm ncbi nlm nih gov 15394302 102 magoon cruciger scott jampolsky may 1982 diagnost injection xylocaine into extraocular muscl ophthalmol 489–491 doi 1016 s0161 6420 34764 http doi org 1016 2fs0161 6420 2882 2934764 pmid 7099568 http pubm ncbi nih gov 7099568 103 drachman august 1964 atrophy skelet muscle chick embryo treat with botulinum toxin sci 145 3633 719–721 bibcode 1964sci 145 719d http adsab harvard edu 1964sci 145 719d doi 1126 sci 145 3633 719 http doi org 112 2fsci 145 3633 719 pmid 14163805 http pubm ncbi nlm nih gov 14163805 s2cid 43093912 http api semanticscholar org corpusid 43093912 104 scott",
        @"botulinum toxin sci 145 3633 719–721 bibcode 1964sci 145 719d http adsab harvard edu 1964sci 145 719d doi 1126 sci 145 3633 719 http doi org 112 2fsci 145 3633 719 pmid 14163805 http pubm ncbi nlm nih gov 14163805 s2cid 43093912 http api semanticscholar org corpusid 43093912 104 scott rosenbaum collin december 1973 pharmacolog weaken extraocular muscl investigat ophthalmol 924–927 pmid 4203467 http pubm ncbi nih gov 4203467 105 scott october 1980 botulinum toxin injection into extraocular muscl alternat strabismus surgery ophthalmol 1044–1049 doi 1016 s0161 6420 35127 ttp doi org 1016 2fs0161 6420 2880 2935127 pmid 7243198 http pubm ncbi nih gov 7243198 s2cid 27341687 http api semanticscholar org corpusid 27341687 106 boffey october 1986 loss drug relegat many blind again http www tim com 1986 sci loss drug relegat many blind again html the new york tim archive http web archive org web 20110126045116 http www nytim com 198 sci loss drug relegat many blind again html from the origin janu 2011 retriev july 2010 107 docket fda 2008 0061 http web archive org web 20100706104512 http www gov download drug drugsafe postmarketdrugsafetyinformationforpatientsandprovider ugsafetyinformationforheathcareprofessional ucm143989 pdf pdf food and drug administr fda april 2009 archive from the origin http www fda gov download drugsafe postmarketdrugsafetyinformationforpatientsandprovider drugsafetyinform forheathcareprofessional ucm143989 pdf pdf july 2010 retriev july 2010 thi article incorporat text from thi source which the public domain 108 wellman labadie zhou may 2010 the orphan drug act rare disease research stimulator commerci opportun health policy 2–3 216–228 doi 1016 healthpol 2009 001 http doi",
        @"drugsafe postmarketdrugsafetyinformationforpatientsandprovider drugsafetyinform forheathcareprofessional ucm143989 pdf pdf july 2010 retriev july 2010 thi article incorporat text from thi source which the public domain 108 wellman labadie zhou may 2010 the orphan drug act rare disease research stimulator commerci opportun health policy 2–3 216–228 doi 1016 healthpol 2009 001 http doi org 1016 2fj healthpol 2009 001 pmid 20036435 http pubm ncbi nlm nih gov 20036435 109 clark berri august 1989 botulinum toxin treatm for faci asymmetry caus faci nerve paralysi plast and reconstruct surgery 353–355 doi 1097 0000205566 47797 http doi org 1097 2f01 0000205566 47797 pmid 2748749 http pubm ncbi nlm nih gov 2748749",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 26/33",
        @"botulinum toxin wikipedia 110 rohrich jani fagien stuzin october 2003 the cosmet use botulinum toxin plast and reconstruct surgery 112 suppl 177s–188 doi 1097 0000082208 37239 http doi org 1097 2f01 0000082208 37239 pmid 14504502 http pubm ncbi nlm nih gov 14504502 111 carruther november–december 2003 history the clinic use botulinum toxin and clinic dermatol 469–472 doi 1016 clindermatol 2003 003 http doi org 1016 2fj clindermatol 2003 003 pmid 14759577 http pubm ncbi nlm nih gov 147595 112 carruther carruther janu 1992 treatm glabellar frown lin with botulinum exotoxin the journ dermatolog surgery and oncol 17–21 doi 1111 1524 4725 1992 tb03295 http doi org 1111 2fj 1524 4725 1992 tb03295 pmid 1740562 http pubm ncbi nlm nih gov 1740562 113 binder brin blitzer schoenrock pogoda december 2000 botulinum toxin type botox for treatm migraine headach open label study otolaryngology–head and neck surgery 123 669–676 doi 1067 mhn 2000 110960 http doi org 1067 mhn 2000 110960 pmid 11112955 http pubm ncbi nlm nih gov 11112955 s2cid 24406607 http api semanticscholar org corpusid 24406607 114 jackson kuriyama hayashino april 2012 botulinum toxin for prophylact treatm migraine and tension headach adult meta analysi jama 307 1736–1745 doi 1001 jama 2012 505 http doi org 1001 2fjama 2012 505 pmid 22535858 http pubm ncbi nlm nih gov 22535858 115 ramachandran yaksh september 2014 therapeut use botulinum toxin migraine mechanism action http www ncbi nlm nih gov pmc articl pmc4241086 british journ pharmacol 171 4177–4192 doi 1111 bph 12763 http doi org 1111 2fbph 763 pmc 4241086 http",
        @"2012 505 pmid 22535858 http pubm ncbi nlm nih gov 22535858 115 ramachandran yaksh september 2014 therapeut use botulinum toxin migraine mechanism action http www ncbi nlm nih gov pmc articl pmc4241086 british journ pharmacol 171 4177–4192 doi 1111 bph 12763 http doi org 1111 2fbph 763 pmc 4241086 http www ncbi nlm nih gov pmc articl pmc4241086 pmid 24819339 http pubm ncbi nlm nih gov 24819339 116 new plast surgery statistic reve trend toward body enhancem http web archive org 20190312062815 http www plasticsurgery org new press releas new plast surgery stati stic reve trend toward body enhancem plast surgery march 2019 archive from the origin http www plasticsurgery org new press releas new plast surgery statistic rev eal trend toward body enhancem march 2019 117 chapman may 2012 the glob botox market forecast reach billion 2018 http web archive org web 20120806230249 http www companiesandmarket com new healthcar and medic the glob botox market forecast reach billion 2018 ni2991 archive from the origin http www companiesandmarket com new healthcare and medic the globa botox market forecast reach billion 2018 ni2991 august 2012 retriev october 2012 118 2020 nation plast surgery statistic cosmet surgic procedur http www plasticsurg ery org document new statistic 2020 plast surgery statistic report 2020 pdf pdf american socie plast surgeon archive http web archive org web 20210623232536 htt www plasticsurgery org document new statistic 2020 plast surgery statistic report 202 pdf pdf from the origin june 2021 retriev may 2021 119 botulinum toxin market http www fortunebusinessinsight com industry report",
        @"surgery statistic report 2020 pdf pdf american socie plast surgeon archive http web archive org web 20210623232536 htt www plasticsurgery org document new statistic 2020 plast surgery statistic report 202 pdf pdf from the origin june 2021 retriev may 2021 119 botulinum toxin market http www fortunebusinessinsight com industry report botulinum toxi market 100996 fortune busi insight archive http web archive org web 2021062719 2718 http www fortunebusinessinsight com industry report botulinum toxin market 100996 from the origin june 2021 retriev may 2021 120 how much botox cost http www cosmeticassoci org cosmet procedur botox much botox cost american cosmet associ archive http web archive org 20230313122938 http www cosmeticassoci org cosmet procedur botox how much botox cost from the origin march 2023 retriev march 2013",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 27/33",
        @"botulinum toxin wikipedia 121 medicare guidelin for botox treatment http www medicarefaq com faq medicare covera for botox treatment medicarefaq com september 2021 archive http web archive web 20210523024145 http www medicarefaq com faq medicare coverage for botox treatme from the origin may 2021 retriev may 2021 122 botox onabotulinumtoxina for injection for intramuscular intradetrusor intraderm use www accessdata fda gov drugsatfda doc label 2017 103000s5302lbl pdf pdf highlight prescrib inform food and drug administr fda archive http web archiv org web 20210328131433 http www accessdata fda gov drugsatfda doc label 2017 103000 5302lbl pdf pdf from the origin march 2021 retriev may 2021 123 botox procedur what botox how work http www facialesthetic org botox pro cedur the american academy faci esthetic archive http web archive org web 210522010411 http www facialesthetic org botox procedur from the origin may 2021 retriev may 2021 124 koirala basnet july 2004 botulism botulinum toxin and bioterrorism review and update http web archive org web 20110601225033 http www medscape com viewarticle 482 812 medscape cliggott publish archive from the origin http www medscape com viewar ticle 482812 june 2011 retriev july 2010 125 public health agency canada april 2011 pathogen safe datum sheet infecti substanc clostridium botulinum http www canada public health servic laboratory biosafe biosecur pathogen safe datum sheet risk assessm clostridium botulinum html archive http web archive org web 20220124011913 http www canada public health ervic laboratory biosafe biosecur pathogen safe datum sheet risk assessm clostridium botulinum html from the origin janu 2022 retriev janu 2022 126",
        @"canada public health servic laboratory biosafe biosecur pathogen safe datum sheet risk assessm clostridium botulinum html archive http web archive org web 20220124011913 http www canada public health ervic laboratory biosafe biosecur pathogen safe datum sheet risk assessm clostridium botulinum html from the origin janu 2022 retriev janu 2022 126 fleisher roizen roizen may 2017 ess anesthesia practice book http book google com book rsqmdwaaqbaj nerve exposure will increase mortal enhanc botulinum toxin pa56 elsevier health scienc isbn 978 323 39541 archive http web archive org web 20211111023438 http book google com book rsq mdwaaqbaj nerve exposure will increase mortal enhanc botulinum toxin pa56 from the origin november 2021 retriev june 2022 127 paper http www wmddetectorselector army mil pdf 328 pdf pdf army archive http web archive org web 20201023152924 http www wmddetectorselector army mil pdf pdf pdf from the origin october 2020 retriev september 2020 paper chemical treat dye impregnat paper used detect liquid substanc for the pres and type nerve agent and and type blister agent 128 baselt 2014 disposition tox drug and chemical man seal beach biomedic publication 260–61 isbn 978 9626523 129 mcadam kornblet 2011 baader meinhof group baader meinhof gang pilch zilinska encyclopedia bioterrorism defense wiley liss 1–2 doi 1002 0471686786 ebd0012 pub2 http doi org 1002 2f0471686786 ebd0012 pub2 isbn 978 471 68678 130 botulinum toxin type product approv inform licens action http web arc hive org web 20170113111252 http www fda gov drug developmentapprovalprocess howdru gsaredevelopedandapprov approvalapplication therapeuticbiologicapplication ucm080509 htm food and drug administr fda febru 2009 archive",
        @"0471686786 ebd0012 pub2 http doi org 1002 2f0471686786 ebd0012 pub2 isbn 978 471 68678 130 botulinum toxin type product approv inform licens action http web arc hive org web 20170113111252 http www fda gov drug developmentapprovalprocess howdru gsaredevelopedandapprov approvalapplication therapeuticbiologicapplication ucm080509 htm food and drug administr fda febru 2009 archive from the origin http www fda gov drug developmentapprovalprocess howdrugsaredevelopedandapprov appr ovalapplication therapeuticbiologicapplication ucm080509 htm janu 2017 retriev december 2022 131 drug approv pack dysport abobotulinumtoxin nda 125274s000 http www accessdat fda gov drugsatfda doc nda 2009 125274s000 dysport toc cfm food and drug administr fda august 2011 archive http web archive org web 20191124011534 htt www accessdata fda gov drugsatfda doc nda 2009 125274s000 dysport toc cfm from the origin november 2019 retriev november 2019 http wikipedia org wiki botulinum toxin",
        @"botulinum toxin wikipedia 132 hugel letybo first korea obtain market approv from australia http www prnewsw ire com new releas hugel letybo first korea obtain market approv from australia 1686683 html hugel press release november 2022 archive http web archive org web 20221218020505 http www prnewswire com new releas hugel letybo first korea obtai market approv from australia 301686683 html from the origin december 2022 retriev december 2022 via newswire 133 drug approv pack xeomin incobotulinumtoxina injection nda 125360 http www acc essdata fda gov drugsatfda doc nda 2010 125360s0000toc cfm food and drug administr fda december 1999 archive http web archive org web 2020072703004 http www accessdata fda gov drugsatfda doc nda 2010 125360s0000toc cfm from the origin july 2020 retriev november 2019 134 drug approv pack jeuveau http www accessdata fda gov drugsatfda doc nda 2019 61085orig1s000toc cfm food and drug administr fda march 2019 archive htt web archive org web 20191123073132 http www accessdata fda gov drugsatfda doc nda 2019 761085orig1s000toc cfm from the origin november 2019 retriev november 2019 thi article incorporat text from thi source which the public domain 135 2011 allergan annu report http www allergan com asset pdf 2011annualreport pdf pdf allergan archive http web archive org web 20121115061406 http www allergan com asset pdf 2011annualreport pdf pdf from the origin november 2012 retriev may 2012 see pdf 136 walker dayan febru 2014 comparison and overview current avail neurotoxin http www ncbi nlm nih gov pmc articl pmc3935649 the journ clinic and aesthet dermatol 31–39 pmc 3935649 http www",
        @"http www allergan com asset pdf 2011annualreport pdf pdf from the origin november 2012 retriev may 2012 see pdf 136 walker dayan febru 2014 comparison and overview current avail neurotoxin http www ncbi nlm nih gov pmc articl pmc3935649 the journ clinic and aesthet dermatol 31–39 pmc 3935649 http www ncbi nlm nih gov pmc article pmc3935649 pmid 24587850 http pubm ncbi nlm nih gov 24587850 137 botulinum toxin type http www btxa com hugh source internation limit archive web archive org web 20080724102749 http www btxa com from the origin july 2008 retriev july 2010 138 petrou spr 2009 medy tox introduc neuronox the botulinum toxin arena http archive org web 20130320010107 http www miinew com pdf medytox eagsp09v2 0228 pdf pdf the european aesthet guide archive from the origin http www miinew pdf medytox eagsp09v2 022809 pdf pdf march 2013 retriev december 2009 139 schantz johnson march 1992 property and use botulinum toxin and other microbi neurotoxin medicine http www ncbi nlm nih gov pmc articl pmc372855 microbiologic review 80–99 doi 1128 mmbr 1992 http doi org 1128 2fmmbr 1992 pmc 372855 http www ncbi nlm nih gov pmc articl pmc372855 pmid 1579114 http pubm ncbi nlm nih gov 1579114 140 about botulism http www cdc gov botulism gener html center for disease control and prevention cdc october 2018 archive http web archive org web 20200427164333 ttp www cdc gov botulism gener html from the origin april 2020 retriev may 2020 141 poulain popoff janu 2019 why botulinum neurotoxin produc bacteria diverse and botulinum",
        @"www cdc gov botulism gener html center for disease control and prevention cdc october 2018 archive http web archive org web 20200427164333 ttp www cdc gov botulism gener html from the origin april 2020 retriev may 2020 141 poulain popoff janu 2019 why botulinum neurotoxin produc bacteria diverse and botulinum neurotoxin tox http www ncbi nlm nih gov pmc articl pmc63 57194 toxin doi 3390 toxins11010034 http doi org 3390 2ftoxins1101003 pmc 6357194 http www ncbi nlm nih gov pmc articl pmc6357194 pmid 30641949 htt pubm ncbi nlm nih gov 30641949",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 29/33",
        @"botulinum toxin wikipedia 142 hill xie foley smith munk bruce october 2009 recombin and insertion event involv the botulinum neurotoxin complex gen clostridium botulinum typ and and clostridium butyricum type strain http www ncbi nlm nih gov pmc article pmc2764570 bmc biol doi 1186 1741 7007 http doi org 1186 f1741 7007 pmc 2764570 http www ncbi nlm nih gov pmc articl pmc2764570 pmid 19804621 http pubm ncbi nlm nih gov 19804621 143 botulism http www cdc gov botulism center for disease control and prevention cdc august 2019 archive http web archive org web 20160803091921 http www cdc botulism from the origin august 2016 retriev august 2019 144 clostridium botulinum toxin form http www fda gov fil food publish fish and fisher product hazard and control guid chapter download pdf pdf food and drug administr fda march 2011 246 archive http web archive org web 202102 08183813 http www fda gov fil food publish fish and fishery product hazard and contr guid chapter download pdf pdf from the origin febru 2021 retriev march 2023 145 licciardello nickerson ribich goldblith march 1967 therm inactiv type botulinum toxin http www ncbi nlm nih gov pmc articl pmc546888 appli microbiol 249–256 doi 1128 aem 249 256 1967 http doi org 1128 2faem 249 256 1967 pmc 546888 http www ncbi nlm nih gov pmc articl pmc546888 pmid 5339838 http pubm ncbi nlm nih gov 5339838 146 setlow april 2007 will surv dna protection bacteri spor trend microbiol 172–180 doi 1016 tim 2007 004 http doi org 1016 2fj tim",
        @"org 1128 2faem 249 256 1967 pmc 546888 http www ncbi nlm nih gov pmc articl pmc546888 pmid 5339838 http pubm ncbi nlm nih gov 5339838 146 setlow april 2007 will surv dna protection bacteri spor trend microbiol 172–180 doi 1016 tim 2007 004 http doi org 1016 2fj tim 2007 004 pmid 17336071 http pubm ncbi nlm nih gov 17336071 147 capková salzameda janda october 2009 investigation into small molecule non peptid inhibitor the botulinum neurotoxin http www ncbi nlm nih gov pmc articl pmc27 30986 toxicon 575–582 doi 1016 toxicon 2009 016 http doi org 1016 2fj toxicon 2009 016 pmc 2730986 http www ncbi nlm nih gov pmc articl pmc2730986 pmid 19327377 http pubm ncbi nlm nih gov 19327377 148 flander tischler wise william beneish auger june 1987 injection type botulinum toxin into extraocular muscl for correction strabismus canadian journ ophthalmol journ canadien ophtalmologie 212–217 pmid 3607594 http pub ncbi nlm nih gov 3607594 149 botulinum toxin therapy eye muscle disorder safe and effective american academy ophthalmol ophthalmol suppl suppl 37–41 september 1989 doi 1016 s0161 6420 32989 http doi org 1016 2fs0161 6420 2889 2932989 pmid 2779991 http pubm ncbi nlm nih gov 2779991 150 hellman torr russotto march 2015 botulinum toxin the manage blepharospasm curr evid and rec development http www ncbi nlm nih gov pmc ticl pmc4356659 therapeut advanc neurologic disorder 82–91 doi 1177 1756285614557475 http doi org 1177 2f1756285614557475 pmc 4356659 http www ncbi nlm nih gov pmc articl pmc4356659 pmid 25922620 http pubm ncbi nih gov 25922620 151",
        @"the manage blepharospasm curr evid and rec development http www ncbi nlm nih gov pmc ticl pmc4356659 therapeut advanc neurologic disorder 82–91 doi 1177 1756285614557475 http doi org 1177 2f1756285614557475 pmc 4356659 http www ncbi nlm nih gov pmc articl pmc4356659 pmid 25922620 http pubm ncbi nih gov 25922620 151 koudsie coste verdier paya chan andrebe pechmeja april 2021 long term outcom botulinum toxin injection infantile esotropia journ françai ophtalmologie 509–518 doi 1016 jfo 2020 023 http doi org 1016 2fj jfo 020 023 pmid 33632627 http pubm ncbi nlm nih gov 33632627 s2cid 232058260 htt api semanticscholar org corpusid 232058260 152 keen kopelman aviv binder brin blitzer april 1994 botulinum toxin novel method remove periorbit wrinkl faci plast surgery 141–146 doi 1055 2008 1064563 http doi org 1055 2008 1064563 pmid 7995530 http pubm ncbi nlm nih gov 7995530 s2cid 29006338 http api semanticscholar org corpusi 29006338 http wikipedia org wiki botulinum toxin",
        @"botulinum toxin wikipedia 153 botulinum toxin type product approv inform licens action http web arc hive org web 20100308063343 http www fda gov drug developmentapprovalprocess howdru gsaredevelopedandapprov approvalapplication therapeuticbiologicapplication ucm080509 htm food and drug administr fda october 2009 archive from the origin http www fda gov drug developmentapprovalprocess howdrugsaredevelopedandapprov appr ovalapplication therapeuticbiologicapplication ucm080509 htm march 2010 retriev july 2010 thi article incorporat text from thi source which the public domain 154 giesler 2012 how doppelgänger brand imag influ the market cre process longitudin insight from the rise botox cosmet journ market 55–68 doi 1509 0406 http doi org 1509 2fjm 0406 s2cid 167319134 http api manticscholar org corpusid 167319134 155 botox cosmet onabotulinumtoxina product inform http www botox com allergan janu 2014 archive http web archive org web 20210721001858 http www botox com from the origin july 2021 retriev march 2018 156 allergan receiv fda approv for first kind full vitro cell bas assay for botox and botox cosmet onabotulinumtoxina http web archive org web 20110626185759 http cli shareholder com releasedetail cfm releaseid 587234 allergan june 2011 archive from the origin http agn cli shareholder com releasedetail cfm releaseid 58723 june 2011 retriev june 2011 157 few alternativ test animal http www washingtonpost com dyn conte article 2008 ar2008041103733 html the washington post april 2008 archive htt web archive org web 20121112163835 http www washingtonpost com dyn cont articl 2008 ar2008041103733 html from the origin november 2012 retriev june 2011 158 nayyar kumar nayyar singh december 2014 botox broaden the horizon dentistry http www ncbi nlm",
        @"article 2008 ar2008041103733 html the washington post april 2008 archive htt web archive org web 20121112163835 http www washingtonpost com dyn cont articl 2008 ar2008041103733 html from the origin november 2012 retriev june 2011 158 nayyar kumar nayyar singh december 2014 botox broaden the horizon dentistry http www ncbi nlm nih gov pmc articl pmc4316364 journ clinic and diagnost research ze25–ze29 doi 7860 jcdr 2014 11624 5341 http doi org 7860 2fjcdr 2f2014 2f11624 5341 pmc 4316364 http www ncbi nlm nih gov pmc art pmc4316364 pmid 25654058 http pubm ncbi nlm nih gov 25654058 159 hwang hur song koh baik janu 2009 surface anatomy the lip elevator muscl for the treatm gummy smile using botulinum toxin http doi org 2319 2f091407 437 the angle orthodontist 70–77 doi 2319 091407 437 http doi org 2319 2f091407 437 pmid 19123705 http bme ncbi nlm nih gov 19123705 160 gracco tracey may 2010 botox and the gummy smile progress orthodontic 76–82 doi 1016 pio 2010 004 http doi org 1016 2fj pio 2010 004 pmid 20529632 http pubm ncbi nlm nih gov 20529632 161 mazzuco hexsel december 2010 gummy smile and botulinum toxin new approach bas the gingiv exposure area journ the american academy dermatol 1042–1051 doi 1016 jaad 2010 053 http doi org 1016 2fj jaad 2010 053 pmid 21093661 http pubm ncbi nlm nih gov 21093661 162 khan campisi nadarajah shakur khan semenuk april 2011 botulinum toxin for treatm sialorrhea child effect minimal invas approach http doi org 1001 2farchoto 2010 240 archive otolaryngology–head neck surgery",
        @"jaad 2010 053 http doi org 1016 2fj jaad 2010 053 pmid 21093661 http pubm ncbi nlm nih gov 21093661 162 khan campisi nadarajah shakur khan semenuk april 2011 botulinum toxin for treatm sialorrhea child effect minimal invas approach http doi org 1001 2farchoto 2010 240 archive otolaryngology–head neck surgery 137 339–344 doi 1001 archoto 2010 240 http doi org 1001 2farcho 2010 240 pmid 21242533 http pubm ncbi nlm nih gov 21242533 163 fda approv botox treat chron migraine http web archive org web 20101019002022 htt www fda gov newsevent newsroom pressannouncement ucm229782 htm press release food and drug administr fda october 2010 archive from the origin http www fda gov newsevent newsroom pressannouncement ucm229782 htm october 2010 retriev november 2019 thi article incorporat text from thi source which the public domain http wikipedia org wiki botulinum toxin",
        @"botulinum toxin wikipedia 164 watkin october 2010 fda approv botox migraine preventat http cnn com 010 health migrain botox index html cnn archive http web archive org web 202 00727025543 http cnn com 2010 health migrain botox index html from the origin july 2020 retriev october 2010 165 dodick turkel degryse aurora silberstein lipton june 2010 onabotulinumtoxina for treatm chron migraine pool result from the double blind randomiz placebo controll phas the preempt clinic program headache 921–936 doi 1111 1526 4610 2010 01678 http doi org 1111 2fj 1526 4610 2010 678 pmid 20487038 http pubm ncbi nlm nih gov 20487038 s2cid 9621285 http api semanticscholar org corpusid 9621285 166 ashkenazi march 2010 botulinum toxin type for chron migraine curr neurol and neurosci report 140–146 doi 1007 s11910 010 0087 http doi org 100 2fs11910 010 0087 pmid 20425239 http pubm ncbi nlm nih gov 20425239 s2cid 32191932 http api semanticscholar org corpusid 32191932 167 magid keel reichenberg november 2015 neurotoxin expand neuromodulator medicine major depress disorder plast and reconstruct surgery 136 suppl 111s–119 doi 1097 0000000000001733 http doi org 1097 2fpr 0000000000001733 pmid 26441090 http pubm ncbi nlm nih gov 26441090 s2cid 24196194 http api semanticscholar org corpusid 24196194 168 onabotulinum toxin allergan adisinsight http adisinsight springer com drug 800008810 archive http web archive org web 20171030223633 http adisinsight springer com drug 800 008810 from the origin october 2017 retriev september 2017 169 finzi rosenth may 2014 treatm depression with onabotulinumtoxina randomiz double blind placebo controll tri journ psychiatr research 1–6 doi 1016 jpsychir 2013",
        @"adisinsight springer com drug 800008810 archive http web archive org web 20171030223633 http adisinsight springer com drug 800 008810 from the origin october 2017 retriev september 2017 169 finzi rosenth may 2014 treatm depression with onabotulinumtoxina randomiz double blind placebo controll tri journ psychiatr research 1–6 doi 1016 jpsychir 2013 006 http doi org 1016 2fj jpsychir 2013 006 pmid 24345483 http pubm ncbi nlm nih gov 24345483 170 clinic tri number nct01917006 http www clinicaltrial gov show nct01917006 for exploratory study the safe and efficacy botox for the treatm premature ejacul clinicaltrial gov",
        @"Further reading",
        @"Carruthers JD, Fagien S, Joseph JH, Humphrey SD, Biesman BS, Gallagher CJ, et al. (January 2020). ""DaxibotulinumtoxinA for Injection for the Treatment of Glabellar Lines: Results from Each of Two Multicenter, Randomized, Double-Blind, Placebo-Controlled, Phase 3 Studies (SAKURA 1 and SAKURA 2)"" (https://doi.org/10.1097%2FPRS.0000000000006327). Plastic and Reconstructive Surgery. 145 (1): 45–58. doi:10.1097/PRS.0000000000006327 (https://doi.org/10. 1097%2FPRS.0000000000006327). PMC 6940025 (https://www.ncbi.nlm.nih.gov/pmc/articles/PM C6940025). PMID 31609882 (https://pubmed.ncbi.nlm.nih.gov/31609882). Solish N, Carruthers J, Kaufman J, Rubio RG, Gross TM, Gallagher CJ (December 2021). ""Overview of DaxibotulinumtoxinA for Injection: A Novel Formulation of Botulinum Toxin Type A"" (h ttps://doi.org/10.1007%2Fs40265-021-01631-w). Drugs. 81 (18): 2091–2101. doi:10.1007/s40265- 021-01631-w (https://doi.org/10.1007%2Fs40265-021-01631-w). PMC 8648634 (https://www.ncbi.",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 32/33",
        @"8/23/24, 8:59 PM Botulinum toxin - Wikipedia nlm.nih.gov/pmc/articles/PMC8648634). PMID 34787840 (https://pubmed.ncbi.nlm.nih.gov/34787 840).",
        @"External links",
        @"Overview of all the structural information available in the PDB for UniProt: P0DPI1 (https://www.eb i.ac.uk/pdbe/pdbe-kb/proteins/P0DPI1) (Botulinum neurotoxin type A) at the PDBe-KB. Overview of all the structural information available in the PDB for UniProt: P10844 (https://www.eb i.ac.uk/pdbe/pdbe-kb/proteins/P10844) (Botulinum neurotoxin type B) at the PDBe-KB. Overview of all the structural information available in the PDB for UniProt: A0A0X1KH89 (https://w ww.ebi.ac.uk/pdbe/pdbe-kb/proteins/A0A0X1KH89) (Bontoxilysin A) at the PDBe-KB. ""AbobotulinumtoxinA Injection"" (https://medlineplus.gov/druginfo/meds/a609035.html). MedlinePlus. ""IncobotulinumtoxinA Injection"" (https://medlineplus.gov/druginfo/meds/a611008.html). MedlinePlus. ""OnabotulinumtoxinA Injection"" (https://medlineplus.gov/druginfo/meds/a608013.html). MedlinePlus. ""PrabotulinumtoxinA-xvfs Injection"" (https://medlineplus.gov/druginfo/meds/a619021.html). MedlinePlus. ""RimabotulinumtoxinB Injection"" (https://medlineplus.gov/druginfo/meds/a608014.html). MedlinePlus.",
        @"Retrieved from ""https://en.wikipedia.org/w/index.php?title=Botulinum_toxin&oldid=1240833172""",
        @"https://en.wikipedia.org/wiki/Botulinum_toxin 33/33"
    };

                var tasks = new List<Task<float[][]>>();
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
               
                for (int i = 0; i < 5; i++)
                {
                    var taskTexts = textList.ToArray();
                    var task = _AIDriver.Embeddings.Generate(_CurrentModel, taskTexts);
                    tasks.Add(task);
                    
                    Console.WriteLine($"Started parallel task {i + 1}/5");
                }

                Console.WriteLine("Waiting for all parallel tasks to complete...");
                var results = await Task.WhenAll(tasks);
                stopwatch.Stop();

                Console.WriteLine($"✓ Generated {results.Length * results[0].Length} embeddings in {stopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine($"✓ Average time per embedding: {stopwatch.ElapsedMilliseconds / (results.Length * results[0].Length)}ms");

                // Verify results
                AssertResultsValid(results, "Intensive Parallel");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Intensive parallel embedding test failed: {ex.Message}");
                throw;
            }
        }

        static void AssertResultsValid(float[][] results, string testName)
        {
            if (results == null || results.Length == 0)
            {
                throw new Exception($"{testName} test returned null or empty results");
            }

            foreach (var embedding in results)
            {
                if (embedding == null || embedding.Length == 0)
                {
                    throw new Exception($"{testName} test returned null or empty embedding");
                }
            }

            Console.WriteLine($"✓ {testName} results are valid");
        }

        static void AssertResultsValid(IEnumerable<float[][]> results, string testName)
        {
            foreach (var result in results)
            {
                AssertResultsValid(result, testName);
            }
        }

        #endregion

        #endregion

        #region Helpers

        static bool EnsureModelSelected()
        {
            if (String.IsNullOrEmpty(_CurrentModel))
            {
                Console.WriteLine("No model selected. Use 'model' command to select a model.");
                return false;
            }
            return true;
        }

        static double CosineSimilarity(float[] vector1, float[] vector2)
        {
            if (vector1.Length != vector2.Length)
                throw new ArgumentException("Vectors must have the same length");

            double dotProduct = 0;
            double magnitude1 = 0;
            double magnitude2 = 0;

            for (int i = 0; i < vector1.Length; i++)
            {
                dotProduct += vector1[i] * vector2[i];
                magnitude1 += vector1[i] * vector1[i];
                magnitude2 += vector2[i] * vector2[i];
            }

            magnitude1 = Math.Sqrt(magnitude1);
            magnitude2 = Math.Sqrt(magnitude2);

            if (magnitude1 == 0 || magnitude2 == 0)
                return 0;

            return dotProduct / (magnitude1 * magnitude2);
        }

        #endregion

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}