namespace SharpAI.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Lightweight pure-C# reader for GGUF file metadata.
    /// Reads only the header and key-value metadata section without loading tensor weights.
    /// Supports GGUF format versions 2 and 3.
    /// </summary>
    public static class GgufMetadataReader
    {
        #region Public-Methods

        /// <summary>
        /// Read all metadata key-value pairs from a GGUF file header.
        /// Only the metadata section is read; tensor data is not touched.
        /// </summary>
        /// <param name="filePath">Path to the GGUF file.</param>
        /// <returns>Dictionary of metadata key-value pairs with string keys and object values.</returns>
        /// <exception cref="ArgumentNullException">Thrown when filePath is null or empty.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
        /// <exception cref="InvalidDataException">Thrown when the file is not a valid GGUF file.</exception>
        public static Dictionary<string, object> ReadMetadata(string filePath)
        {
            if (String.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath)) throw new FileNotFoundException("GGUF file not found.", filePath);

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader reader = new BinaryReader(fs, Encoding.UTF8, leaveOpen: true))
            {
                // Magic: 4 bytes, must be "GGUF"
                byte[] magic = reader.ReadBytes(4);
                if (magic.Length < 4 ||
                    magic[0] != 0x47 || magic[1] != 0x47 || magic[2] != 0x55 || magic[3] != 0x46)
                {
                    throw new InvalidDataException("Not a valid GGUF file (bad magic number).");
                }

                // Version: uint32
                uint version = reader.ReadUInt32();
                if (version < 2 || version > 3)
                {
                    throw new InvalidDataException($"Unsupported GGUF version: {version}. Supported: 2, 3.");
                }

                // Tensor count: uint64 (v3) or uint32 (v2)
                ulong tensorCount;
                ulong metadataKvCount;

                if (version >= 3)
                {
                    tensorCount = reader.ReadUInt64();
                    metadataKvCount = reader.ReadUInt64();
                }
                else
                {
                    tensorCount = reader.ReadUInt32();
                    metadataKvCount = reader.ReadUInt32();
                }

                Dictionary<string, object> metadata = new Dictionary<string, object>(StringComparer.Ordinal);

                for (ulong i = 0; i < metadataKvCount; i++)
                {
                    string key = ReadGgufString(reader, version);
                    uint valueType = reader.ReadUInt32();
                    object value = ReadGgufValue(reader, valueType, version);
                    metadata[key] = value;
                }

                return metadata;
            }
        }

        /// <summary>
        /// Read a specific metadata value by key from a GGUF file.
        /// Returns null if the key is not found.
        /// </summary>
        /// <param name="filePath">Path to the GGUF file.</param>
        /// <param name="key">Metadata key to look up.</param>
        /// <returns>The value if found, null otherwise.</returns>
        public static object ReadValue(string filePath, string key)
        {
            Dictionary<string, object> metadata = ReadMetadata(filePath);
            if (metadata.TryGetValue(key, out object value))
                return value;
            return null;
        }

        /// <summary>
        /// Read a specific metadata value as a string from a GGUF file.
        /// Returns null if the key is not found or the value is not a string.
        /// </summary>
        /// <param name="filePath">Path to the GGUF file.</param>
        /// <param name="key">Metadata key to look up.</param>
        /// <returns>The string value if found, null otherwise.</returns>
        public static string ReadStringValue(string filePath, string key)
        {
            object value = ReadValue(filePath, key);
            return value as string;
        }

        #endregion

        #region Capability-Detection

        /// <summary>
        /// Detect model capabilities from GGUF metadata without loading the model.
        /// </summary>
        /// <param name="filePath">Path to the GGUF file.</param>
        /// <param name="architecture">Detected architecture string, or null if not found.</param>
        /// <param name="supportsEmbeddings">True if the model supports embeddings.</param>
        /// <param name="supportsCompletions">True if the model supports text generation.</param>
        public static void DetectCapabilities(
            string filePath,
            out string architecture,
            out bool supportsEmbeddings,
            out bool supportsCompletions)
        {
            Dictionary<string, object> metadata = ReadMetadata(filePath);

            // Read architecture
            architecture = null;
            if (metadata.TryGetValue("general.architecture", out object archObj) && archObj is string archStr)
                architecture = archStr;

            // Detect embeddings support via pooling type.
            // LLamaSharp checks "general.pooling_type" but the raw GGUF key is "{arch}.pooling_type".
            // Check both patterns.
            supportsEmbeddings = false;

            string poolingValue = null;

            if (metadata.TryGetValue("general.pooling_type", out object generalPooling))
                poolingValue = generalPooling?.ToString();

            if (poolingValue == null && !String.IsNullOrEmpty(architecture))
            {
                string archPoolingKey = architecture + ".pooling_type";
                if (metadata.TryGetValue(archPoolingKey, out object archPooling))
                    poolingValue = archPooling?.ToString();
            }

            if (!String.IsNullOrEmpty(poolingValue) && poolingValue != "-1" && poolingValue != "none" && poolingValue != "0")
                supportsEmbeddings = true;

            if (!supportsEmbeddings &&
                !String.IsNullOrEmpty(architecture) &&
                EmbeddingOnlyArchitectures.Contains(architecture))
            {
                supportsEmbeddings = true;
            }

            // Detect completions support: false only for known embedding-only architectures.
            supportsCompletions = true;

            if (!String.IsNullOrEmpty(architecture) &&
                EmbeddingOnlyArchitectures.Contains(architecture))
            {
                supportsCompletions = false;
            }
        }

        /// <summary>
        /// Architectures that produce embeddings only (encoder-only, BERT family, etc.).
        /// Source: llama.cpp's gguf-py/gguf/constants.py MODEL_ARCH enum — these arches
        /// route to encoder-only code paths with no causal text generation.
        /// This is the single source of truth; LlamaSharpEngine references this set.
        /// </summary>
        public static readonly HashSet<string> EmbeddingOnlyArchitectures = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "bert",
            "nomic-bert",
            "nomic-bert-moe",
            "jina-bert-v2",
            "jina-bert-v3",
            "t5encoder",
            "gte",
            "bge",
            "gritlm",
        };

        #endregion

        #region Private-Parsing-Methods

        private static string ReadGgufString(BinaryReader reader, uint version)
        {
            ulong length;
            if (version >= 3)
                length = reader.ReadUInt64();
            else
                length = reader.ReadUInt32();

            if (length == 0) return String.Empty;
            if (length > 1024 * 1024) throw new InvalidDataException($"GGUF string length too large: {length}");

            byte[] bytes = reader.ReadBytes((int)length);
            return Encoding.UTF8.GetString(bytes);
        }

        private static object ReadGgufValue(BinaryReader reader, uint valueType, uint version)
        {
            // GGUF value types:
            // 0 = uint8, 1 = int8, 2 = uint16, 3 = int16,
            // 4 = uint32, 5 = int32, 6 = float32,
            // 7 = bool, 8 = string, 9 = array,
            // 10 = uint64, 11 = int64, 12 = float64
            switch (valueType)
            {
                case 0: // UINT8
                    return reader.ReadByte();

                case 1: // INT8
                    return reader.ReadSByte();

                case 2: // UINT16
                    return reader.ReadUInt16();

                case 3: // INT16
                    return reader.ReadInt16();

                case 4: // UINT32
                    return reader.ReadUInt32();

                case 5: // INT32
                    return reader.ReadInt32();

                case 6: // FLOAT32
                    return reader.ReadSingle();

                case 7: // BOOL
                    return reader.ReadByte() != 0;

                case 8: // STRING
                    return ReadGgufString(reader, version);

                case 9: // ARRAY
                    return ReadGgufArray(reader, version);

                case 10: // UINT64
                    return reader.ReadUInt64();

                case 11: // INT64
                    return reader.ReadInt64();

                case 12: // FLOAT64
                    return reader.ReadDouble();

                default:
                    throw new InvalidDataException($"Unknown GGUF value type: {valueType}");
            }
        }

        private static object ReadGgufArray(BinaryReader reader, uint version)
        {
            uint elementType = reader.ReadUInt32();

            ulong count;
            if (version >= 3)
                count = reader.ReadUInt64();
            else
                count = reader.ReadUInt32();

            if (count > 10 * 1024 * 1024) throw new InvalidDataException($"GGUF array too large: {count}");

            List<object> array = new List<object>((int)Math.Min(count, 1024));

            for (ulong i = 0; i < count; i++)
            {
                array.Add(ReadGgufValue(reader, elementType, version));
            }

            return array;
        }

        #endregion
    }
}
