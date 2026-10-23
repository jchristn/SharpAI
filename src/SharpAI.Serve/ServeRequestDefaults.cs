namespace SharpAI.Serve
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using SharpAI.Serialization;

    internal static class ServeRequestDefaults
    {
        internal static T Read<T>(string json, string path, ServeOptions options, Serializer serializer) where T : class
        {
            JsonObject body = JsonNode.Parse(json) as JsonObject ?? throw new JsonException("A JSON object is required.");
            if (body["model"] is not JsonValue modelValue || !modelValue.TryGetValue(out string? model) || String.IsNullOrWhiteSpace(model)) throw new JsonException("A model name is required.");
            if (path.EndsWith("/chat", StringComparison.Ordinal) || path.EndsWith("/chat/completions", StringComparison.Ordinal))
            {
                if (body["messages"] is not JsonArray messages || messages.Count == 0) throw new JsonException("A nonempty messages array is required.");
                foreach (JsonNode? message in messages)
                {
                    if (message is not JsonObject item || item["role"] is not JsonValue role || !role.TryGetValue(out string? text) || String.IsNullOrWhiteSpace(text)) throw new JsonException("Each message requires a role.");
                    if (item["content"] == null && item["tool_calls"] == null) throw new JsonException("Each message requires content or tool_calls.");
                }
            }
            if (path == "/api/generate" && (body["prompt"] is not JsonValue prompt || !prompt.TryGetValue(out string? value) || String.IsNullOrEmpty(value))) throw new JsonException("A prompt string is required.");
            if (path == "/api/embed" || path == "/v1/embeddings")
            {
                JsonNode? input = body["input"];
                bool IsText(JsonNode? node) { return node is JsonValue candidate && candidate.TryGetValue(out string? value) && !String.IsNullOrWhiteSpace(value); }
                if (input is JsonArray inputs)
                {
                    if (inputs.Count == 0) throw new JsonException("A nonempty input array is required.");
                    foreach (JsonNode? item in inputs) if (!IsText(item)) throw new JsonException("Embedding inputs must be nonempty strings.");
                }
                else if (!IsText(input)) throw new JsonException("An input string or array is required.");
            }
            bool generation = path is "/api/chat" or "/api/generate" or "/v1/completions" or "/v1/chat/completions";
            if (generation)
            {
                JsonObject target;
                if (path.StartsWith("/api/", StringComparison.Ordinal))
                {
                    if (body["options"] != null && body["options"] is not JsonObject) throw new JsonException("options must be an object.");
                    target = body["options"] as JsonObject ?? new JsonObject();
                    if (body["options"] == null) body["options"] = target;
                    target["num_predict"] ??= JsonValue.Create(options.MaxTokens);
                    // The shared managed handler's legacy default is nonstreaming; Serve follows Ollama's default.
                    body["stream"] ??= JsonValue.Create(true);
                }
                else { target = body; target["max_tokens"] ??= JsonValue.Create(options.MaxTokens); }
                target["temperature"] ??= JsonValue.Create(options.Temperature);
                if (options.Seed.HasValue) target["seed"] ??= JsonValue.Create(options.Seed.Value);
            }
            return serializer.DeserializeJson<T>(body.ToJsonString()) ?? throw new JsonException("A request object is required.");
        }
    }
}
