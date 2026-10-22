namespace SharpAI.Prompts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Provides methods for building prompts in various chat formats used by different language models.
    /// </summary>
    public static class ChatPromptBuilder
    {
        /// <summary>
        /// Builds a formatted prompt string based on the specified chat format and messages.
        /// </summary>
        /// <param name="chatFormat">The chat format to use for formatting the messages.</param>
        /// <param name="messages">List of chat messages to format into a prompt.</param>
        /// <returns>A formatted prompt string ready for model inference.</returns>
        public static string Build(ChatFormatEnum chatFormat, List<ChatMessage> messages)
        {
            if (messages == null) return "";
            if (messages.Count == 0) return "";

            return chatFormat switch
            {
                ChatFormatEnum.Simple => BuildSimple(messages),
                ChatFormatEnum.ChatML => BuildChatML(messages),
                ChatFormatEnum.Llama2 => BuildLlama2(messages),
                ChatFormatEnum.Llama3 => BuildLlama3(messages),
                ChatFormatEnum.Alpaca => BuildAlpaca(messages),
                ChatFormatEnum.Mistral => BuildMistral(messages),
                ChatFormatEnum.HumanAssistant => BuildHumanAssistant(messages),
                ChatFormatEnum.Zephyr => BuildZephyr(messages),
                ChatFormatEnum.Phi => BuildPhi(messages),
                ChatFormatEnum.DeepSeek => BuildDeepSeek(messages),
                ChatFormatEnum.Gemma => BuildGemma(messages),
                ChatFormatEnum.CommandR => BuildCommandR(messages),
                ChatFormatEnum.Vicuna => BuildVicuna(messages),
                ChatFormatEnum.StableLM => BuildStableLM(messages),
                _ => BuildSimple(messages) // Default fallback
            };
        }

        private static string BuildSimple(List<ChatMessage> messages)
        {
            StringBuilder sb = new StringBuilder();
            foreach (ChatMessage message in messages)
            {
                sb.AppendLine($"{message.Role}: {message.Content}");
            }
            sb.Append("assistant:");
            return sb.ToString();
        }

        private static string BuildChatML(List<ChatMessage> messages)
        {
            StringBuilder sb = new StringBuilder();
            foreach (ChatMessage message in messages)
            {
                sb.AppendLine($"<|im_start|>{message.Role}");
                sb.Append(message.Content);
                sb.AppendLine("<|im_end|>");
            }
            sb.AppendLine("<|im_start|>assistant");
            return sb.ToString();
        }

        private static string BuildLlama2(List<ChatMessage> messages)
        {
            StringBuilder sb = new StringBuilder();
            ChatMessage systemMessage = messages.FirstOrDefault(m => m.Role.Equals("system", StringComparison.OrdinalIgnoreCase));
            List<ChatMessage> conversationMessages = messages.Where(m => !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase)).ToList();

            sb.Append("<s>[INST] ");

            if (systemMessage != null)
            {
                sb.AppendLine("<<SYS>>");
                sb.AppendLine(systemMessage.Content);
                sb.AppendLine("<</SYS>>");
                sb.AppendLine();
            }

            for (int i = 0; i < conversationMessages.Count; i++)
            {
                ChatMessage message = conversationMessages[i];

                if (message.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                {
                    if (i > 0) sb.Append("<s>[INST] ");
                    sb.Append(message.Content);
                    sb.Append(" [/INST]");
                }
                else if (message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append($" {message.Content} </s>");
                }
            }

            if (!conversationMessages.Last().Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(" ");
            }

            return sb.ToString();
        }

        private static string BuildLlama3(List<ChatMessage> messages)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<|begin_of_text|>");

            foreach (ChatMessage message in messages)
            {
                sb.Append($"<|start_header_id|>{message.Role}<|end_header_id|>");
                sb.AppendLine();
                sb.AppendLine();
                sb.Append(message.Content);
                sb.Append("<|eot_id|>");
            }

            sb.Append("<|start_header_id|>assistant<|end_header_id|>");
            sb.AppendLine();
            sb.AppendLine();

            return sb.ToString();
        }

        private static string BuildAlpaca(List<ChatMessage> messages)
        {
            StringBuilder sb = new StringBuilder();
            ChatMessage systemMessage = messages.FirstOrDefault(m => m.Role.Equals("system", StringComparison.OrdinalIgnoreCase));

            if (systemMessage != null)
            {
                sb.AppendLine("### System:");
                sb.AppendLine(systemMessage.Content);
                sb.AppendLine();
            }

            foreach (ChatMessage message in messages.Where(m => !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase)))
            {
                if (message.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine("### Instruction:");
                    sb.AppendLine(message.Content);
                    sb.AppendLine();
                }
                else if (message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine("### Response:");
                    sb.AppendLine(message.Content);
                    sb.AppendLine();
                }
            }

            sb.AppendLine("### Response:");
            return sb.ToString();
        }

        private static string BuildMistral(List<ChatMessage> messages)
        {
            StringBuilder sb = new StringBuilder();
            List<ChatMessage> conversationMessages = messages.Where(m => !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase)).ToList();

            // Mistral typically includes system message as part of first user message
            ChatMessage systemMessage = messages.FirstOrDefault(m => m.Role.Equals("system", StringComparison.OrdinalIgnoreCase));

            for (int i = 0; i < conversationMessages.Count; i++)
            {
                ChatMessage message = conversationMessages[i];

                if (message.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append("<s>[INST] ");

                    // Include system message with first user message
                    if (i == 0 && systemMessage != null)
                    {
                        sb.Append(systemMessage.Content);
                        sb.Append("\n\n");
                    }

                    sb.Append(message.Content);
                    sb.Append(" [/INST]");
                }
                else if (message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append($" {message.Content}</s>");
                }
            }

            if (!conversationMessages.Last().Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(" ");
            }

            return sb.ToString();
        }

        private static string BuildHumanAssistant(List<ChatMessage> messages)
        {
            StringBuilder sb = new StringBuilder();

            foreach (ChatMessage message in messages)
            {
                if (message.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"System: {message.Content}");
                    sb.AppendLine();
                }
                else if (message.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"Human: {message.Content}");
                    sb.AppendLine();
                }
                else if (message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"Assistant: {message.Content}");
                    sb.AppendLine();
                }
            }

            sb.Append("Assistant:");
            return sb.ToString();
        }

        private static string BuildZephyr(List<ChatMessage> messages)
        {
            StringBuilder sb = new StringBuilder();

            foreach (ChatMessage message in messages)
            {
                sb.AppendLine($"<|{message.Role}|>");
                sb.Append(message.Content);
                sb.AppendLine("</s>");
            }

            sb.AppendLine("<|assistant|>");
            return sb.ToString();
        }

        private static string BuildPhi(List<ChatMessage> messages)
        {
            StringBuilder sb = new StringBuilder();
            ChatMessage systemMessage = messages.FirstOrDefault(m => m.Role.Equals("system", StringComparison.OrdinalIgnoreCase));

            if (systemMessage != null)
            {
                sb.AppendLine($"System: {systemMessage.Content}");
                sb.AppendLine();
            }

            foreach (ChatMessage message in messages.Where(m => !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase)))
            {
                if (message.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"Instruct: {message.Content}");
                }
                else if (message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"Output: {message.Content}");
                }
            }

            sb.Append("Output: ");
            return sb.ToString();
        }

        private static string BuildDeepSeek(List<ChatMessage> messages)
        {
            StringBuilder sb = new StringBuilder();

            foreach (ChatMessage message in messages)
            {
                if (message.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"System: {message.Content}");
                    sb.AppendLine();
                }
                else if (message.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"User: {message.Content}");
                    sb.AppendLine();
                }
                else if (message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"Assistant: {message.Content}");
                    sb.AppendLine();
                }
            }

            sb.Append("Assistant: ");
            return sb.ToString();
        }

        private static string BuildGemma(List<ChatMessage> messages)
        {
            StringBuilder sb = new StringBuilder();

            foreach (ChatMessage message in messages)
            {
                if (message.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                {
                    // Gemma typically includes system message as first user turn
                    sb.AppendLine("<start_of_turn>user");
                    sb.Append($"System: {message.Content}");
                    sb.AppendLine("<end_of_turn>");
                    sb.AppendLine("<start_of_turn>model");
                    sb.Append("Understood.");
                    sb.AppendLine("<end_of_turn>");
                }
                else if (message.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine("<start_of_turn>user");
                    sb.Append(message.Content);
                    sb.AppendLine("<end_of_turn>");
                }
                else if (message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine("<start_of_turn>model");
                    sb.Append(message.Content);
                    sb.AppendLine("<end_of_turn>");
                }
            }

            sb.AppendLine("<start_of_turn>model");
            return sb.ToString();
        }

        private static string BuildCommandR(List<ChatMessage> messages)
        {
            StringBuilder sb = new StringBuilder();
            ChatMessage systemMessage = messages.FirstOrDefault(m => m.Role.Equals("system", StringComparison.OrdinalIgnoreCase));

            sb.Append("<BOS_TOKEN>");

            if (systemMessage != null)
            {
                sb.Append("<|START_OF_TURN_TOKEN|><|SYSTEM_TOKEN|>");
                sb.Append(systemMessage.Content);
                sb.Append("<|END_OF_TURN_TOKEN|>");
            }

            foreach (ChatMessage message in messages.Where(m => !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase)))
            {
                if (message.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append("<|START_OF_TURN_TOKEN|><|USER_TOKEN|>");
                    sb.Append(message.Content);
                    sb.Append("<|END_OF_TURN_TOKEN|>");
                }
                else if (message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append("<|START_OF_TURN_TOKEN|><|CHATBOT_TOKEN|>");
                    sb.Append(message.Content);
                    sb.Append("<|END_OF_TURN_TOKEN|>");
                }
            }

            sb.Append("<|START_OF_TURN_TOKEN|><|CHATBOT_TOKEN|>");
            return sb.ToString();
        }

        private static string BuildVicuna(List<ChatMessage> messages)
        {
            StringBuilder sb = new StringBuilder();
            ChatMessage systemMessage = messages.FirstOrDefault(m => m.Role.Equals("system", StringComparison.OrdinalIgnoreCase));

            // Vicuna v1.1 format starts with a preamble
            if (systemMessage != null)
            {
                sb.AppendLine(systemMessage.Content);
            }
            else
            {
                sb.AppendLine("A chat between a user and an assistant.");
            }
            sb.AppendLine();

            foreach (ChatMessage message in messages.Where(m => !m.Role.Equals("system", StringComparison.OrdinalIgnoreCase)))
            {
                if (message.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"USER: {message.Content}");
                }
                else if (message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"ASSISTANT: {message.Content}");
                }
            }

            sb.Append("ASSISTANT:");
            return sb.ToString();
        }

        private static string BuildStableLM(List<ChatMessage> messages)
        {
            StringBuilder sb = new StringBuilder();

            foreach (ChatMessage message in messages)
            {
                if (message.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append("<|SYSTEM|>");
                    sb.AppendLine(message.Content);
                }
                else if (message.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append("<|USER|>");
                    sb.AppendLine(message.Content);
                }
                else if (message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append("<|ASSISTANT|>");
                    sb.AppendLine(message.Content);
                }
            }

            sb.Append("<|ASSISTANT|>");
            return sb.ToString();
        }
    }
}