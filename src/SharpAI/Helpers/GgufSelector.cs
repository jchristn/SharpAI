namespace SharpAI.Helpers
{
    using SharpAI.Hosting.HuggingFace;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// GGUF selector.
    /// </summary>
    public static class GgufSelector
    {
        /// <summary>
        /// Sorts a list of GGUF files based on Ollama's quantization preference order.
        /// </summary>
        /// <param name="files">The list of GGUF files to sort.</param>
        /// <returns>A sorted list of GGUF files ordered by Ollama's preference.</returns>
        public static List<GgufFileInfo> SortByOllamaPreference(IEnumerable<GgufFileInfo> files)
        {
            if (files == null) return new List<GgufFileInfo>();

            // Define Ollama's quantization preference order (higher priority = lower index)
            Dictionary<string, int> quantizationPriority = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                // Ollama's typical defaults - best balance of quality and performance
                { "Q4_K_M", 1 },   // Most common Ollama default
                { "Q4_0", 2 },     // Alternative common default
                { "Q5_K_M", 3 },   // Slightly higher quality option
                { "Q4_K_S", 4 },   // Good alternative to Q4_K_M
                
                // Higher quality options (larger files, slower)
                { "Q5_K_L", 5 },
                { "Q5_K_S", 6 },
                { "Q5_K", 7 },
                { "Q6_K", 8 },
                { "Q5_1", 9 },
                { "Q5_0", 10 },
                { "Q8_0", 11 },    // Highest quality but often impractical
                
                // Slightly lower quality but still good
                { "Q4_K_L", 12 },
                { "Q4_K", 13 },
                { "Q4_1", 14 },
                
                // Lower quality but faster
                { "Q3_K_XL", 15 },
                { "Q3_K_L", 16 },
                { "Q3_K_M", 17 },
                { "Q3_K_S", 18 },
                { "Q3_K", 19 },
                
                // Lowest quality
                { "Q2_K", 20 },
                { "Q2_K_S", 21 },
                
                // Special formats
                { "F32", 22 },  // Full precision (usually too large)
                { "F16", 23 },  // Half precision
                { "Q8_K", 24 }, // Alternative 8-bit
                
                // IQ (Integer Quantization) variants
                { "IQ4_NL", 25 },
                { "IQ4_XS", 26 },
                { "IQ3_XXS", 27 },
                { "IQ3_S", 28 },
                { "IQ3_M", 29 },
                { "IQ2_XXS", 30 },
                { "IQ2_XS", 31 },
                { "IQ2_S", 32 },
                { "IQ2_M", 33 },
                { "IQ1_S", 34 },
                { "IQ1_M", 35 }
            };

            return files
                .OrderBy(f =>
                {
                    // First, prioritize main model files over shards
                    if (!f.IsMainModel) return 1000;

                    // Get the quantization priority
                    if (string.IsNullOrEmpty(f.QuantizationType)) return 999; // Unknown quantization goes to the end

                    // Try exact match first
                    if (quantizationPriority.TryGetValue(f.QuantizationType, out int priority)) return priority;

                    // Try to extract base quantization type (remove suffixes like _GGUF, etc.)
                    string cleanedType = ExtractBaseQuantizationType(f.QuantizationType);
                    if (!string.IsNullOrEmpty(cleanedType) &&
                        quantizationPriority.TryGetValue(cleanedType, out int cleanedPriority))
                        return cleanedPriority;

                    // If not found in priority list, place at the end but before unknown
                    return 998;
                })
                .ThenBy(f => f.QuantizationType) // Secondary sort by quantization name
                .ThenBy(f => f.Path) // Tertiary sort by filename for consistency
                .ToList();
        }

        /// <summary>
        /// Sorts a list of GGUF files based on user-supplied quantization priority.
        /// </summary>
        /// <param name="files">The list of GGUF files to sort.</param>
        /// <param name="quantizationPriority">User-supplied quantization priority.</param>
        /// <returns>A sorted list of GGUF files ordered by Ollama's preference.</returns>
        public static List<GgufFileInfo> SortByPreference(IEnumerable<GgufFileInfo> files, Dictionary<string, int> quantizationPriority)
        {
            if (files == null) return new List<GgufFileInfo>();
            if (quantizationPriority == null || quantizationPriority.Count < 1) return files.ToList();

            return files
                .OrderBy(f =>
                {
                    // First, prioritize main model files over shards
                    if (!f.IsMainModel) return 1000;

                    // Get the quantization priority
                    if (string.IsNullOrEmpty(f.QuantizationType)) return 999; // Unknown quantization goes to the end

                    // Try exact match first
                    if (quantizationPriority.TryGetValue(f.QuantizationType, out int priority)) return priority;

                    // Try to extract base quantization type (remove suffixes like _GGUF, etc.)
                    string cleanedType = ExtractBaseQuantizationType(f.QuantizationType);
                    if (!string.IsNullOrEmpty(cleanedType) &&
                        quantizationPriority.TryGetValue(cleanedType, out int cleanedPriority))
                        return cleanedPriority;

                    // If not found in priority list, place at the end but before unknown
                    return 998;
                })
                .ThenBy(f => f.QuantizationType) // Secondary sort by quantization name
                .ThenBy(f => f.Path) // Tertiary sort by filename for consistency
                .ToList();
        }

        /// <summary>
        /// Extracts the base quantization type from a potentially complex quantization string.
        /// </summary>
        private static string ExtractBaseQuantizationType(string quantizationType)
        {
            if (string.IsNullOrEmpty(quantizationType)) return null;

            // Common patterns to extract base quantization
            // Examples: "Q4_K_M_GGUF" -> "Q4_K_M", "q5_k_s" -> "Q5_K_S"
            string[] patterns = new[]
            {
                @"^(Q\d+_K_[A-Z]+)",  // Q4_K_M, Q5_K_S, etc.
                @"^(Q\d+_K)",         // Q4_K, Q5_K, etc.
                @"^(Q\d+_\d+)",       // Q4_0, Q5_1, etc.
                @"^(IQ\d+_[A-Z]+)",   // IQ3_XXS, IQ4_NL, etc.
                @"^(F\d+)",           // F16, F32
                @"^(Q\d+)"            // Q8, Q4, etc.
            };

            string upperType = quantizationType.ToUpperInvariant();

            foreach (string pattern in patterns)
            {
                Match match = Regex.Match(upperType, pattern, RegexOptions.IgnoreCase);
                if (match.Success) return match.Groups[1].Value;
            }

            return null;
        }
    }
}