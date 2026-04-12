namespace SharpAI.Prompts
{
    /// <summary>
    /// Chat format helper.
    /// </summary>
    public static class ChatFormatHelper
    {
        /// <summary>
        /// Maps a model family name to its corresponding ChatFormat.
        /// </summary>
        /// <param name="modelFamily">The model family name (case-insensitive)</param>
        /// <returns>The appropriate ChatFormatEnum for the model family</returns>
        public static ChatFormatEnum ModelFamilyToChatFormat(string modelFamily)
        {
            if (string.IsNullOrWhiteSpace(modelFamily))
                return ChatFormatEnum.Simple;

            // Normalize the input to lowercase for case-insensitive comparison
            string normalizedFamily = modelFamily.ToLowerInvariant().Trim();

            // Check for specific model families and their variants
            return normalizedFamily switch
            {
                // Llama family
                "llama" or "llama2" or "llama-2" => ChatFormatEnum.Llama2,
                "llama3" or "llama-3" or "llama3.1" or "llama-3.1" or "llama3.2" or "llama-3.2"
                    or "llama3.3" or "llama-3.3" or "llama4" or "llama-4" => ChatFormatEnum.Llama3,

                // Alpaca and derivatives
                "alpaca" => ChatFormatEnum.Alpaca,
                "guanaco" or "wizardlm" => ChatFormatEnum.Alpaca,

                // Vicuna (has its own format, different from Alpaca)
                "vicuna" or "vicuna-v1.1" or "vicuna-13b" or "vicuna-7b" => ChatFormatEnum.Vicuna,

                // Mistral/Mixtral family
                "mistral" or "mixtral" or "mistral-7b" or "mixtral-8x7b" or "mistral-nemo" => ChatFormatEnum.Mistral,

                // Claude/Anthropic
                "claude" or "anthropic" or "claude-2" or "claude-3" or "claude-instant" => ChatFormatEnum.HumanAssistant,

                // OpenAI models
                "gpt" or "gpt-3.5" or "gpt-4" or "chatgpt" or "openai" or "gpt-4-turbo" => ChatFormatEnum.ChatML,

                // Microsoft Phi series
                "phi" or "phi-2" or "phi-3" or "phi-4" or "microsoft-phi" or "phi-3-mini" => ChatFormatEnum.Phi,

                // Zephyr
                "zephyr" or "huggingfaceh4" or "zephyr-7b" => ChatFormatEnum.Zephyr,

                // DeepSeek
                "deepseek" or "deepseek2" or "deepseek-coder" or "deepseek-chat" or "deepseek-llm" => ChatFormatEnum.DeepSeek,

                // Google Gemma
                "gemma" or "google-gemma" or "gemma-2b" or "gemma-7b" or "gemma2" or "gemma3" or "gemma3n" => ChatFormatEnum.Gemma,

                // Cohere Command-R
                "command-r" or "command-r-plus" or "cohere-command-r" => ChatFormatEnum.CommandR,

                // Older Cohere models use Simple format
                "command" or "cohere" or "command-light" or "command-xlarge" => ChatFormatEnum.Simple,

                // StableLM
                "stablelm" or "stability" or "stable-lm" or "stablelm-3b" or "stablelm-7b" => ChatFormatEnum.StableLM,

                // Models that typically use ChatML
                "qwen" or "qwen2" or "qwen2moe" or "qwen3" or "qwen3moe" or "qwen35"
                    or "qwen-7b" or "qwen-14b" or "qwen-72b" => ChatFormatEnum.ChatML,
                "yi" or "yi-6b" or "yi-34b" or "01-ai" => ChatFormatEnum.ChatML,
                "starling" or "openchat" or "openchat-3.5" => ChatFormatEnum.ChatML,
                "nous-hermes" or "nous-hermes-2" => ChatFormatEnum.ChatML,
                "dolphin" or "dolphin-mixtral" => ChatFormatEnum.ChatML,
                "smollm" or "smollm2" or "smollm3" => ChatFormatEnum.ChatML,

                // Orca models (Microsoft) typically use Alpaca-like format
                "orca" or "orca-2" or "microsoft-orca" => ChatFormatEnum.Alpaca,

                // Solar (Upstage) uses Alpaca-like format
                "solar" or "solar-10.7b" or "upstage-solar" => ChatFormatEnum.Alpaca,

                // Falcon typically uses simple format
                "falcon" or "falcon-7b" or "falcon-40b" or "falcon-180b" => ChatFormatEnum.Simple,

                // InternLM and Baichuan (Chinese models) - default to Simple for now
                "internlm" or "internlm-7b" or "internlm-20b" => ChatFormatEnum.Simple,
                "baichuan" or "baichuan-7b" or "baichuan-13b" => ChatFormatEnum.Simple,

                // RWKV uses simple format
                "rwkv" or "rwkv-4" or "rwkv-5" => ChatFormatEnum.Simple,

                // Dolly (Databricks) uses Alpaca-like format
                "dolly" or "dolly-v2" or "databricks-dolly" => ChatFormatEnum.Alpaca,

                // RedPajama uses simple format
                "redpajama" or "red-pajama" => ChatFormatEnum.Simple,

                // Pygmalion models (for roleplay) typically use simple or ChatML
                "pygmalion" or "pygmalion-7b" or "pygmalion-13b" => ChatFormatEnum.Simple,

                // Base models and others default to Simple
                _ => ChatFormatEnum.Simple
            };
        }

        /// <summary>
        /// Gets the ChatFormat for a model family with a fallback option.
        /// </summary>
        /// <param name="modelFamily">The model family name</param>
        /// <param name="fallback">The fallback format to use if model family is not recognized</param>
        /// <returns>The appropriate ChatFormatEnum or the fallback</returns>
        public static ChatFormatEnum ModelFamilyToChatFormat(string modelFamily, ChatFormatEnum fallback)
        {
            var format = ModelFamilyToChatFormat(modelFamily);

            // If we got Simple as a result and it wasn't explicitly mapped, use the fallback
            if (format == ChatFormatEnum.Simple && !IsExplicitlySimpleFormat(modelFamily))
            {
                return fallback;
            }

            return format;
        }

        /// <summary>
        /// Checks if a model family explicitly uses the Simple format.
        /// </summary>
        private static bool IsExplicitlySimpleFormat(string modelFamily)
        {
            if (string.IsNullOrWhiteSpace(modelFamily))
                return false;

            string normalizedFamily = modelFamily.ToLowerInvariant().Trim();

            return normalizedFamily switch
            {
                // Models that explicitly use Simple format
                "falcon" or "falcon-7b" or "falcon-40b" or "falcon-180b" => true,
                "command" or "cohere" or "command-light" or "command-xlarge" => true,
                "internlm" or "internlm-7b" or "internlm-20b" => true,
                "baichuan" or "baichuan-7b" or "baichuan-13b" => true,
                "rwkv" or "rwkv-4" or "rwkv-5" => true,
                "redpajama" or "red-pajama" => true,
                "pygmalion" or "pygmalion-7b" or "pygmalion-13b" => true,
                _ => false
            };
        }

        /// <summary>
        /// Returns the default stop sequences (anti-prompts) for a given chat format.
        /// These are the tokens that signal the model has finished its response.
        /// </summary>
        /// <param name="format">The chat format.</param>
        /// <returns>Array of stop sequence strings.</returns>
        public static string[] GetDefaultStopSequences(ChatFormatEnum format)
        {
            return format switch
            {
                ChatFormatEnum.ChatML => new[] { "<|im_end|>", "<|endoftext|>" },
                ChatFormatEnum.Llama2 => new[] { "</s>", "[INST]" },
                ChatFormatEnum.Llama3 => new[] { "<|eot_id|>", "<|end_of_text|>" },
                ChatFormatEnum.Alpaca => new[] { "### Instruction:", "### System:" },
                ChatFormatEnum.Mistral => new[] { "</s>", "[INST]" },
                ChatFormatEnum.HumanAssistant => new[] { "Human:", "\nHuman:" },
                ChatFormatEnum.Zephyr => new[] { "</s>", "<|user|>" },
                ChatFormatEnum.Phi => new[] { "Instruct:", "\nInstruct:" },
                ChatFormatEnum.DeepSeek => new[] { "User:", "\nUser:" },
                ChatFormatEnum.Gemma => new[] { "<end_of_turn>", "<start_of_turn>" },
                ChatFormatEnum.CommandR => new[] { "<|END_OF_TURN_TOKEN|>" },
                ChatFormatEnum.Vicuna => new[] { "USER:", "\nUSER:" },
                ChatFormatEnum.StableLM => new[] { "<|USER|>", "<|SYSTEM|>" },
                ChatFormatEnum.Simple => new[] { "user:", "User:", "\nuser:", "\nUser:" },
                _ => new[] { "user:", "User:", "\nuser:", "\nUser:" }
            };
        }

        /// <summary>
        /// Gets a description of the chat format.
        /// </summary>
        /// <param name="format">The chat format to describe</param>
        /// <returns>A human-readable description of the format</returns>
        public static string GetFormatDescription(ChatFormatEnum format)
        {
            return format switch
            {
                ChatFormatEnum.Simple => "Simple role-based format (role: content)",
                ChatFormatEnum.ChatML => "ChatML format with <|im_start|> and <|im_end|> tokens",
                ChatFormatEnum.Llama2 => "Llama 2 format with [INST] tags and <<SYS>> for system",
                ChatFormatEnum.Llama3 => "Llama 3 format with header IDs",
                ChatFormatEnum.Alpaca => "Alpaca format with ### markers",
                ChatFormatEnum.Mistral => "Mistral format with [INST] tags",
                ChatFormatEnum.HumanAssistant => "Human/Assistant conversational format",
                ChatFormatEnum.Zephyr => "Zephyr format with <|role|> tags",
                ChatFormatEnum.Phi => "Microsoft Phi format with Instruct/Output markers",
                ChatFormatEnum.DeepSeek => "DeepSeek format with User/Assistant markers",
                ChatFormatEnum.Gemma => "Google Gemma format with <start_of_turn> tokens",
                ChatFormatEnum.CommandR => "Cohere Command-R format with turn tokens",
                ChatFormatEnum.Vicuna => "Vicuna v1.1 format with USER/ASSISTANT markers",
                ChatFormatEnum.StableLM => "StableLM format with <|ROLE|> tokens",
                _ => "Unknown format"
            };
        }

        /// <summary>
        /// Determines if a format supports system messages.
        /// </summary>
        /// <param name="format">The chat format to check</param>
        /// <returns>True if the format supports system messages, false otherwise</returns>
        public static bool SupportsSystemMessage(ChatFormatEnum format)
        {
            return format switch
            {
                ChatFormatEnum.Simple => true,
                ChatFormatEnum.ChatML => true,
                ChatFormatEnum.Llama2 => true,
                ChatFormatEnum.Llama3 => true,
                ChatFormatEnum.Alpaca => true,
                ChatFormatEnum.Mistral => true,
                ChatFormatEnum.HumanAssistant => true,
                ChatFormatEnum.Zephyr => true,
                ChatFormatEnum.Phi => true,
                ChatFormatEnum.DeepSeek => true,
                ChatFormatEnum.Gemma => true, // Handled as first user turn
                ChatFormatEnum.CommandR => true,
                ChatFormatEnum.Vicuna => true, // Used as preamble
                ChatFormatEnum.StableLM => true,
                _ => false
            };
        }
    }
}