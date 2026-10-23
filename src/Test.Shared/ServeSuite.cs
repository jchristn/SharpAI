namespace Test.Shared
{
    using System;
    using System.IO;
    using System.Text.Json.Nodes;
    using System.Threading.Tasks;
    using SharpAI.Serve;
    using SharpAI.Serialization;
    using SharpAI.Models.OpenAI;
    using Touchstone.Core;

    /// <summary>Standalone serving argument and generation-default contracts.</summary>
    public static class ServeSuite
    {
        /// <summary>Build deterministic tests without loading a native model.</summary>
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor("Serve", "Standalone serving", new[]
            {
                new TestCaseDescriptor("Serve", "Help", "Help and version need no files or model", token =>
                {
                    TestAssert.True(ServeOptions.Parse(Array.Empty<string>()).Help, "no arguments show help");
                    TestAssert.True(ServeOptions.Parse(new[] { "serve", "--help" }).Help, "serve help");
                    TestAssert.True(ServeOptions.Parse(new[] { "--version" }).Version, "version");
                    TestAssert.True(ServeOptions.Parse(new[] { "/?" }).Help, "Windows-style help alias");
                    return Task.CompletedTask;
                }),
                new TestCaseDescriptor("Serve", "Options", "Paths and explicit runtime arguments are portable", token => WithModel(path =>
                {
                    ServeOptions options = ServeOptions.Parse(new[] { "serve", path, "--host", "localhost", "--port=8123", "--served-model-name", "assistant", "--backend", "cpu", "--gpu-layers", "all", "--seed", "-12", "--parallel", "4" });
                    TestAssert.Equal(Path.GetFullPath(path), options.ModelPath);
                    TestAssert.Equal("127.0.0.1", options.Host); TestAssert.Equal(8123, options.Port);
                    TestAssert.Equal("assistant", options.ModelName); TestAssert.Equal("-1", options.GpuLayers);
                    TestAssert.Equal(-12, options.Seed!.Value); TestAssert.Equal(4, options.Parallel);
                    TestAssert.Equal(2048, options.MaxTokens); TestAssert.True(options.LogFile == null, "no default log files");
                })),
                new TestCaseDescriptor("Serve", "Invalid", "Invalid arguments fail before native initialization", token => WithModel(path =>
                {
                    foreach (string[] extra in new[] { new[] { "--port", "0" }, new[] { "--port", "65536" }, new[] { "--max-tokens", "0" }, new[] { "--temperature", "NaN" }, new[] { "--parallel", "0" }, new[] { "--backend", "other" }, new[] { "--port", "8001", "--port", "8002" }, new[] { "--served-model-name", "bad\tname" }, new[] { "--unknown", "x" }, new[] { "extra.gguf" } })
                    {
                        string[] arguments = new string[extra.Length + 2]; arguments[0] = "serve"; arguments[1] = path; Array.Copy(extra, 0, arguments, 2, extra.Length);
                        bool rejected = false; try { ServeOptions.Parse(arguments); } catch (ArgumentException) { rejected = true; }
                        TestAssert.True(rejected, "reject " + String.Join(" ", extra));
                    }
                })),
                new TestCaseDescriptor("Serve", "Defaults", "CLI defaults fill missing fields and preserve request overrides", token => WithModel(path =>
                {
                    ServeOptions options = ServeOptions.Parse(new[] { "serve", path, "--max-tokens", "64", "--temperature", "0.2", "--seed", "7" });
                    Serializer serializer = new Serializer();
                    OpenAIGenerateCompletionRequest request = ServeRequestDefaults.Read<OpenAIGenerateCompletionRequest>("{\"model\":\"test\",\"prompt\":\"Hello\"}", "/v1/completions", options, serializer);
                    JsonNode data = JsonNode.Parse(serializer.SerializeJson(request))!;
                    TestAssert.Equal(64, data["max_tokens"]!.GetValue<int>());
                    request = ServeRequestDefaults.Read<OpenAIGenerateCompletionRequest>("{\"model\":\"test\",\"prompt\":\"Hello\",\"max_tokens\":8,\"temperature\":0}", "/v1/completions", options, serializer);
                    data = JsonNode.Parse(serializer.SerializeJson(request))!; TestAssert.Equal(8, data["max_tokens"]!.GetValue<int>());
                    TestAssert.Equal(0f, data["temperature"]!.GetValue<float>());
                    foreach (string invalid in new[] { "[]", "{}", "{\"model\":\"test\",\"messages\":[]}" })
                    {
                        bool rejected = false; try { ServeRequestDefaults.Read<OpenAIGenerateChatCompletionRequest>(invalid, "/v1/chat/completions", options, serializer); } catch (System.Text.Json.JsonException) { rejected = true; }
                        TestAssert.True(rejected, "invalid JSON request rejected");
                    }
                }))
            });
        }
        private static Task WithModel(Action<string> action)
        {
            string path = Path.GetTempFileName();
            try { action(path); } finally { File.Delete(path); }
            return Task.CompletedTask;
        }
    }
}
