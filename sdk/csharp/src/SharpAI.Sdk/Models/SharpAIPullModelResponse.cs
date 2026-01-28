namespace SharpAI.Sdk.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// SharpAI server pull model response message.
    /// </summary>
    public class SharpAIPullModelResponse
    {
        /// <summary>
        /// Current status of the pull operation.
        /// Examples: "pulling manifest", "pulling model", "writing manifest", "success"
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Number of bytes downloaded.
        /// </summary>
        [JsonPropertyName("downloaded")]
        public long? Downloaded { get; set; }

        /// <summary>
        /// Download progress as a decimal (0.0 to 1.0).
        /// </summary>
        [JsonPropertyName("percent")]
        public decimal? Percent { get; set; }

        /// <summary>
        /// Error message if the pull operation failed.
        /// </summary>
        [JsonPropertyName("error")]
        public string Error { get; set; } = string.Empty;

        /// <summary>
        /// Gets the download progress as a percentage (0-100).
        /// </summary>
        /// <returns>Progress percentage (0-100), or null if data unavailable.</returns>
        public double? GetProgressPercentage()
        {
            if (Percent.HasValue)
            {
                return (double)(Percent.Value * 100.0m);
            }
            return null;
        }

        /// <summary>
        /// Gets a formatted progress string.
        /// </summary>
        /// <returns>Progress string (e.g., "1.8 GB (44.7%)").</returns>
        public string GetFormattedProgress()
        {
            if (Downloaded.HasValue && Percent.HasValue)
            {
                var downloadedStr = FormatBytes(Downloaded.Value);
                var percentStr = GetProgressPercentage()?.ToString("F1") ?? "0.0";
                return $"{downloadedStr} ({percentStr}%)";
            }
            return Status ?? "Unknown";
        }

        /// <summary>
        /// Checks if the operation is complete.
        /// </summary>
        /// <returns>True if the status indicates completion.</returns>
        public bool IsComplete()
        {
            return Status?.Equals("success", StringComparison.OrdinalIgnoreCase) == true;
        }

        /// <summary>
        /// Checks if the operation has failed.
        /// </summary>
        /// <returns>True if an error occurred.</returns>
        public bool HasError()
        {
            return !string.IsNullOrEmpty(Error);
        }

        /// <summary>
        /// Formats bytes into human-readable format.
        /// </summary>
        /// <param name="bytes">Number of bytes.</param>
        /// <returns>Formatted string (e.g., "1.5 GB").</returns>
        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:F2} {sizes[order]}";
        }
    }
}
