namespace SharpAI.Tui.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Nodes;

    internal static class JsonData
    {
        internal static string Text(JsonNode? node, params string[] keys)
        {
            foreach (string key in keys) if (node is JsonObject obj && obj[key] != null) return SafeText.Clean(obj[key]!.ToString());
            return string.Empty;
        }
        internal static string Id(JsonNode node) { return Text(node, "Id", "Guid", "id", "name", "model"); }
        internal static bool Flag(JsonNode? node, string key) { return Text(node, key).Equals("true", StringComparison.OrdinalIgnoreCase); }
        internal static IReadOnlyList<JsonNode> Rows(JsonNode? node)
        {
            JsonArray? array = node as JsonArray;
            if (node is JsonObject obj) array = (obj["Objects"] ?? obj["models"] ?? obj["Models"] ?? obj["Buckets"]) as JsonArray;
            return array?.Where(item => item != null).Select(item => item!).ToList() ?? new List<JsonNode>();
        }
        internal static JsonNode Redact(JsonNode node)
        {
            JsonNode copy = node.DeepClone();
            if (copy is JsonObject obj)
            {
                foreach (string key in obj.Select(entry => entry.Key).ToArray())
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(key, "password|secret|token$|apikey|accesskey", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) obj[key] = "[withheld]";
                    else if (obj[key] != null) obj[key] = Redact(obj[key]!);
                }
            }
            else if (copy is JsonArray array)
                for (int index = 0; index < array.Count; index++) if (array[index] != null) array[index] = Redact(array[index]!);
            return copy;
        }
    }
}
