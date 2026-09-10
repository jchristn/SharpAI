namespace Test.Shared
{
    using System.Collections.Generic;

    using Touchstone.Core;

    /// <summary>
    /// Aggregates all SharpAI Touchstone suites so any runner (console, xUnit, NUnit) consumes them
    /// through a single entry point. The prompt/format/telemetry/tool suites are deterministic and require
    /// no model. The fixture-gated <see cref="ModelInferenceSuite"/> runs real inference only when a GGUF
    /// is provided (see <see cref="ModelFixture"/>) and skips cleanly otherwise, so CI stays green without
    /// a model present.
    /// </summary>
    public static class SharpAISuites
    {
        /// <summary>
        /// All registered suites, in execution order.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new List<TestSuiteDescriptor>
                {
                    PromptSuites.ChatFormatSuite(),
                    PromptSuites.ChatPromptBuilderSuite(),
                    PromptSuites.TextGenerationSuite(),
                    ThinkingFilterSuite.Build(),
                    IdGeneratorSuite.Build(),
                    ChatTemplateResolverSuite.Build(),
                    ToolCallParserSuite.Build(),
                    ToolCallingSuite.Build(),
                    JsonGrammarSuite.Build(),
                    TelemetrySuite.Build(),
                    AuthEvaluatorSuite.Build(),
                    RequestHistoryQuerySuite.Build(),
                    DatabaseSuite.Build(),
                    ModelPresetSuite.Build(),
                    AuthenticationEngineSuite.Build(),
                    RbacEngineSuite.Build(),
                    ReliabilitySuite.Build(),
                    ModelInferenceSuite.Build(),
                    ModelLifecycleSuite.Build()
                };
            }
        }
    }
}
