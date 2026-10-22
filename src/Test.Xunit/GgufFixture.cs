namespace Test.Xunit
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    internal static class GgufFixture
    {
        internal static byte[] Create(uint version = 3, Dictionary<string, object>? metadata = null)
        {
            metadata ??= new Dictionary<string, object>
            {
                { "general.architecture", "llama" }, { "general.file_type", (uint)2 },
                { "general.parameter_count", (ulong)1000 },
                { "llama.block_count", (uint)4 }, { "llama.context_length", (uint)2048 },
                { "llama.attention.head_count", (uint)8 }, { "llama.attention.head_count_kv", (uint)2 },
                { "llama.embedding_length", (uint)256 }
            };
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true);
            writer.Write(Encoding.ASCII.GetBytes("GGUF")); writer.Write(version);
            writer.Write((ulong)0); writer.Write((ulong)metadata.Count);
            foreach (KeyValuePair<string, object> item in metadata)
            {
                WriteString(writer, item.Key);
                WriteValue(writer, item.Value);
            }
            return stream.ToArray();
        }

        internal static void WriteString(BinaryWriter writer, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            writer.Write((ulong)bytes.Length); writer.Write(bytes);
        }

        private static void WriteValue(BinaryWriter writer, object value)
        {
            switch (value)
            {
                case byte v: writer.Write((uint)0); writer.Write(v); break;
                case sbyte v: writer.Write((uint)1); writer.Write(v); break;
                case ushort v: writer.Write((uint)2); writer.Write(v); break;
                case short v: writer.Write((uint)3); writer.Write(v); break;
                case uint v: writer.Write((uint)4); writer.Write(v); break;
                case int v: writer.Write((uint)5); writer.Write(v); break;
                case float v: writer.Write((uint)6); writer.Write(v); break;
                case bool v: writer.Write((uint)7); writer.Write(v); break;
                case string v: writer.Write((uint)8); WriteString(writer, v); break;
                case ulong v: writer.Write((uint)10); writer.Write(v); break;
                case long v: writer.Write((uint)11); writer.Write(v); break;
                case double v: writer.Write((uint)12); writer.Write(v); break;
                default: throw new ArgumentException("Unsupported fixture type.");
            }
        }
    }
}
