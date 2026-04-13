namespace SharpAI.Classes.Runtime
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Environment variable names and parsing helpers for runtime/container controls.
    /// </summary>
    public static class SharpAIEnvironment
    {
        /// <summary>Forces backend selection. Values: auto, cpu, cuda, metal.</summary>
        public const string ForceBackend = "SHARPAI_FORCE_BACKEND";
        /// <summary>Forces an x64 CPU native library variant. Values: auto, avx512, avx2, avx, noavx.</summary>
        public const string CpuVariant = "SHARPAI_CPU_VARIANT";
        /// <summary>When true, startup fails instead of falling back if the selected backend cannot load.</summary>
        public const string RequireBackend = "SHARPAI_REQUIRE_BACKEND";
        /// <summary>Enables llama.cpp native logging.</summary>
        public const string EnableNativeLogging = "SHARPAI_ENABLE_NATIVE_LOGGING";
        /// <summary>Generation thread count. Set to 0 or unset for automatic sizing.</summary>
        public const string NumThreads = "SHARPAI_NUM_THREADS";
        /// <summary>Batch processing thread count. Set to 0 or unset to match generation threads.</summary>
        public const string BatchThreads = "SHARPAI_BATCH_THREADS";
        /// <summary>GPU layer count. Values: auto, -1 for all, 0 for none, or a positive layer count.</summary>
        public const string GpuLayers = "SHARPAI_GPU_LAYERS";
        /// <summary>Main GPU index for llama.cpp model loading.</summary>
        public const string MainGpu = "SHARPAI_MAIN_GPU";
        /// <summary>Context size override. Set to 0 or unset to use model/library defaults.</summary>
        public const string ContextSize = "SHARPAI_CONTEXT_SIZE";
        /// <summary>Prompt batch size override. Set to 0 or unset to use library defaults.</summary>
        public const string BatchSize = "SHARPAI_BATCH_SIZE";
        /// <summary>Physical micro-batch size override. Set to 0 or unset to use library defaults.</summary>
        public const string UBatchSize = "SHARPAI_UBATCH_SIZE";
        /// <summary>Enables memory-mapped model loading.</summary>
        public const string UseMmap = "SHARPAI_USE_MMAP";
        /// <summary>Enables memory locking for loaded model pages.</summary>
        public const string UseMlock = "SHARPAI_USE_MLOCK";
        /// <summary>Enables llama.cpp flash attention when supported by the selected backend/model.</summary>
        public const string FlashAttention = "SHARPAI_FLASH_ATTENTION";

        /// <summary>
        /// Gets a normalized environment variable value, or null when unset, empty, or "null".
        /// </summary>
        /// <param name="name">Environment variable name.</param>
        /// <returns>Normalized string value or null.</returns>
        public static string GetString(string name)
        {
            if (String.IsNullOrWhiteSpace(name)) return null;

            string value = Environment.GetEnvironmentVariable(name);
            if (String.IsNullOrWhiteSpace(value)) return null;

            value = value.Trim().Trim('"', '\'');
            if (String.IsNullOrWhiteSpace(value)) return null;
            if (value.Equals("null", StringComparison.OrdinalIgnoreCase)) return null;

            return value;
        }

        /// <summary>
        /// Gets a boolean environment variable value.
        /// </summary>
        /// <param name="name">Environment variable name.</param>
        /// <param name="defaultValue">Default value when unset or invalid.</param>
        /// <returns>Parsed boolean value.</returns>
        public static bool GetBool(string name, bool defaultValue)
        {
            string value = GetString(name);
            if (String.IsNullOrEmpty(value)) return defaultValue;

            if (Boolean.TryParse(value, out bool parsed)) return parsed;

            if (value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("y", StringComparison.OrdinalIgnoreCase)
                || value.Equals("on", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (value.Equals("0", StringComparison.OrdinalIgnoreCase)
                || value.Equals("no", StringComparison.OrdinalIgnoreCase)
                || value.Equals("n", StringComparison.OrdinalIgnoreCase)
                || value.Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return defaultValue;
        }

        /// <summary>
        /// Gets an integer environment variable value.
        /// </summary>
        /// <param name="name">Environment variable name.</param>
        /// <param name="defaultValue">Default value when unset or invalid.</param>
        /// <param name="minimum">Optional inclusive minimum.</param>
        /// <param name="maximum">Optional inclusive maximum.</param>
        /// <returns>Parsed integer value.</returns>
        public static int GetInt(string name, int defaultValue, int? minimum = null, int? maximum = null)
        {
            string value = GetString(name);
            if (String.IsNullOrEmpty(value)) return defaultValue;

            if (!Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                return defaultValue;
            }

            if (minimum.HasValue && parsed < minimum.Value) return defaultValue;
            if (maximum.HasValue && parsed > maximum.Value) return defaultValue;

            return parsed;
        }

        /// <summary>
        /// Gets an unsigned integer environment variable value.
        /// </summary>
        /// <param name="name">Environment variable name.</param>
        /// <param name="defaultValue">Default value when unset or invalid.</param>
        /// <param name="minimum">Optional inclusive minimum.</param>
        /// <param name="maximum">Optional inclusive maximum.</param>
        /// <returns>Parsed unsigned integer value.</returns>
        public static uint GetUInt(string name, uint defaultValue, uint? minimum = null, uint? maximum = null)
        {
            string value = GetString(name);
            if (String.IsNullOrEmpty(value)) return defaultValue;

            if (!UInt32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsed))
            {
                return defaultValue;
            }

            if (minimum.HasValue && parsed < minimum.Value) return defaultValue;
            if (maximum.HasValue && parsed > maximum.Value) return defaultValue;

            return parsed;
        }
    }
}
