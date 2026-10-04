namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using SharpAI.Prompts;

    using Touchstone.Core;

    /// <summary>
    /// Touchstone suites for the deterministic prompt-formatting surface: <see cref="ChatFormatHelper"/>,
    /// <see cref="ChatPromptBuilder"/>, and <see cref="TextGenerationPromptBuilder"/>. These have no native
    /// dependency and run fast, so they carry a large share of the reliability coverage.
    /// </summary>
    public static class PromptSuites
    {
        #region Public-Methods

        /// <summary>
        /// Suite covering the family-to-format mapping, default stop sequences, and system-message support.
        /// </summary>
        /// <returns>Chat-format helper suite.</returns>
        public static TestSuiteDescriptor ChatFormatSuite()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();

            Dictionary<string, ChatFormatEnum> families = new Dictionary<string, ChatFormatEnum>
            {
                { "llama2", ChatFormatEnum.Llama2 },
                { "llama-2", ChatFormatEnum.Llama2 },
                { "llama3", ChatFormatEnum.Llama3 },
                { "llama-3.1", ChatFormatEnum.Llama3 },
                { "llama4", ChatFormatEnum.Llama3 },
                { "alpaca", ChatFormatEnum.Alpaca },
                { "vicuna", ChatFormatEnum.Vicuna },
                { "mistral", ChatFormatEnum.Mistral },
                { "mixtral", ChatFormatEnum.Mistral },
                { "claude", ChatFormatEnum.HumanAssistant },
                { "anthropic", ChatFormatEnum.HumanAssistant },
                { "gpt-4", ChatFormatEnum.ChatML },
                { "phi-3", ChatFormatEnum.Phi },
                { "zephyr", ChatFormatEnum.Zephyr },
                { "deepseek", ChatFormatEnum.DeepSeek },
                { "gemma", ChatFormatEnum.Gemma },
                { "gemma2", ChatFormatEnum.Gemma },
                { "gemma3", ChatFormatEnum.Gemma },
                { "command-r", ChatFormatEnum.CommandR },
                { "stablelm", ChatFormatEnum.StableLM },
                { "qwen", ChatFormatEnum.ChatML },
                { "qwen2", ChatFormatEnum.ChatML },
                { "qwen3", ChatFormatEnum.ChatML },
                { "yi", ChatFormatEnum.ChatML },
                { "openchat", ChatFormatEnum.ChatML },
                { "smollm3", ChatFormatEnum.ChatML },
                { "orca", ChatFormatEnum.Alpaca },
                { "solar", ChatFormatEnum.Alpaca },
                { "falcon", ChatFormatEnum.Simple },
                { "baichuan", ChatFormatEnum.Simple },
                { "rwkv", ChatFormatEnum.Simple }
            };

            foreach (KeyValuePair<string, ChatFormatEnum> pair in families)
            {
                string family = pair.Key;
                ChatFormatEnum expected = pair.Value;
                cases.Add(new TestCaseDescriptor(
                    "ChatFormat", "Family_" + family, "Family '" + family + "' maps to " + expected,
                    ct =>
                    {
                        TestAssert.Equal(expected, ChatFormatHelper.ModelFamilyToChatFormat(family));
                        return Task.CompletedTask;
                    }));
            }

            // New 2.0-era aliases that must resolve correctly.
            Dictionary<string, ChatFormatEnum> aliases = new Dictionary<string, ChatFormatEnum>
            {
                { "qwen3.5", ChatFormatEnum.ChatML },
                { "qwen-3.5", ChatFormatEnum.ChatML },
                { "qwen35", ChatFormatEnum.ChatML },
                { "gemma4", ChatFormatEnum.Gemma },
                { "gemma-4", ChatFormatEnum.Gemma },
                { "google-gemma-4", ChatFormatEnum.Gemma }
            };

            foreach (KeyValuePair<string, ChatFormatEnum> pair in aliases)
            {
                string alias = pair.Key;
                ChatFormatEnum expected = pair.Value;
                cases.Add(new TestCaseDescriptor(
                    "ChatFormat", "Alias_" + alias, "Alias '" + alias + "' maps to " + expected,
                    ct =>
                    {
                        TestAssert.Equal(expected, ChatFormatHelper.ModelFamilyToChatFormat(alias));
                        return Task.CompletedTask;
                    }));
            }

            cases.Add(new TestCaseDescriptor(
                "ChatFormat", "Null_Simple", "Null family falls back to Simple",
                ct => { TestAssert.Equal(ChatFormatEnum.Simple, ChatFormatHelper.ModelFamilyToChatFormat(null)); return Task.CompletedTask; }));

            cases.Add(new TestCaseDescriptor(
                "ChatFormat", "Empty_Simple", "Empty family falls back to Simple",
                ct => { TestAssert.Equal(ChatFormatEnum.Simple, ChatFormatHelper.ModelFamilyToChatFormat("   ")); return Task.CompletedTask; }));

            cases.Add(new TestCaseDescriptor(
                "ChatFormat", "Unknown_Simple", "Unknown family falls back to Simple",
                ct => { TestAssert.Equal(ChatFormatEnum.Simple, ChatFormatHelper.ModelFamilyToChatFormat("totally-made-up-model")); return Task.CompletedTask; }));

            cases.Add(new TestCaseDescriptor(
                "ChatFormat", "Fallback_Overload", "Unknown family with fallback returns the fallback",
                ct => { TestAssert.Equal(ChatFormatEnum.ChatML, ChatFormatHelper.ModelFamilyToChatFormat("unknown-x", ChatFormatEnum.ChatML)); return Task.CompletedTask; }));

            cases.Add(new TestCaseDescriptor(
                "ChatFormat", "Fallback_ExplicitSimple", "Explicitly-Simple family ignores the fallback",
                ct => { TestAssert.Equal(ChatFormatEnum.Simple, ChatFormatHelper.ModelFamilyToChatFormat("falcon", ChatFormatEnum.ChatML)); return Task.CompletedTask; }));

            // Default stop sequences per format contain the expected terminator.
            Dictionary<ChatFormatEnum, string> stops = new Dictionary<ChatFormatEnum, string>
            {
                { ChatFormatEnum.ChatML, "<|im_end|>" },
                { ChatFormatEnum.Llama2, "</s>" },
                { ChatFormatEnum.Llama3, "<|eot_id|>" },
                { ChatFormatEnum.Alpaca, "### Instruction:" },
                { ChatFormatEnum.Mistral, "</s>" },
                { ChatFormatEnum.Gemma, "<end_of_turn>" },
                { ChatFormatEnum.CommandR, "<|END_OF_TURN_TOKEN|>" },
                { ChatFormatEnum.Vicuna, "USER:" }
            };

            foreach (KeyValuePair<ChatFormatEnum, string> pair in stops)
            {
                ChatFormatEnum format = pair.Key;
                string token = pair.Value;
                cases.Add(new TestCaseDescriptor(
                    "ChatFormat", "Stops_" + format, format + " stop sequences include '" + token + "'",
                    ct =>
                    {
                        string[] seqs = ChatFormatHelper.GetDefaultStopSequences(format);
                        bool found = false;
                        foreach (string s in seqs) { if (s == token) { found = true; break; } }
                        TestAssert.True(found, format + " stop sequences should contain " + token);
                        return Task.CompletedTask;
                    }));
            }

            cases.Add(new TestCaseDescriptor(
                "ChatFormat", "SupportsSystem_ChatML", "ChatML supports system messages",
                ct => { TestAssert.True(ChatFormatHelper.SupportsSystemMessage(ChatFormatEnum.ChatML), "ChatML should support system"); return Task.CompletedTask; }));

            return new TestSuiteDescriptor("ChatFormat", "Chat format mapping", cases);
        }

        /// <summary>
        /// Suite covering <see cref="ChatPromptBuilder.Build"/> for every supported format plus null/empty input.
        /// </summary>
        /// <returns>Chat prompt builder suite.</returns>
        public static TestSuiteDescriptor ChatPromptBuilderSuite()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();

            cases.Add(new TestCaseDescriptor(
                "ChatBuild", "Null_Empty", "Null message list yields empty string",
                ct => { TestAssert.Equal("", ChatPromptBuilder.Build(ChatFormatEnum.ChatML, null)); return Task.CompletedTask; }));

            cases.Add(new TestCaseDescriptor(
                "ChatBuild", "Empty_Empty", "Empty message list yields empty string",
                ct => { TestAssert.Equal("", ChatPromptBuilder.Build(ChatFormatEnum.ChatML, new List<ChatMessage>())); return Task.CompletedTask; }));

            cases.Add(new TestCaseDescriptor(
                "ChatBuild", "ChatML_Tokens", "ChatML wraps turns and opens an assistant turn",
                ct =>
                {
                    string prompt = ChatPromptBuilder.Build(ChatFormatEnum.ChatML, Sample());
                    TestAssert.Contains(prompt, "<|im_start|>system");
                    TestAssert.Contains(prompt, "<|im_start|>user");
                    TestAssert.Contains(prompt, "<|im_end|>");
                    TestAssert.Contains(prompt, "<|im_start|>assistant");
                    return Task.CompletedTask;
                }));

            cases.Add(new TestCaseDescriptor(
                "ChatBuild", "Gemma_Markers", "Gemma uses turn markers and opens a model turn",
                ct =>
                {
                    string prompt = ChatPromptBuilder.Build(ChatFormatEnum.Gemma, Sample());
                    TestAssert.Contains(prompt, "<start_of_turn>user");
                    TestAssert.Contains(prompt, "<end_of_turn>");
                    TestAssert.Contains(prompt, "<start_of_turn>model");
                    return Task.CompletedTask;
                }));

            cases.Add(new TestCaseDescriptor(
                "ChatBuild", "Llama2_Inst", "Llama2 uses [INST] and <<SYS>>",
                ct =>
                {
                    string prompt = ChatPromptBuilder.Build(ChatFormatEnum.Llama2, Sample());
                    TestAssert.Contains(prompt, "[INST]");
                    TestAssert.Contains(prompt, "<<SYS>>");
                    return Task.CompletedTask;
                }));

            cases.Add(new TestCaseDescriptor(
                "ChatBuild", "Llama3_Headers", "Llama3 uses header ids and eot",
                ct =>
                {
                    string prompt = ChatPromptBuilder.Build(ChatFormatEnum.Llama3, Sample());
                    TestAssert.Contains(prompt, "<|begin_of_text|>");
                    TestAssert.Contains(prompt, "<|start_header_id|>");
                    TestAssert.Contains(prompt, "<|eot_id|>");
                    return Task.CompletedTask;
                }));

            cases.Add(new TestCaseDescriptor(
                "ChatBuild", "Alpaca_Markers", "Alpaca uses ### markers",
                ct =>
                {
                    string prompt = ChatPromptBuilder.Build(ChatFormatEnum.Alpaca, Sample());
                    TestAssert.Contains(prompt, "### Instruction:");
                    TestAssert.Contains(prompt, "### Response:");
                    return Task.CompletedTask;
                }));

            cases.Add(new TestCaseDescriptor(
                "ChatBuild", "Mistral_Inst", "Mistral uses [INST]",
                ct =>
                {
                    string prompt = ChatPromptBuilder.Build(ChatFormatEnum.Mistral, Sample());
                    TestAssert.Contains(prompt, "[INST]");
                    return Task.CompletedTask;
                }));

            cases.Add(new TestCaseDescriptor(
                "ChatBuild", "HumanAssistant", "HumanAssistant uses Human:/Assistant:",
                ct =>
                {
                    string prompt = ChatPromptBuilder.Build(ChatFormatEnum.HumanAssistant, Sample());
                    TestAssert.Contains(prompt, "Human:");
                    TestAssert.Contains(prompt, "Assistant:");
                    return Task.CompletedTask;
                }));

            cases.Add(new TestCaseDescriptor(
                "ChatBuild", "Zephyr_Tags", "Zephyr uses role tags",
                ct =>
                {
                    string prompt = ChatPromptBuilder.Build(ChatFormatEnum.Zephyr, Sample());
                    TestAssert.Contains(prompt, "<|user|>");
                    TestAssert.Contains(prompt, "<|assistant|>");
                    return Task.CompletedTask;
                }));

            cases.Add(new TestCaseDescriptor(
                "ChatBuild", "Phi_Markers", "Phi uses Instruct:/Output:",
                ct =>
                {
                    string prompt = ChatPromptBuilder.Build(ChatFormatEnum.Phi, Sample());
                    TestAssert.Contains(prompt, "Instruct:");
                    TestAssert.Contains(prompt, "Output:");
                    return Task.CompletedTask;
                }));

            cases.Add(new TestCaseDescriptor(
                "ChatBuild", "DeepSeek_Markers", "DeepSeek uses User:/Assistant:",
                ct =>
                {
                    string prompt = ChatPromptBuilder.Build(ChatFormatEnum.DeepSeek, Sample());
                    TestAssert.Contains(prompt, "User:");
                    TestAssert.Contains(prompt, "Assistant:");
                    return Task.CompletedTask;
                }));

            cases.Add(new TestCaseDescriptor(
                "ChatBuild", "CommandR_Tokens", "Command-R uses turn tokens",
                ct =>
                {
                    string prompt = ChatPromptBuilder.Build(ChatFormatEnum.CommandR, Sample());
                    TestAssert.Contains(prompt, "<|USER_TOKEN|>");
                    TestAssert.Contains(prompt, "<|CHATBOT_TOKEN|>");
                    return Task.CompletedTask;
                }));

            cases.Add(new TestCaseDescriptor(
                "ChatBuild", "Vicuna_Markers", "Vicuna uses USER:/ASSISTANT:",
                ct =>
                {
                    string prompt = ChatPromptBuilder.Build(ChatFormatEnum.Vicuna, Sample());
                    TestAssert.Contains(prompt, "USER:");
                    TestAssert.Contains(prompt, "ASSISTANT:");
                    return Task.CompletedTask;
                }));

            cases.Add(new TestCaseDescriptor(
                "ChatBuild", "StableLM_Tokens", "StableLM uses role tokens",
                ct =>
                {
                    string prompt = ChatPromptBuilder.Build(ChatFormatEnum.StableLM, Sample());
                    TestAssert.Contains(prompt, "<|USER|>");
                    TestAssert.Contains(prompt, "<|ASSISTANT|>");
                    return Task.CompletedTask;
                }));

            cases.Add(new TestCaseDescriptor(
                "ChatBuild", "Simple_Roles", "Simple uses role: content and opens assistant",
                ct =>
                {
                    string prompt = ChatPromptBuilder.Build(ChatFormatEnum.Simple, Sample());
                    TestAssert.Contains(prompt, "user: ");
                    TestAssert.Contains(prompt, "assistant:");
                    return Task.CompletedTask;
                }));

            return new TestSuiteDescriptor("ChatBuild", "Chat prompt builder", cases);
        }

        /// <summary>
        /// Suite covering <see cref="TextGenerationPromptBuilder"/> formats and helpers.
        /// </summary>
        /// <returns>Text generation prompt suite.</returns>
        public static TestSuiteDescriptor TextGenerationSuite()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();

            cases.Add(new TestCaseDescriptor(
                "TextGen", "Raw_Passthrough", "Raw format returns the input verbatim",
                ct => { TestAssert.Equal("hello world", TextGenerationPromptBuilder.Build(TextGenerationFormatEnum.Raw, "hello world")); return Task.CompletedTask; }));

            cases.Add(new TestCaseDescriptor(
                "TextGen", "Instruction_Markers", "Instruction format includes instruction and response markers",
                ct =>
                {
                    string prompt = TextGenerationPromptBuilder.Build(TextGenerationFormatEnum.Instruction, "Write a haiku");
                    TestAssert.Contains(prompt, "### Instruction:");
                    TestAssert.Contains(prompt, "### Response:");
                    return Task.CompletedTask;
                }));

            cases.Add(new TestCaseDescriptor(
                "TextGen", "QuestionAnswer_Markers", "Q&A format includes Question and Answer markers",
                ct =>
                {
                    string prompt = TextGenerationPromptBuilder.Build(TextGenerationFormatEnum.QuestionAnswer, "What causes rain?");
                    TestAssert.Contains(prompt, "Question:");
                    TestAssert.Contains(prompt, "Answer:");
                    return Task.CompletedTask;
                }));

            cases.Add(new TestCaseDescriptor(
                "TextGen", "CodeGeneration_Context", "Code generation includes provided context",
                ct =>
                {
                    Dictionary<string, string> context = new Dictionary<string, string>
                    {
                        { "language", "python" },
                        { "requirements", "Include error handling" }
                    };
                    string prompt = TextGenerationPromptBuilder.Build(TextGenerationFormatEnum.CodeGeneration, "Parse JSON", context);
                    TestAssert.Contains(prompt, "python");
                    return Task.CompletedTask;
                }));

            cases.Add(new TestCaseDescriptor(
                "TextGen", "WithExamples", "Few-shot builder embeds the examples",
                ct =>
                {
                    List<(string, string)> examples = new List<(string, string)>
                    {
                        ("2+2", "4"),
                        ("5*3", "15")
                    };
                    string prompt = TextGenerationPromptBuilder.BuildWithExamples(TextGenerationFormatEnum.QuestionAnswer, "7-3", examples);
                    TestAssert.Contains(prompt, "2+2");
                    TestAssert.Contains(prompt, "15");
                    return Task.CompletedTask;
                }));

            cases.Add(new TestCaseDescriptor(
                "TextGen", "CreateInstruction", "CreateInstruction wraps an instruction",
                ct =>
                {
                    string prompt = TextGenerationPromptBuilder.CreateInstruction("Do the thing");
                    TestAssert.Contains(prompt, "Do the thing");
                    return Task.CompletedTask;
                }));

            return new TestSuiteDescriptor("TextGen", "Text generation prompts", cases);
        }

        #endregion

        #region Private-Methods

        private static List<ChatMessage> Sample()
        {
            return new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = "You are concise." },
                new ChatMessage { Role = "user", Content = "What is the capital of France?" },
                new ChatMessage { Role = "assistant", Content = "Paris." },
                new ChatMessage { Role = "user", Content = "And its population?" }
            };
        }

        #endregion
    }
}
