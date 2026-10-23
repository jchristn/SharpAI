namespace SharpAI.Tui.Services
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.Json.Nodes;

    internal sealed class ChatCommand
    {
        internal bool IsCommand { get; init; }
        internal string Id { get; init; } = string.Empty;
        internal string Arguments { get; init; } = string.Empty;
        internal string Prompt { get; init; } = string.Empty;
    }
    internal static class ChatCommands
    {
        private static readonly JsonObject _Catalog = Load();
        internal static JsonArray Commands { get { return (JsonArray)_Catalog["commands"]!; } }
        internal static string CompactInstruction { get { return _Catalog["compactInstruction"]!.ToString(); } }
        internal static string SummaryPrefix { get { return _Catalog["summaryPrefix"]!.ToString(); } }
        internal static int CompactMaxTokens { get { return _Catalog["compactMaxTokens"]!.GetValue<int>(); } }
        private static JsonObject Load()
        {
            using Stream resource = typeof(ChatCommands).Assembly.GetManifestResourceStream("SharpAI.Tui.chatCommands.json")!;
            return (JsonObject)JsonNode.Parse(resource)!;
        }
        internal static ChatCommand Parse(string input)
        {
            string trimmed = input.Trim();
            if (trimmed.StartsWith("//", StringComparison.Ordinal)) return new ChatCommand { Prompt = trimmed.Substring(1) };
            if (!trimmed.StartsWith("/", StringComparison.Ordinal)) return new ChatCommand { Prompt = input };
            int separator = 0; while (separator < trimmed.Length && !char.IsWhiteSpace(trimmed[separator])) separator++;
            string name = trimmed.Substring(0, separator).ToLowerInvariant();
            JsonNode? command = Commands.FirstOrDefault(item => item!["name"]!.ToString() == name || ((JsonArray)item["aliases"]!).Any(alias => alias!.ToString() == name));
            return new ChatCommand { IsCommand = true, Id = command?["id"]?.ToString() ?? "unknown", Arguments = trimmed.Substring(separator).Trim(), Prompt = trimmed };
        }
    }
}
