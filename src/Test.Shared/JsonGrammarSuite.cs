namespace Test.Shared
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using SharpAI.Grammars;
    using SharpAI.Models.OpenAI;

    using Touchstone.Core;

    /// <summary>
    /// Deterministic suite for <see cref="JsonGrammar"/> — the mapping from the OpenAI <c>response_format</c>
    /// and Ollama <c>format</c> request fields onto a JSON GBNF grammar. The grammar's runtime effect
    /// (constraining decoding to valid JSON) is exercised model-side by <see cref="ModelInferenceSuite"/>.
    /// </summary>
    public static class JsonGrammarSuite
    {
        #region Public-Methods

        /// <summary>
        /// Build the JSON grammar suite.
        /// </summary>
        /// <returns>JSON grammar suite.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor("JsonGrammar", "OpenAI_Null", "Null response_format yields no grammar",
                    ct =>
                    {
                        TestAssert.True(JsonGrammar.ForOpenAIResponseFormat(null) == null, "null response_format should not constrain");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("JsonGrammar", "OpenAI_Text", "response_format text yields no grammar",
                    ct =>
                    {
                        TestAssert.True(JsonGrammar.ForOpenAIResponseFormat(new OpenAIResponseFormat { Type = "text" }) == null, "text format should not constrain");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("JsonGrammar", "OpenAI_JsonObject", "response_format json_object yields the JSON grammar",
                    ct =>
                    {
                        string grammar = JsonGrammar.ForOpenAIResponseFormat(new OpenAIResponseFormat { Type = "json_object" });
                        TestAssert.True(!string.IsNullOrEmpty(grammar), "json_object should produce a grammar");
                        TestAssert.Contains(grammar, "root");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("JsonGrammar", "OpenAI_JsonSchema", "response_format json_schema yields the JSON grammar (schema fallback)",
                    ct =>
                    {
                        string grammar = JsonGrammar.ForOpenAIResponseFormat(new OpenAIResponseFormat { Type = "json_schema" });
                        TestAssert.True(!string.IsNullOrEmpty(grammar), "json_schema should produce a grammar");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("JsonGrammar", "Ollama_Json", "Ollama format 'json' yields the JSON grammar",
                    ct =>
                    {
                        TestAssert.True(!string.IsNullOrEmpty(JsonGrammar.ForOllamaFormat("json")), "format 'json' should produce a grammar");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("JsonGrammar", "Ollama_Empty", "Ollama empty/null format yields no grammar",
                    ct =>
                    {
                        TestAssert.True(JsonGrammar.ForOllamaFormat(null) == null, "null format should not constrain");
                        TestAssert.True(JsonGrammar.ForOllamaFormat("") == null, "empty format should not constrain");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("JsonGrammar", "Grammar_WellFormed", "The generic JSON grammar defines the root rule",
                    ct =>
                    {
                        TestAssert.Contains(JsonGrammar.GenericJson, "root   ::= object");
                        TestAssert.Equal("root", JsonGrammar.RootRule);
                        return Task.CompletedTask;
                    })
            };

            return new TestSuiteDescriptor("JsonGrammar", "JSON-mode grammar mapping", cases);
        }

        #endregion
    }
}
