namespace Test.Shared
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using SharpAI.Tools;

    using Touchstone.Core;

    /// <summary>
    /// Touchstone suite for <see cref="ToolCallParser"/>, covering the tool-call output formats emitted by
    /// tool-capable models. This is the model-independent, deterministic core of tool calling; end-to-end
    /// round-trips against a live model are exercised separately once a model fixture is available.
    /// </summary>
    public static class ToolCallParserSuite
    {
        #region Public-Methods

        /// <summary>
        /// Build the tool-call parser suite.
        /// </summary>
        /// <returns>Tool-call parser suite.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor("ToolParse", "Single_Block", "Parses a single <tool_call> block",
                    ct =>
                    {
                        List<ParsedToolCall> calls = ToolCallParser.Parse(
                            "<tool_call>{\"name\": \"get_weather\", \"arguments\": {\"city\": \"Paris\"}}</tool_call>");
                        TestAssert.Equal(1, calls.Count);
                        TestAssert.Equal("get_weather", calls[0].Name);
                        TestAssert.Contains(calls[0].ArgumentsJson, "Paris");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("ToolParse", "Multiple_Blocks", "Parses multiple <tool_call> blocks",
                    ct =>
                    {
                        List<ParsedToolCall> calls = ToolCallParser.Parse(
                            "<tool_call>{\"name\":\"a\",\"arguments\":{}}</tool_call>\n<tool_call>{\"name\":\"b\",\"arguments\":{}}</tool_call>");
                        TestAssert.Equal(2, calls.Count);
                        TestAssert.Equal("a", calls[0].Name);
                        TestAssert.Equal("b", calls[1].Name);
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("ToolParse", "Bare_Object", "Parses a bare JSON object",
                    ct =>
                    {
                        List<ParsedToolCall> calls = ToolCallParser.Parse("{\"name\":\"search\",\"arguments\":{\"q\":\"cats\"}}");
                        TestAssert.Equal(1, calls.Count);
                        TestAssert.Equal("search", calls[0].Name);
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("ToolParse", "Json_Array", "Parses a JSON array of calls",
                    ct =>
                    {
                        List<ParsedToolCall> calls = ToolCallParser.Parse("[{\"name\":\"x\",\"arguments\":{}},{\"name\":\"y\",\"arguments\":{}}]");
                        TestAssert.Equal(2, calls.Count);
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("ToolParse", "Nested_Function", "Parses the {function:{name,arguments}} shape",
                    ct =>
                    {
                        List<ParsedToolCall> calls = ToolCallParser.Parse("{\"function\":{\"name\":\"fn\",\"arguments\":{\"a\":1}}}");
                        TestAssert.Equal(1, calls.Count);
                        TestAssert.Equal("fn", calls[0].Name);
                        TestAssert.Contains(calls[0].ArgumentsJson, "a");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("ToolParse", "Plain_Text_Empty", "Plain text yields no tool calls",
                    ct =>
                    {
                        List<ParsedToolCall> calls = ToolCallParser.Parse("The weather in Paris is sunny.");
                        TestAssert.Equal(0, calls.Count);
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("ToolParse", "Unclosed_Tag", "Parses an unclosed <tool_call> (small models omit the closing tag)",
                    ct =>
                    {
                        // Observed from Qwen2.5-1.5B: it emits the opening tag + JSON but no </tool_call>.
                        List<ParsedToolCall> calls = ToolCallParser.Parse(
                            "<tool_call>{\"name\": \"get_weather\", \"arguments\": {\"city\": \"Paris\"}}");
                        TestAssert.Equal(1, calls.Count);
                        TestAssert.Equal("get_weather", calls[0].Name);
                        TestAssert.Contains(calls[0].ArgumentsJson, "Paris");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("ToolParse", "Nested_Braces_In_Args", "Balanced-brace extraction handles nested objects",
                    ct =>
                    {
                        List<ParsedToolCall> calls = ToolCallParser.Parse(
                            "<tool_call>{\"name\":\"f\",\"arguments\":{\"nested\":{\"a\":{\"b\":1}},\"s\":\"}not-a-close\"}}");
                        TestAssert.Equal(1, calls.Count);
                        TestAssert.Equal("f", calls[0].Name);
                        TestAssert.Contains(calls[0].ArgumentsJson, "nested");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("ToolParse", "Malformed_Skipped", "Malformed JSON in a block is skipped",
                    ct =>
                    {
                        List<ParsedToolCall> calls = ToolCallParser.Parse("<tool_call>{not valid json}</tool_call>");
                        TestAssert.Equal(0, calls.Count);
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("ToolParse", "Empty_Input", "Null or empty input yields no calls",
                    ct =>
                    {
                        TestAssert.Equal(0, ToolCallParser.Parse(null).Count);
                        TestAssert.Equal(0, ToolCallParser.Parse("   ").Count);
                        return Task.CompletedTask;
                    })
            };

            return new TestSuiteDescriptor("ToolParse", "Tool-call output parser", cases);
        }

        #endregion
    }
}
