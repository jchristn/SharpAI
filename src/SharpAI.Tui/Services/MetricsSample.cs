namespace SharpAI.Tui.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.RegularExpressions;

    internal sealed class MetricsSample
    {
        private readonly Dictionary<string, double> _Values = new Dictionary<string, double>();
        internal DateTimeOffset Captured { get; } = DateTimeOffset.UtcNow;
        internal static MetricsSample Parse(string text)
        {
            MetricsSample sample = new MetricsSample();
            foreach (string line in text.Split('\n'))
            {
                Match match = Regex.Match(line, @"^([a-zA-Z_:][a-zA-Z0-9_:]*)(?:\{.*\})?\s+([-+0-9.eE]+)(?:\s|$)");
                if (match.Success && double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value))
                    sample._Values[match.Groups[1].Value] = sample._Values.GetValueOrDefault(match.Groups[1].Value) + value;
            }
            return sample;
        }
        internal double? Find(params string[] names)
        {
            foreach (string name in names) if (_Values.TryGetValue(name, out double value)) return value;
            return null;
        }
        internal double? Rate(MetricsSample? previous, params string[] names)
        {
            double? current = Find(names); double? prior = previous?.Find(names);
            double seconds = previous == null ? 0 : (Captured - previous.Captured).TotalSeconds;
            return current.HasValue && prior.HasValue && current >= prior && seconds > 0 ? (current - prior) / seconds : null;
        }
    }
}
