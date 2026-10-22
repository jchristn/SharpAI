namespace SharpAI.Telemetry
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using SharpAI.Operations;

    /// <summary>
    /// Central telemetry surface for the SharpAI core library. Instruments are emitted through the .NET
    /// BCL (<see cref="Meter"/> and <see cref="ActivitySource"/>) so the core carries no dependency on any
    /// telemetry host or exporter. A host process (for example the server, via Radiant) subscribes to the
    /// meter and activity source by name; when nothing subscribes, recording is a near-zero-cost no-op.
    ///
    /// Metric names follow OpenTelemetry conventions; after Prometheus suffixing they render as
    /// <c>sharpai_inference_requests_total</c>, <c>sharpai_inference_tokens_generated_total</c>,
    /// <c>sharpai_inference_latency_seconds</c>, and <c>sharpai_models_resident</c>.
    /// </summary>
    public static class SharpAITelemetry
    {
        #region Public-Members

        /// <summary>
        /// Name of the inference meter and activity source. Subscribe to this name to receive inference
        /// metrics and spans. This is a stable public contract.
        /// </summary>
        public const string MeterName = "SharpAI.Inference";

        /// <summary>
        /// Name of the models meter, which emits gauges about loaded models. Stable public contract.
        /// </summary>
        public const string ModelsMeterName = "SharpAI.Models";

        /// <summary>
        /// Name of the inference activity (trace) source. Stable public contract.
        /// </summary>
        public const string ActivitySourceName = "SharpAI.Inference";

        /// <summary>
        /// Activity source used to create inference spans. Never null.
        /// </summary>
        public static ActivitySource Source
        {
            get { return _Source; }
        }

        #endregion

        #region Private-Members

        private static readonly Meter _Meter = new Meter(MeterName);
        private static readonly Meter _ModelsMeter = new Meter(ModelsMeterName);
        private static readonly ActivitySource _Source = new ActivitySource(ActivitySourceName);

        private static readonly Counter<long> _Requests =
            _Meter.CreateCounter<long>("sharpai.inference.requests", "{request}", "Inference requests processed.");
        private static readonly Counter<long> _Tokens =
            _Meter.CreateCounter<long>("sharpai.inference.tokens_generated", "{token}", "Tokens generated during inference.");
        private static readonly Histogram<double> _Latency =
            _Meter.CreateHistogram<double>("sharpai.inference.latency", "s", "End-to-end inference latency in seconds.");

        private static readonly Histogram<double> _Load = _ModelsMeter.CreateHistogram<double>("sharpai.models.load_duration", "s");
        private static readonly Histogram<double> _Queue = _Meter.CreateHistogram<double>("sharpai.inference.queue_wait", "s");
        private static readonly Histogram<double> _First = _Meter.CreateHistogram<double>("sharpai.inference.time_to_first_token", "s");
        private static readonly Histogram<double> _Prompt = _Meter.CreateHistogram<double>("sharpai.inference.prompt_eval_duration", "s");
        private static readonly Histogram<double> _Generation = _Meter.CreateHistogram<double>("sharpai.inference.generation_duration", "s");
        private static readonly Histogram<double> _PromptRate = _Meter.CreateHistogram<double>("sharpai.inference.prompt_tokens_per_second", "{token}/s");
        private static readonly Histogram<double> _GenerationRate = _Meter.CreateHistogram<double>("sharpai.inference.generation_tokens_per_second", "{token}/s");
        private static readonly Counter<long> _Cancelled = _Meter.CreateCounter<long>("sharpai.inference.cancellations", "{request}");
        private static readonly ConcurrentDictionary<string, long> _Busy = new ConcurrentDictionary<string, long>();
        private static readonly ConcurrentDictionary<string, long> _Slots = new ConcurrentDictionary<string, long>();

        private static Func<int> _ResidentModelProvider = () => 0;

        #endregion

        #region Constructors-and-Factories

        static SharpAITelemetry()
        {
            _Meter.CreateObservableGauge<long>("sharpai.inference.busy_slots", () => ObserveSlots(_Busy), "{slot}");
            _Meter.CreateObservableGauge<long>("sharpai.inference.total_slots", () => ObserveSlots(_Slots), "{slot}");
            _ModelsMeter.CreateObservableGauge<long>(
                "sharpai.models.resident",
                ObserveResidentModels,
                "{model}",
                "Number of models currently resident in memory.");
        }

        #endregion

        #region Public-Methods

        /// <summary>Record cold initialization and configured slot capacity.</summary>
        /// <param name="model">Model label.</param><param name="seconds">Load duration.</param><param name="slots">Configured slots.</param>
        public static void RecordLoad(string model, double seconds, int slots)
        {
            string label = model ?? "unknown";
            _Slots[label] = slots;
            _Cancelled.Add(0, new KeyValuePair<string, object>("model", label));
            _Load.Record(Math.Max(0, seconds), new KeyValuePair<string, object>("model", label));
        }

        /// <summary>Update occupied slots after acquisition or release.</summary>
        /// <param name="model">Model label.</param><param name="delta">Positive or negative change.</param>
        public static void ChangeBusySlots(string model, int delta)
        {
            _Busy.AddOrUpdate(model ?? "unknown", Math.Max(0, delta), (key, count) => Math.Max(0, count + delta));
        }

        /// <summary>Remove an unloaded model from slot gauges.</summary>
        /// <param name="model">Model label.</param>
        public static void RemoveSlots(string model)
        {
            _Slots.TryRemove(model ?? "unknown", out _);
            _Busy.TryRemove(model ?? "unknown", out _);
        }

        /// <summary>Record per-call phase timings. No prompt or response text is retained.</summary>
        /// <param name="model">Model label.</param><param name="statistics">Measured request statistics.</param>
        public static void RecordDetails(string model, InferenceStatistics statistics)
        {
            ArgumentNullException.ThrowIfNull(statistics);
            KeyValuePair<string, object> tag = new KeyValuePair<string, object>("model", model ?? "unknown");
            _Queue.Record(statistics.QueueDurationNanoseconds / 1e9, tag);
            if (statistics.FirstTokenDurationNanoseconds > 0) _First.Record(statistics.FirstTokenDurationNanoseconds / 1e9, tag);
            KeyValuePair<string, object> timing = new KeyValuePair<string, object>("timing_source", statistics.PhaseTimingsApproximate ? "backend_step_wall_clock" : "native");
            if (statistics.PromptDurationNanoseconds > 0) _Prompt.Record(statistics.PromptDurationNanoseconds / 1e9, tag, timing);
            if (statistics.GenerationDurationNanoseconds > 0) _Generation.Record(statistics.GenerationDurationNanoseconds / 1e9, tag, timing);
            if (statistics.PromptDurationNanoseconds > 0) _PromptRate.Record(statistics.PromptTokens * 1e9 / statistics.PromptDurationNanoseconds, tag, timing);
            if (statistics.GenerationDurationNanoseconds > 0) _GenerationRate.Record(statistics.CompletionTokens * 1e9 / statistics.GenerationDurationNanoseconds, tag, timing);
            if (statistics.Cancelled) _Cancelled.Add(1, tag);
        }

        /// <summary>
        /// Register a callback that reports the number of models currently resident in memory. The most
        /// recent non-null provider wins. Passing null is ignored.
        /// </summary>
        /// <param name="provider">Callback returning the resident model count.</param>
        public static void SetResidentModelCountProvider(Func<int> provider)
        {
            if (provider != null) _ResidentModelProvider = provider;
        }

        /// <summary>
        /// Start an inference span. The returned activity may be null when no trace listener is active;
        /// callers must null-check or use it inside a <c>using</c> that tolerates null.
        /// </summary>
        /// <param name="operation">Operation name, for example "chat" or "completion".</param>
        /// <param name="model">Model identifier, or null.</param>
        /// <returns>Started activity, or null when nothing is sampling.</returns>
        public static Activity StartInference(string operation, string model)
        {
            Activity activity = _Source.StartActivity("inference." + (operation ?? "unknown"), ActivityKind.Internal);
            if (activity != null)
            {
                activity.SetTag("sharpai.operation", operation ?? "unknown");
                activity.SetTag("sharpai.model", model ?? "unknown");
            }
            return activity;
        }

        /// <summary>Attach measured engine phases and outcome to the sampled inference span.</summary>
        /// <param name="activity">Optional sampled activity.</param><param name="statistics">Request measurements.</param><param name="success">Whether generation completed.</param>
        public static void CompleteInference(Activity activity, InferenceStatistics statistics, bool success)
        {
            ArgumentNullException.ThrowIfNull(statistics);
            if (activity == null) return;
            activity.SetTag("sharpai.prompt_tokens", statistics.PromptTokens);
            activity.SetTag("sharpai.completion_tokens", statistics.CompletionTokens);
            activity.SetTag("sharpai.queue_wait_ns", statistics.QueueDurationNanoseconds);
            activity.SetTag("sharpai.ttft_ns", statistics.FirstTokenDurationNanoseconds);
            activity.SetTag("sharpai.prompt_eval_ns", statistics.PromptDurationNanoseconds);
            activity.SetTag("sharpai.generation_ns", statistics.GenerationDurationNanoseconds);
            activity.SetTag("sharpai.timing_source", statistics.PhaseTimingsApproximate ? "backend_step_wall_clock" : "native");
            activity.SetTag("sharpai.cancelled", statistics.Cancelled);
            activity.SetTag("sharpai.outcome", success ? "ok" : statistics.Cancelled ? "cancelled" : "error");
            activity.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        }

        /// <summary>Structured completion record for the existing rolling log/Loki pipeline. Contains no request text.</summary>
        /// <param name="activity">Optional trace context.</param><param name="operation">Operation type.</param><param name="model">Model label.</param><param name="statistics">Request measurements.</param><param name="success">Whether generation completed.</param>
        /// <returns>Single-line JSON, including trace identifiers when sampled.</returns>
        public static string FormatCompletion(Activity activity, string operation, string model, InferenceStatistics statistics, bool success)
        {
            ArgumentNullException.ThrowIfNull(statistics);
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                event_name = "engine.inference.completed", operation, model,
                trace_id = activity?.TraceId.ToHexString(), span_id = activity?.SpanId.ToHexString(),
                outcome = success ? "ok" : statistics.Cancelled ? "cancelled" : "error",
                prompt_tokens = statistics.PromptTokens, completion_tokens = statistics.CompletionTokens,
                total_ns = statistics.TotalDurationNanoseconds, queue_wait_ns = statistics.QueueDurationNanoseconds,
                ttft_ns = statistics.FirstTokenDurationNanoseconds, prompt_eval_ns = statistics.PromptDurationNanoseconds,
                generation_ns = statistics.GenerationDurationNanoseconds,
                timing_source = statistics.PhaseTimingsApproximate ? "backend_step_wall_clock" : "native"
            });
        }

        /// <summary>
        /// Record the outcome of an inference call: the request count, its latency, and the number of
        /// tokens generated. Only low-cardinality tags (operation, model, outcome) are attached to metrics.
        /// </summary>
        /// <param name="operation">Operation type, for example "chat", "completion", or "embedding".</param>
        /// <param name="model">Model identifier, or null.</param>
        /// <param name="seconds">Elapsed time in seconds. Negative values are clamped to zero.</param>
        /// <param name="tokensGenerated">Number of tokens generated. Values below zero are treated as zero.</param>
        /// <param name="success">True when the call completed successfully.</param>
        public static void RecordInference(string operation, string model, double seconds, long tokensGenerated, bool success)
        {
            if (seconds < 0) seconds = 0;

            TagList tags = new TagList
            {
                { "operation", operation ?? "unknown" },
                { "model", model ?? "unknown" },
                { "outcome", success ? "ok" : "error" }
            };

            _Requests.Add(1, tags);
            _Latency.Record(seconds, tags);

            if (tokensGenerated > 0)
            {
                TagList tokenTags = new TagList
                {
                    { "operation", operation ?? "unknown" },
                    { "model", model ?? "unknown" }
                };
                _Tokens.Add(tokensGenerated, tokenTags);
            }
        }

        #endregion

        #region Private-Methods

        private static IEnumerable<Measurement<long>> ObserveSlots(ConcurrentDictionary<string, long> slots)
        {
            foreach (KeyValuePair<string, long> slot in slots)
                yield return new Measurement<long>(slot.Value, new KeyValuePair<string, object>("model", slot.Key));
        }

        private static long ObserveResidentModels()
        {
            try
            {
                return _ResidentModelProvider();
            }
            catch
            {
                return 0;
            }
        }

        #endregion
    }
}
