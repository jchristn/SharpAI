namespace Test.Xunit
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using SharpAI.Helpers;
    using SharpAI.Prompts;
    using Xunit;

    /// <summary>Preserves hashing and few-shot prompt behavior when using named result objects.</summary>
    public sealed class NamedResultTests
    {
        /// <summary>Hash names retain their standard algorithm outputs and cover the entire seekable stream.</summary>
        [Fact]
        public void NamedHashesCoverTheEntireStream()
        {
            using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes("abc"));
            stream.Position = 2;
            HashResults result = HashHelper.ComputeAllHashes(stream);
            Assert.Equal("900150983CD24FB0D6963F7D28E17F72", Convert.ToHexString(result.MD5));
            Assert.Equal("A9993E364706816ABA3E25717850C26C9CD0D89D", Convert.ToHexString(result.SHA1));
            Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", Convert.ToHexString(result.SHA256));
        }

        /// <summary>Named examples retain input/output order and precede the final instruction.</summary>
        [Fact]
        public void NamedExamplesPrecedeTheRequestedInput()
        {
            List<PromptExample> examples = new List<PromptExample>
            {
                new PromptExample("first example input", "first example output"),
                new PromptExample("second example input", "second example output")
            };
            string prompt = TextGenerationPromptBuilder.BuildWithExamples(TextGenerationFormatEnum.Completion, "requested input", examples);
            Assert.True(prompt.IndexOf("first example input", StringComparison.Ordinal) < prompt.IndexOf("first example output", StringComparison.Ordinal));
            Assert.True(prompt.IndexOf("first example output", StringComparison.Ordinal) < prompt.IndexOf("second example input", StringComparison.Ordinal));
            Assert.True(prompt.IndexOf("second example input", StringComparison.Ordinal) < prompt.IndexOf("second example output", StringComparison.Ordinal));
            Assert.EndsWith("requested input", prompt);
        }
    }
}
