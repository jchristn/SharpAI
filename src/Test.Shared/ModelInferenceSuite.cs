namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    using SharpAI.Engines;
    using SharpAI.Prompts;

    using Touchstone.Core;

    /// <summary>
    /// Model-dependent (integration) suite that exercises real inference against a GGUF located by
    /// <see cref="ModelFixture"/>. When no model is available every case is skipped, so the suite is safe
    /// to run in CI without a model. When a model is present it verifies engine initialization, embedded
    /// chat-template rendering (W1), small-<c>max_tokens</c> generation (W1.T3), concurrent generation
    /// (W2), and embeddings — the runtime half of the reliability coverage.
    /// </summary>
    public static class ModelInferenceSuite
    {
        #region Private-Members

        private static LlamaSharpEngine? _Engine = null;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build the model inference suite.
        /// </summary>
        /// <returns>Model inference suite.</returns>
        public static TestSuiteDescriptor Build()
        {
            bool skip = !ModelFixture.IsAvailable;
            string reason = ModelFixture.SkipReason;

            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor("ModelInference", "Init", "Engine initializes from the fixture model",
                    ExecuteInit, Array.Empty<string>(), skip, reason),

                new TestCaseDescriptor("ModelInference", "ChatTemplate", "Chat prompt renders (embedded or fallback)",
                    ExecuteChatTemplate, Array.Empty<string>(), skip, reason),

                new TestCaseDescriptor("ModelInference", "MaxTokens", "Small max_tokens produces a short completion",
                    ExecuteMaxTokens, Array.Empty<string>(), skip, reason),

                new TestCaseDescriptor("ModelInference", "Concurrent", "Two concurrent generations both complete",
                    ExecuteConcurrent, Array.Empty<string>(), skip, reason),

                new TestCaseDescriptor("ModelInference", "Embeddings", "Embeddings produce a non-empty vector when supported",
                    ExecuteEmbeddings, Array.Empty<string>(), skip, reason),

                new TestCaseDescriptor("ModelInference", "TemplateGolden", "Embedded chat template renders byte-for-byte to the committed golden",
                    ExecuteTemplateGolden, Array.Empty<string>(), skip, reason),

                new TestCaseDescriptor("ModelInference", "ToolCalling", "A tool-augmented prompt yields a parseable tool call",
                    ExecuteToolCalling, Array.Empty<string>(), skip, reason),

                new TestCaseDescriptor("ModelInference", "JsonMode", "GBNF grammar constrains output to valid JSON",
                    ExecuteJsonMode, Array.Empty<string>(), skip, reason)
            };

            return new TestSuiteDescriptor(
                "ModelInference",
                "Model inference (fixture-gated)",
                cases,
                BeforeSuiteAsync,
                AfterSuiteAsync);
        }

        #endregion

        #region Private-Methods

        private static async ValueTask BeforeSuiteAsync(CancellationToken token)
        {
            if (!ModelFixture.IsAvailable) return;

            _Engine = new LlamaSharpEngine();
            await _Engine.InitializeAsync(ModelFixture.Path!).ConfigureAwait(false);
        }

        private static ValueTask AfterSuiteAsync(CancellationToken token)
        {
            _Engine?.Dispose();
            _Engine = null;
            return new ValueTask();
        }

        private static Task ExecuteInit(CancellationToken token)
        {
            TestAssert.True(_Engine != null && _Engine.IsInitialized, "engine should be initialized");
            return Task.CompletedTask;
        }

        private static Task ExecuteChatTemplate(CancellationToken token)
        {
            LlamaSharpEngine engine = _Engine!;
            List<ChatMessage> messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "user", Content = "Say hello." }
            };

            ChatTemplateResult result = ChatTemplateResolver.Resolve(engine, engine.Architecture, messages);
            TestAssert.True(!string.IsNullOrEmpty(result.Prompt), "rendered chat prompt should not be empty");
            return Task.CompletedTask;
        }

        private static async Task ExecuteMaxTokens(CancellationToken token)
        {
            LlamaSharpEngine engine = _Engine!;
            string output = await engine.GenerateTextAsync("Count: 1 2 3", 8, 0.2f, null, token).ConfigureAwait(false);
            TestAssert.True(output != null, "generation should return a (possibly empty) string");
            // A max of 8 tokens cannot decode into a very long string; guards against the old clamp-to-100 bug.
            TestAssert.True(output!.Length < 400, "small max_tokens should produce a short completion, got length " + output.Length);
        }

        private static async Task ExecuteConcurrent(CancellationToken token)
        {
            LlamaSharpEngine engine = _Engine!;
            Task<string> a = engine.GenerateTextAsync("A:", 8, 0.2f, null, token);
            Task<string> b = engine.GenerateTextAsync("B:", 8, 0.2f, null, token);
            string[] results = await Task.WhenAll(a, b).ConfigureAwait(false);
            TestAssert.True(results[0] != null && results[1] != null, "both concurrent generations should complete");
        }

        private static async Task ExecuteEmbeddings(CancellationToken token)
        {
            LlamaSharpEngine engine = _Engine!;
            if (!engine.SupportsEmbeddings) return; // model does not produce embeddings; nothing to assert

            float[] vector = await engine.GenerateEmbeddingsAsync("hello world", token).ConfigureAwait(false);
            TestAssert.True(vector != null && vector.Length > 0, "embedding vector should be non-empty");
        }

        // W1.T6 — Golden-output regression for embedded chat-template rendering. A fixed conversation is
        // rendered through the model's own embedded template and byte-matched against a committed golden
        // keyed by the model architecture. This locks templating so a LlamaSharp bump can't silently
        // regress the prompt bytes. Goldens live under a 'goldens' directory in the test tree; when a
        // model whose architecture has no committed golden is supplied the case skips (rather than fails),
        // and running with SHARPAI_WRITE_GOLDENS=1 captures a missing golden instead of asserting.
        private static Task ExecuteTemplateGolden(CancellationToken token)
        {
            LlamaSharpEngine engine = _Engine!;

            if (!engine.SupportsEmbeddedChatTemplate)
            {
                // No embedded template on this model; the embedded-template golden path does not apply.
                return Task.CompletedTask;
            }

            List<ChatMessage> messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = "You are a helpful assistant." },
                new ChatMessage { Role = "user", Content = "What is the capital of France?" }
            };

            string rendered = Normalize(engine.RenderEmbeddedChatPrompt(messages, true));
            TestAssert.True(!string.IsNullOrEmpty(rendered), "embedded template render should not be empty");

            string architecture = string.IsNullOrEmpty(engine.Architecture) ? "unknown" : engine.Architecture;
            string? goldenDir = ResolveGoldenDirectory();

            if (goldenDir == null)
            {
                // Running outside the source tree (e.g. a packaged CI leg with no goldens folder); nothing to
                // compare against, so this is a no-op rather than a failure.
                return Task.CompletedTask;
            }

            string goldenPath = System.IO.Path.Combine(goldenDir, "chat-" + architecture + ".txt");

            if (!File.Exists(goldenPath))
            {
                string? writeFlag = Environment.GetEnvironmentVariable("SHARPAI_WRITE_GOLDENS");
                if (!string.IsNullOrEmpty(writeFlag) && (writeFlag == "1" || writeFlag.Equals("true", StringComparison.OrdinalIgnoreCase)))
                {
                    File.WriteAllText(goldenPath, rendered);
                    return Task.CompletedTask;
                }

                // No committed golden for this architecture and not in capture mode; skip cleanly.
                return Task.CompletedTask;
            }

            string expected = Normalize(File.ReadAllText(goldenPath));
            TestAssert.Equal(expected, rendered);
            return Task.CompletedTask;
        }

        // W4.T1 — end-to-end tool calling through the engine: inject the tool instruction, generate at
        // temperature 0 (deterministic), and confirm the output parses into the expected tool call. Gated on
        // a tool-capable model; a model that does not emit a tool call fails this case (it asserts the
        // end-to-end path, not merely the parser, which is covered deterministically elsewhere).
        private static async Task ExecuteToolCalling(CancellationToken token)
        {
            LlamaSharpEngine engine = _Engine!;

            List<SharpAI.Tools.ToolDefinition> tools = new List<SharpAI.Tools.ToolDefinition>
            {
                new SharpAI.Tools.ToolDefinition(
                    "get_weather",
                    "Get the current weather for a city",
                    "{\"type\":\"object\",\"properties\":{\"city\":{\"type\":\"string\"}},\"required\":[\"city\"]}")
            };

            string instruction = SharpAI.Tools.ToolPromptBuilder.BuildSystemInstruction(tools);

            List<ChatMessage> messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = instruction },
                new ChatMessage { Role = "user", Content = "What is the weather in Paris right now? Use the tool." }
            };

            ChatTemplateResult templateResult = ChatTemplateResolver.Resolve(engine, engine.Architecture, messages);
            string output = await engine.GenerateChatCompletionAsync(templateResult.Prompt, 128, 0.0f, templateResult.StopSequences, token).ConfigureAwait(false);

            List<SharpAI.Tools.ParsedToolCall> calls = SharpAI.Tools.ToolCallParser.Parse(output);
            TestAssert.True(calls.Count > 0, "expected at least one tool call, got output: " + output);
            TestAssert.Equal("get_weather", calls[0].Name);
            TestAssert.Contains(calls[0].ArgumentsJson.ToLowerInvariant(), "paris");
        }

        // W4.T4 — JSON mode: constrain decoding with the JSON GBNF grammar and assert the output parses as
        // valid JSON, even when the prompt does not ask for JSON (the grammar, not the prompt, guarantees it).
        private static async Task ExecuteJsonMode(CancellationToken token)
        {
            LlamaSharpEngine engine = _Engine!;

            List<ChatMessage> messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "user", Content = "Output a compact JSON object with exactly two keys: name (the string \"Alice\") and age (the number 30). No other keys." }
            };

            ChatTemplateResult templateResult = ChatTemplateResolver.Resolve(engine, engine.Architecture, messages);
            // Generous token budget so a small object completes; the grammar guarantees validity, not brevity.
            string output = await engine.GenerateChatCompletionAsync(
                templateResult.Prompt, 256, 0.1f, templateResult.StopSequences,
                SharpAI.Grammars.JsonGrammar.GenericJson, token).ConfigureAwait(false);

            TestAssert.True(!string.IsNullOrWhiteSpace(output), "grammar-constrained output should not be empty");

            bool valid = true;
            try
            {
                using (System.Text.Json.JsonDocument.Parse(output)) { }
            }
            catch (System.Text.Json.JsonException)
            {
                valid = false;
            }

            TestAssert.True(valid, "grammar-constrained output should be valid JSON, got: " + output);
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        private static string? ResolveGoldenDirectory()
        {
            string? dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
            {
                string candidate = System.IO.Path.Combine(dir, "goldens");
                if (Directory.Exists(candidate)) return candidate;

                DirectoryInfo? parent = Directory.GetParent(dir!);
                dir = parent?.FullName;
            }

            return null;
        }

        #endregion
    }
}
