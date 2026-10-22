namespace SharpAI.Prompts
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Provides methods for building text generation prompts in various formats.
    /// </summary>
    public static class TextGenerationPromptBuilder
    {
        /// <summary>
        /// Builds a formatted prompt for text generation based on the specified format.
        /// </summary>
        /// <param name="format">The text generation format to use.</param>
        /// <param name="input">The main input text or instruction.</param>
        /// <param name="context">Optional context parameters specific to the format.</param>
        /// <returns>A formatted prompt string ready for text generation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when input is null.</exception>
        /// <exception cref="ArgumentException">Thrown when input is empty or whitespace.</exception>
        public static string Build(TextGenerationFormatEnum format, string input, Dictionary<string, string> context = null)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Input cannot be empty or whitespace.", nameof(input));

            return format switch
            {
                TextGenerationFormatEnum.Raw => input,
                TextGenerationFormatEnum.Completion => BuildCompletion(input),
                TextGenerationFormatEnum.Instruction => BuildInstruction(input),
                TextGenerationFormatEnum.QuestionAnswer => BuildQuestionAnswer(input),
                TextGenerationFormatEnum.CreativeWriting => BuildCreativeWriting(input, context),
                TextGenerationFormatEnum.CodeGeneration => BuildCodeGeneration(input, context),
                TextGenerationFormatEnum.Academic => BuildAcademic(input, context),
                TextGenerationFormatEnum.ListGeneration => BuildListGeneration(input, context),
                TextGenerationFormatEnum.TemplateFilling => BuildTemplateFilling(input),
                TextGenerationFormatEnum.Dialogue => BuildDialogue(input, context),
                _ => input // Default fallback
            };
        }

        /// <summary>
        /// Builds a prompt with additional context for few-shot learning.
        /// </summary>
        /// <param name="format">The text generation format to use.</param>
        /// <param name="input">The main input text or instruction.</param>
        /// <param name="examples">List of example input-output pairs for few-shot learning.</param>
        /// <param name="context">Optional context parameters.</param>
        /// <returns>A formatted prompt with examples.</returns>
        public static string BuildWithExamples(
            TextGenerationFormatEnum format,
            string input,
            List<PromptExample> examples,
            Dictionary<string, string> context = null)
        {
            if (examples == null || examples.Count == 0)
                return Build(format, input, context);

            StringBuilder sb = new StringBuilder();

            // Add examples first
            sb.AppendLine("Examples:");
            sb.AppendLine();

            foreach (PromptExample example in examples)
            {
                sb.AppendLine(Build(format, example.Input, context));
                sb.AppendLine(example.Output);
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
            }

            // Add the actual prompt
            sb.AppendLine("Now complete the following:");
            sb.AppendLine();
            sb.Append(Build(format, input, context));

            return sb.ToString();
        }

        private static string BuildCompletion(string input)
        {
            // For completion, we assume the input ends where the model should continue
            return input;
        }

        private static string BuildInstruction(string input)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("### Instruction:");
            sb.AppendLine(input);
            sb.AppendLine();
            sb.AppendLine("### Response:");
            return sb.ToString();
        }

        private static string BuildQuestionAnswer(string input)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Question: {input}");
            sb.AppendLine();
            sb.Append("Answer:");
            return sb.ToString();
        }

        private static string BuildCreativeWriting(string input, Dictionary<string, string> context)
        {
            StringBuilder sb = new StringBuilder();

            if (context != null)
            {
                if (context.TryGetValue("genre", out string genre))
                    sb.AppendLine($"Genre: {genre}");

                if (context.TryGetValue("style", out string style))
                    sb.AppendLine($"Style: {style}");

                if (context.TryGetValue("length", out string length))
                    sb.AppendLine($"Length: {length}");
            }

            sb.AppendLine($"Topic: {input}");
            sb.AppendLine();
            sb.Append("Story:");
            return sb.ToString();
        }

        private static string BuildCodeGeneration(string input, Dictionary<string, string> context)
        {
            StringBuilder sb = new StringBuilder();

            string language = context?.GetValueOrDefault("language", "python") ?? "python";

            sb.AppendLine($"Language: {language}");
            sb.AppendLine($"Task: {input}");

            if (context != null)
            {
                if (context.TryGetValue("requirements", out string requirements))
                    sb.AppendLine($"Requirements: {requirements}");

                if (context.TryGetValue("constraints", out string constraints))
                    sb.AppendLine($"Constraints: {constraints}");
            }

            sb.AppendLine();
            sb.Append($"```{language.ToLower()}");
            return sb.ToString();
        }

        private static string BuildAcademic(string input, Dictionary<string, string> context)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"Title: {input}");

            if (context != null)
            {
                if (context.TryGetValue("type", out string type))
                    sb.AppendLine($"Type: {type}");

                if (context.TryGetValue("field", out string field))
                    sb.AppendLine($"Field: {field}");

                if (context.TryGetValue("audience", out string audience))
                    sb.AppendLine($"Audience: {audience}");
            }

            sb.AppendLine();

            string section = context?.GetValueOrDefault("section", "Abstract") ?? "Abstract";
            sb.Append($"{section}:");

            return sb.ToString();
        }

        private static string BuildListGeneration(string input, Dictionary<string, string> context)
        {
            StringBuilder sb = new StringBuilder();

            string count = context?.GetValueOrDefault("count", "10") ?? "10";
            string format = context?.GetValueOrDefault("format", "numbered") ?? "numbered";

            sb.AppendLine($"Create a list of {count} {input}:");
            sb.AppendLine();

            if (format.Equals("numbered", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append("1.");
            }
            else if (format.Equals("bullet", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append("•");
            }
            else
            {
                sb.Append("-");
            }

            return sb.ToString();
        }

        private static string BuildTemplateFilling(string input)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Complete the following template:");
            sb.AppendLine();
            sb.Append(input);
            return sb.ToString();
        }

        private static string BuildDialogue(string input, Dictionary<string, string> context)
        {
            StringBuilder sb = new StringBuilder();

            if (context != null)
            {
                if (context.TryGetValue("characters", out string characters))
                    sb.AppendLine($"Characters: {characters}");

                if (context.TryGetValue("setting", out string setting))
                    sb.AppendLine($"Setting: {setting}");

                if (context.TryGetValue("tone", out string tone))
                    sb.AppendLine($"Tone: {tone}");
            }

            sb.AppendLine($"Topic: {input}");
            sb.AppendLine();

            string firstCharacter = context?.GetValueOrDefault("firstCharacter", "Character 1") ?? "Character 1";
            sb.Append($"{firstCharacter}:");

            return sb.ToString();
        }

        /// <summary>
        /// Creates a simple continuation prompt from the given text.
        /// </summary>
        /// <param name="text">The text to continue from.</param>
        /// <returns>The text formatted for continuation.</returns>
        public static string CreateContinuation(string text)
        {
            return Build(TextGenerationFormatEnum.Completion, text);
        }

        /// <summary>
        /// Creates an instruction prompt with optional system context.
        /// </summary>
        /// <param name="instruction">The instruction to follow.</param>
        /// <param name="systemContext">Optional system context to prepend.</param>
        /// <returns>A formatted instruction prompt.</returns>
        public static string CreateInstruction(string instruction, string systemContext = null)
        {
            if (string.IsNullOrWhiteSpace(systemContext))
                return Build(TextGenerationFormatEnum.Instruction, instruction);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Context: {systemContext}");
            sb.AppendLine();
            sb.Append(Build(TextGenerationFormatEnum.Instruction, instruction));

            return sb.ToString();
        }
    }
}
