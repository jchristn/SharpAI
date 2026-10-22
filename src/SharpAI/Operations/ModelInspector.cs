namespace SharpAI.Operations
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using SharpAI.Classes.Runtime;
    using SharpAI.Helpers;

    /// <summary>Reads metadata without native model loading. Estimates standard attention caches only; unsupported layouts are explicit.</summary>
    public static class ModelInspector
    {
        /// <summary>Inspect a complete GGUF set using the runtime configuration currently in effect.</summary>
        /// <param name="path">Entry file.</param><returns>Inspection and allocation assumptions.</returns>
        public static ModelInspection Inspect(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            List<string> files = GgufShards.Files(path);
            Dictionary<string, object> metadata = GgufMetadataReader.ReadMetadata(path);
            string architecture = metadata.TryGetValue("general.architecture", out object arch) ? arch as string : null;
            ModelInspection result = new ModelInspection { Architecture = architecture, Files = files };
            foreach (string file in files) result.WeightsBytes = checked(result.WeightsBytes + new FileInfo(file).Length);
            result.Layers = Number(metadata, architecture + ".block_count");
            result.Heads = Number(metadata, architecture + ".attention.head_count");
            result.KvHeads = Number(metadata, architecture + ".attention.head_count_kv");
            if (result.KvHeads == 0) result.KvHeads = result.Heads;
            result.TrainedContext = Number(metadata, architecture + ".context_length");
            result.EffectiveContext = SharpAIEnvironment.GetInt(SharpAIEnvironment.ContextSize, result.TrainedContext, 1);
            result.Slots = SharpAIEnvironment.GetInt(SharpAIEnvironment.MaxConcurrentGenerations, 1, 1);
            result.ParameterCount = LongNumber(metadata, "general.parameter_count");
            long derivedParameters = 0;
            for (int i = 0; i < files.Count; i++)
            {
                Dictionary<string, object> shard = files[i] == path ? metadata : GgufMetadataReader.ReadMetadata(files[i]);
                if (shard.TryGetValue("general.architecture", out object shardArchitecture) && !Equals(architecture, shardArchitecture))
                    throw new InvalidDataException("GGUF shards have different architectures.");
                if (shard.ContainsKey("split.count") && Number(shard, "split.count") != files.Count)
                    throw new InvalidDataException("GGUF split.count does not match the complete set.");
                if (shard.ContainsKey("split.no") && Number(shard, "split.no") != i)
                    throw new InvalidDataException("GGUF split.no does not match shard order.");
                derivedParameters = checked(derivedParameters + LongNumber(shard, "sharpai.tensor_parameter_count"));
            }
            if (derivedParameters > 0) result.ParameterCount = derivedParameters;
            result.QuantizationType = checked((uint)LongNumber(metadata, "general.file_type"));
            result.Supported = architecture != "clip" && architecture != "mmproj" && !Path.GetFileName(path).StartsWith("mmproj", StringComparison.OrdinalIgnoreCase);
            string[] standard = { "llama", "qwen2", "qwen3", "gemma", "gemma2", "gemma3", "mistral", "phi2", "phi3", "stablelm", "starcoder2" };
            int embedding = Number(metadata, architecture + ".embedding_length");
            int key = Number(metadata, architecture + ".attention.key_length");
            int value = Number(metadata, architecture + ".attention.value_length");
            if (key == 0 && result.Heads > 0) key = embedding / result.Heads;
            if (value == 0 && result.Heads > 0) value = embedding / result.Heads;
            if (result.Supported && standard.Contains(architecture) && result.Layers > 0 && result.KvHeads > 0 && key > 0 && value > 0 && result.EffectiveContext > 0)
            {
                checked
                {
                    result.KvBytesPerSlot = (long)result.Layers * result.EffectiveContext * result.KvHeads * ((long)key + value) * 2;
                    result.BuffersBytes = (long)SharpAIEnvironment.GetInt("SHARPAI_MEMORY_BUFFERS_MB", 256, 1) * 1024 * 1024;
                    GgufMetadataReader.DetectCapabilities(path, out string ignoredArchitecture, out bool supportsEmbeddings, out bool supportsGeneration);
                    long subtotal = result.WeightsBytes * (supportsEmbeddings ? 2 : 1) + result.KvBytesPerSlot * ((long)result.Slots + (supportsEmbeddings ? 2 : 1)) + result.BuffersBytes;
                    int percent = SharpAIEnvironment.GetInt("SHARPAI_MEMORY_HEADROOM_PERCENT", 20, 0, 1000);
                    result.HeadroomBytes = checked((long)Math.Ceiling(subtotal * (percent / 100m)));
                    result.TotalBytes = subtotal + result.HeadroomBytes;
                    bool gpu = NativeBackendInfo.SelectedBackend == "cuda" || NativeBackendInfo.SelectedBackend == "metal";
                    int gpuLayers = SharpAIEnvironment.GetInt(SharpAIEnvironment.GpuLayers, gpu ? -1 : 0, -1);
                    bool unified = NativeBackendInfo.SelectedBackend == "metal";
                    // Separate-pool estimates deliberately reserve a full upper bound in each pool for partial offload.
                    result.HostBytes = result.TotalBytes;
                    result.DeviceBytes = gpu && gpuLayers != 0 && !unified ? result.TotalBytes : 0;
                    result.EstimateAvailable = true;
                    result.Assumptions = "Standard full attention; F16 K and V; shared generation weights counted once; one retained base context plus configured generation slots; buffers and headroom configured separately. " +
                        "Host/device upper bounds overlap under CUDA offload and must not be summed. Metal uses one unified host pool. Models advertising embeddings reserve a second weights copy and an embedding context. GPU layers requested=" + gpuLayers + ".";
                }
            }
            else result.Assumptions = "No reliable cache estimate for this architecture or incomplete metadata. Budgeted admission fails closed.";
            return result;
        }

        private static int Number(Dictionary<string, object> metadata, string key)
        {
            return checked((int)LongNumber(metadata, key));
        }

        private static long LongNumber(Dictionary<string, object> metadata, string key)
        {
            if (!metadata.TryGetValue(key, out object value)) return 0;
            if (value is string || value is bool || value is float || value is double) throw new InvalidDataException("Expected integer GGUF metadata: " + key);
            long number = Convert.ToInt64(value);
            if (number < 0) throw new InvalidDataException("Negative GGUF metadata: " + key);
            return number;
        }
    }
}
