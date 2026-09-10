namespace Test.Shared
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using SharpAI.Models.Ollama;
    using SharpAI.Models.OpenAI;
    using SharpAI.Tools;

    using Touchstone.Core;

    /// <summary>
    /// Touchstone suite for the model-independent tool-calling core beyond parsing: rendering tool
    /// definitions into a prompt instruction (<see cref="ToolPromptBuilder"/>) and mapping parsed tool calls
    /// onto the OpenAI/Ollama response shapes (<see cref="ToolResponseMapper"/>). End-to-end round-trips
    /// against a live tool-capable model are exercised separately once a model fixture is available.
    /// </summary>
    public static class ToolCallingSuite
    {
        #region Public-Methods

        /// <summary>
        /// Build the tool-calling suite.
        /// </summary>
        /// <returns>Tool-calling suite.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor("ToolCalling", "Instruction_Empty", "No tools yields an empty instruction",
                    ct =>
                    {
                        TestAssert.Equal(string.Empty, ToolPromptBuilder.BuildSystemInstruction(new List<ToolDefinition>()));
                        TestAssert.Equal(string.Empty, ToolPromptBuilder.BuildSystemInstruction(null));
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("ToolCalling", "Instruction_ListsTools", "The instruction lists tools and the call format",
                    ct =>
                    {
                        List<ToolDefinition> tools = new List<ToolDefinition>
                        {
                            new ToolDefinition("get_weather", "Get the weather for a city.", "{\"type\":\"object\",\"properties\":{\"city\":{\"type\":\"string\"}}}"),
                            new ToolDefinition("get_time", "Get the current time.", null)
                        };
                        string instruction = ToolPromptBuilder.BuildSystemInstruction(tools);
                        TestAssert.Contains(instruction, "<tool_call>");
                        TestAssert.Contains(instruction, "Available tools");
                        TestAssert.Contains(instruction, "get_weather");
                        TestAssert.Contains(instruction, "Get the weather for a city.");
                        TestAssert.Contains(instruction, "get_time");
                        TestAssert.Contains(instruction, "\"city\"");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("ToolCalling", "Map_OpenAI", "Parsed calls map to the OpenAI tool_calls shape",
                    ct =>
                    {
                        List<ParsedToolCall> calls = ToolCallParser.Parse(
                            "<tool_call>{\"name\":\"get_weather\",\"arguments\":{\"city\":\"Paris\"}}</tool_call>");
                        List<OpenAIToolCall> mapped = ToolResponseMapper.ToOpenAI(calls);
                        TestAssert.Equal(1, mapped.Count);
                        TestAssert.Equal("call_0", mapped[0].Id);
                        TestAssert.Equal("function", mapped[0].Type);
                        TestAssert.Equal("get_weather", mapped[0].Function.Name);
                        TestAssert.Contains(mapped[0].Function.Arguments, "Paris");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("ToolCalling", "Map_Ollama", "Parsed calls map to the Ollama tool-call shape",
                    ct =>
                    {
                        List<ParsedToolCall> calls = ToolCallParser.Parse(
                            "<tool_call>{\"name\":\"lookup\",\"arguments\":{\"id\":7}}</tool_call>");
                        List<OllamaToolCall> mapped = ToolResponseMapper.ToOllama(calls);
                        TestAssert.Equal(1, mapped.Count);
                        TestAssert.Equal("lookup", mapped[0].Function.Name);
                        TestAssert.Contains(mapped[0].Function.Arguments, "7");
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("ToolCalling", "RoundTrip_Multiple", "Parse then map preserves order and ids",
                    ct =>
                    {
                        List<ParsedToolCall> calls = ToolCallParser.Parse(
                            "<tool_call>{\"name\":\"a\",\"arguments\":{}}</tool_call>\n<tool_call>{\"name\":\"b\",\"arguments\":{}}</tool_call>");
                        List<OpenAIToolCall> mapped = ToolResponseMapper.ToOpenAI(calls);
                        TestAssert.Equal(2, mapped.Count);
                        TestAssert.Equal("call_0", mapped[0].Id);
                        TestAssert.Equal("a", mapped[0].Function.Name);
                        TestAssert.Equal("call_1", mapped[1].Id);
                        TestAssert.Equal("b", mapped[1].Function.Name);
                        return Task.CompletedTask;
                    })
            };

            return new TestSuiteDescriptor("ToolCalling", "Tool calling core (prompt + response mapping)", cases, null, null);
        }

        #endregion
    }
}
