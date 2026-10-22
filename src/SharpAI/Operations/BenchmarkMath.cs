namespace SharpAI.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Benchmark aggregation with sample-size guards. Thread-safe for immutable inputs.</summary>
    public static class BenchmarkMath
    {
        /// <summary>Nearest-rank percentile; returns null until enough observations exist for its tail probability.</summary>
        /// <param name="values">Finite non-negative measurements.</param><param name="percentile">Percentile from 1 through 99.</param>
        /// <returns>Percentile, or null when the sample is too small.</returns>
        public static double? Percentile(IEnumerable<double> values, int percentile)
        {
            ArgumentNullException.ThrowIfNull(values);
            if (percentile < 1 || percentile > 99) throw new ArgumentOutOfRangeException(nameof(percentile));
            double[] sorted = values.OrderBy(value => value).ToArray();
            if (sorted.Any(value => !Double.IsFinite(value) || value < 0)) throw new ArgumentException("Measurements must be finite and non-negative.", nameof(values));
            int minimum = (int)Math.Ceiling(100.0 / (100 - percentile));
            if (sorted.Length < minimum) return null;
            return sorted[(int)Math.Ceiling(sorted.Length * percentile / 100.0) - 1];
        }
    }
}
