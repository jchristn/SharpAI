namespace SharpAI.Operations
{
    using System;
    using System.Diagnostics;
    using SharpAI.Models.OpenAI;

    /// <summary>Per-call inference statistics, measured before output filtering. Caller-owned; do not share between concurrent calls.</summary>
    public sealed class InferenceStatistics
    {
        private int _PromptTokens = 0;
        /// <summary>Measured PromptTokens. Durations use nanoseconds; unavailable timings are zero.</summary>
        public int PromptTokens { get { return _PromptTokens; } internal set { _PromptTokens = value; } }
        private int _CompletionTokens = 0;
        /// <summary>Measured CompletionTokens. Durations use nanoseconds; unavailable timings are zero.</summary>
        public int CompletionTokens { get { return _CompletionTokens; } internal set { _CompletionTokens = value; } }
        private long _TotalDurationNanoseconds = 0;
        /// <summary>Measured TotalDurationNanoseconds. Durations use nanoseconds; unavailable timings are zero.</summary>
        public long TotalDurationNanoseconds { get { return _TotalDurationNanoseconds; } internal set { _TotalDurationNanoseconds = value; } }
        private long _QueueDurationNanoseconds = 0;
        /// <summary>Measured QueueDurationNanoseconds. Durations use nanoseconds; unavailable timings are zero.</summary>
        public long QueueDurationNanoseconds { get { return _QueueDurationNanoseconds; } internal set { _QueueDurationNanoseconds = value; } }
        private long _LoadDurationNanoseconds = 0;
        /// <summary>Measured LoadDurationNanoseconds. Durations use nanoseconds; unavailable timings are zero.</summary>
        public long LoadDurationNanoseconds { get { return _LoadDurationNanoseconds; } internal set { _LoadDurationNanoseconds = value; } }
        private long _PromptDurationNanoseconds = 0;
        /// <summary>Measured PromptDurationNanoseconds. Durations use nanoseconds; unavailable timings are zero.</summary>
        public long PromptDurationNanoseconds { get { return _PromptDurationNanoseconds; } internal set { _PromptDurationNanoseconds = value; } }
        private long _GenerationDurationNanoseconds = 0;
        /// <summary>Measured GenerationDurationNanoseconds. Durations use nanoseconds; unavailable timings are zero.</summary>
        public long GenerationDurationNanoseconds { get { return _GenerationDurationNanoseconds; } internal set { _GenerationDurationNanoseconds = value; } }
        private long _FirstTokenDurationNanoseconds = 0;
        /// <summary>Measured FirstTokenDurationNanoseconds. Durations use nanoseconds; unavailable timings are zero.</summary>
        public long FirstTokenDurationNanoseconds { get { return _FirstTokenDurationNanoseconds; } internal set { _FirstTokenDurationNanoseconds = value; } }
        private bool _Cancelled = false;
        /// <summary>Measured Cancelled. Durations use nanoseconds; unavailable timings are zero.</summary>
        public bool Cancelled { get { return _Cancelled; } internal set { _Cancelled = value; } }
        private bool _PhaseTimingsApproximate;
        /// <summary>True when native counters are unavailable and measured backend-step wall times are used.
        /// Prompt time then includes inference-context creation and first sampling; consumer/network waits are excluded.</summary>
        public bool PhaseTimingsApproximate { get { return _PhaseTimingsApproximate; } internal set { _PhaseTimingsApproximate = value; } }

        private string _TraceId;
        /// <summary>First measured inference trace identifier, or null when tracing is unavailable.</summary>
        public string TraceId { get { return _TraceId; } internal set { _TraceId = value; } }
        private string _SpanId;
        /// <summary>First measured inference span identifier, or null when tracing is unavailable.</summary>
        public string SpanId { get { return _SpanId; } internal set { _SpanId = value; } }

        /// <summary>Retain trace identity before its activity is disposed. Null leaves existing identity unchanged.</summary>
        /// <param name="activity">The measured engine activity. May be null when no listener is attached.</param>
        public void CaptureTrace(Activity activity)
        {
            if (activity == null || TraceId != null) return;
            TraceId = activity.TraceId.ToHexString();
            SpanId = activity.SpanId.ToHexString();
        }

        /// <summary>Create a request accumulator, optionally including cold model load time.</summary>
        /// <param name="loadDurationNanoseconds">Load time, zero for an already resident model.</param>
        public InferenceStatistics(long loadDurationNanoseconds = 0)
        {
            if (loadDurationNanoseconds < 0) throw new ArgumentOutOfRangeException(nameof(loadDurationNanoseconds));
            LoadDurationNanoseconds = loadDurationNanoseconds;
            TotalDurationNanoseconds = loadDurationNanoseconds;
        }

        /// <summary>Convert aggregate token counts to the compatible usage envelope.</summary>
        /// <returns>Usage for all completed calls accumulated here.</returns>
        public OpenAIUsage ToUsage()
        {
            return new OpenAIUsage { PromptTokens = PromptTokens, CompletionTokens = CompletionTokens, TotalTokens = checked(PromptTokens + CompletionTokens) };
        }

        internal void Add(InferenceStatistics value)
        {
            TraceId ??= value.TraceId;
            SpanId ??= value.SpanId;
            PromptTokens = checked(PromptTokens + value.PromptTokens);
            CompletionTokens = checked(CompletionTokens + value.CompletionTokens);
            TotalDurationNanoseconds += value.TotalDurationNanoseconds;
            QueueDurationNanoseconds += value.QueueDurationNanoseconds;
            LoadDurationNanoseconds += value.LoadDurationNanoseconds;
            PromptDurationNanoseconds += value.PromptDurationNanoseconds;
            GenerationDurationNanoseconds += value.GenerationDurationNanoseconds;
            if (FirstTokenDurationNanoseconds == 0) FirstTokenDurationNanoseconds = value.FirstTokenDurationNanoseconds;
            Cancelled |= value.Cancelled;
            PhaseTimingsApproximate |= value.PhaseTimingsApproximate;
        }
    }
}
