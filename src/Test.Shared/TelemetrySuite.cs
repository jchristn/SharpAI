namespace Test.Shared
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;

    using SharpAI.Telemetry;

    using Touchstone.Core;

    /// <summary>
    /// Touchstone suite for <see cref="SharpAITelemetry"/>. Telemetry must be a safe no-op when no listener
    /// is attached: recording, spans, and the resident-model provider must never throw and must not require
    /// a running exporter. These cases run without any telemetry host present.
    /// </summary>
    public static class TelemetrySuite
    {
        #region Public-Methods

        /// <summary>
        /// Build the telemetry suite.
        /// </summary>
        /// <returns>Telemetry suite.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor("Telemetry", "Names_Stable", "Instrument source names are the stable contract",
                    ct =>
                    {
                        TestAssert.Equal("SharpAI.Inference", SharpAITelemetry.MeterName);
                        TestAssert.Equal("SharpAI.Models", SharpAITelemetry.ModelsMeterName);
                        TestAssert.Equal("SharpAI.Inference", SharpAITelemetry.ActivitySourceName);
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Telemetry", "Record_NoListener", "Recording without a listener does not throw",
                    ct =>
                    {
                        SharpAITelemetry.RecordInference("chat", "test-model", 0.25, 42, true);
                        SharpAITelemetry.RecordInference("completion", null, 0.0, 0, false);
                        SharpAITelemetry.RecordInference("embedding", "m", -1.0, -5, true);
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Telemetry", "Span_NullSafe", "Starting a span without a listener is null-safe",
                    ct =>
                    {
                        Activity? activity = SharpAITelemetry.StartInference("chat", "test-model");
                        // With no ActivityListener registered, StartActivity returns null; disposing null is not
                        // our concern, but the call itself must not throw.
                        activity?.Dispose();
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Telemetry", "Span_Produced_WithListener", "A span is produced and tagged when a listener samples",
                    ct =>
                    {
                        Activity started = null;
                        using (ActivityListener listener = new ActivityListener())
                        {
                            listener.ShouldListenTo = source => source.Name == SharpAITelemetry.ActivitySourceName;
                            listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
                            listener.ActivityStarted = a => started = a;
                            ActivitySource.AddActivityListener(listener);

                            using (Activity activity = SharpAITelemetry.StartInference("chat", "unit-model"))
                            {
                                TestAssert.True(activity != null, "an activity should be created when a listener samples");
                            }
                        }

                        TestAssert.True(started != null, "the listener should have observed the started activity");
                        TestAssert.Equal("inference.chat", started!.OperationName);
                        TestAssert.Equal("chat", started.GetTagItem("sharpai.operation") as string);
                        TestAssert.Equal("unit-model", started.GetTagItem("sharpai.model") as string);
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("Telemetry", "Provider_AcceptsAndIgnoresNull", "Resident-model provider accepts a value and ignores null",
                    ct =>
                    {
                        SharpAITelemetry.SetResidentModelCountProvider(() => 3);
                        SharpAITelemetry.SetResidentModelCountProvider(null);
                        return Task.CompletedTask;
                    })
            };

            return new TestSuiteDescriptor("Telemetry", "Telemetry no-op safety", cases);
        }

        #endregion
    }
}
