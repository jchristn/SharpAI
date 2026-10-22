namespace SharpAI.Prompts
{
    using System;

    /// <summary>An input and expected output used for few-shot prompting. Instances are immutable and safe to share between threads.</summary>
    public sealed class PromptExample
    {
        private readonly string _Input;
        private readonly string _Output;

        /// <summary>Create a prompt example.</summary>
        /// <param name="input">Non-null example input. Empty text is permitted.</param>
        /// <param name="output">Non-null expected output. Empty text is permitted.</param>
        /// <exception cref="ArgumentNullException">Input or output is null.</exception>
        public PromptExample(string input, string output)
        {
            _Input = input ?? throw new ArgumentNullException(nameof(input));
            _Output = output ?? throw new ArgumentNullException(nameof(output));
        }

        /// <summary>Gets the non-null example input.</summary>
        public string Input { get { return _Input; } }

        /// <summary>Gets the non-null expected output.</summary>
        public string Output { get { return _Output; } }
    }
}
