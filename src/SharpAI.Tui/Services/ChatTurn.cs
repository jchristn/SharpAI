namespace SharpAI.Tui.Services
{
    using System;
    using System.Text.Json.Nodes;

    internal sealed class ChatTurn
    {
        internal int Number { get; set; }
        internal DateTimeOffset CompletedUtc { get; set; }
        internal string Text { get; set; } = string.Empty;
        internal string Outcome { get; set; } = string.Empty;
        internal JsonNode Measurements { get; set; } = new JsonObject();
    }
}
