namespace SharpAI.Tui.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text.Json.Nodes;

    internal sealed class HistoryActivityBucket
    {
        internal DateTimeOffset Start { get; }
        internal DateTimeOffset End { get; }
        internal decimal Success { get; }
        internal decimal Failure { get; }
        internal decimal Total { get { return Success + Failure; } }
        internal int Intervals { get; }
        internal HistoryActivityBucket(DateTimeOffset start, DateTimeOffset end, decimal success, decimal failure, int intervals = 1)
        { Start = start; End = end; Success = success; Failure = failure; Intervals = intervals; }
    }

    internal sealed class HistoryActivityData
    {
        internal IReadOnlyList<HistoryActivityBucket> Buckets { get; private set; } = Array.Empty<HistoryActivityBucket>();
        internal decimal Total { get; private set; }
        internal decimal Success { get; private set; }
        internal double AverageDurationMs { get; private set; }
        internal static HistoryActivityData Parse(JsonNode? summary)
        {
            if (summary is not JsonObject || summary["Buckets"] is not JsonArray) throw new InvalidDataException("History summary must include activity buckets.");
            List<HistoryActivityBucket> buckets = new List<HistoryActivityBucket>();
            decimal success = 0; decimal failure = 0;
            foreach (JsonNode row in JsonData.Rows(summary))
            {
                if (!DateTimeOffset.TryParse(JsonData.Text(row, "BucketStartUtc"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset start)
                    || !DateTimeOffset.TryParse(JsonData.Text(row, "BucketEndUtc"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset end) || end <= start)
                    throw new InvalidDataException("History interval has invalid UTC bounds.");
                if (buckets.Count > 0 && start < buckets[buckets.Count - 1].End) throw new InvalidDataException("History intervals must be ordered without overlap.");
                decimal passed = Count(row, "SuccessCount"); decimal failed = Count(row, "FailureCount");
                buckets.Add(new HistoryActivityBucket(start.ToUniversalTime(), end.ToUniversalTime(), passed, failed)); success += passed; failure += failed;
            }
            double average = 0;
            if (summary["AverageDurationMs"] != null && (!double.TryParse(summary["AverageDurationMs"]!.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out average) || !double.IsFinite(average) || average < 0))
                throw new InvalidDataException("History average duration must be a nonnegative finite number.");
            return new HistoryActivityData { Buckets = buckets, Total = summary["TotalCount"] == null ? success + failure : Count(summary, "TotalCount"), Success = summary["TotalSuccess"] == null ? success : Count(summary, "TotalSuccess"), AverageDurationMs = average };
        }
        private static decimal Count(JsonNode value, string key)
        {
            if (!long.TryParse(JsonData.Text(value, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out long count) || count < 0) throw new InvalidDataException("History counts must be nonnegative integers.");
            return count;
        }
    }
}
