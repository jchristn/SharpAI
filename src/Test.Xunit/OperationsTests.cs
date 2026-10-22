namespace Test.Xunit
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using SharpAI.Classes.Runtime;
    using SharpAI.Exceptions;
    using SharpAI.Helpers;
    using SharpAI.Hosting;
    using SharpAI.Hosting.HuggingFace;
    using SharpAI.Models.OpenAI;
    using SharpAI.Operations;
    using SharpAI.Services;
    using SharpAI.Telemetry;
    using SyslogLogging;
    using Xunit;

    /// <summary>Deterministic positive and negative coverage for operational invariants.</summary>
    public sealed class OperationsTests : IDisposable
    {
        private readonly string _Directory = Path.Combine(Path.GetTempPath(), "sharpai-operations-" + Guid.NewGuid());
        public OperationsTests() { Directory.CreateDirectory(_Directory); }
        public void Dispose() { Directory.Delete(_Directory, true); }
        private string Model(byte[]? bytes = null)
        {
            string path = Path.Combine(_Directory, Guid.NewGuid() + ".gguf");
            File.WriteAllBytes(path, bytes ?? GgufFixture.Create());
            return path;
        }

        [Theory]
        [InlineData(2u)] [InlineData(3u)]
        public void GgufVersionsAndPreflight(uint version)
        {
            string path = Model(GgufFixture.Create(version));
            ModelInspection inspection = ModelInspector.Inspect(path);
            Assert.Equal("llama", inspection.Architecture);
            Assert.True(inspection.Supported); Assert.True(inspection.EstimateAvailable);
            Assert.Equal(2048, inspection.TrainedContext);
            Assert.Equal(4, inspection.Layers); Assert.Equal(8, inspection.Heads); Assert.Equal(2, inspection.KvHeads);
            Assert.Equal(1000, inspection.ParameterCount); Assert.Equal(2u, inspection.QuantizationType);
            Assert.Equal(4L * inspection.EffectiveContext * 2 * (32 + 32) * 2, inspection.KvBytesPerSlot);
            Assert.Equal(inspection.WeightsBytes + inspection.KvBytesPerSlot * (inspection.Slots + 1L) + inspection.BuffersBytes + inspection.HeadroomBytes, inspection.TotalBytes);
            Assert.Equal(inspection.TotalBytes, inspection.HostBytes);
            Assert.NotNull(inspection.Assumptions);
            Assert.Equal(new FileInfo(path).Length, inspection.WeightsBytes);
        }

        [Fact]
        public void MetadataScalarsRoundTrip()
        {
            object[] values = { (byte)255, (sbyte)-3, (ushort)500, (short)-500, (uint)80000, -80000, 1.25f, true, "é文", (ulong)123456789, (long)-123456789, 1.234567d };
            Dictionary<string, object> metadata = values.Select((value, i) => new KeyValuePair<string, object>(i.ToString(), value)).ToDictionary(p => p.Key, p => p.Value);
            string path = Model(GgufFixture.Create(3, metadata));
            Dictionary<string, object> actual = GgufMetadataReader.ReadMetadata(path);
            foreach (KeyValuePair<string, object> entry in metadata) Assert.Equal(entry.Value, actual[entry.Key]);
            Assert.Null(GgufMetadataReader.ReadValue(path, "missing"));
            Assert.Null(GgufMetadataReader.ReadStringValue(path, "1"));
            Assert.Equal("é文", GgufMetadataReader.ReadStringValue(path, "8"));
        }

        [Theory]
        [InlineData(0)] [InlineData(1)] [InlineData(4)] [InlineData(7)] [InlineData(8)] [InlineData(15)] [InlineData(23)] [InlineData(30)] [InlineData(40)] [InlineData(55)]
        public void TruncatedMetadataCannotBeInspected(int length)
        {
            byte[] bytes = GgufFixture.Create();
            Assert.ThrowsAny<Exception>(() => ModelInspector.Inspect(Model(bytes.Take(length).ToArray())));
        }

        [Theory]
        [InlineData(0u)] [InlineData(1u)] [InlineData(4u)] [InlineData(uint.MaxValue)]
        public void UnsupportedVersionsRejected(uint version)
        {
            Assert.Throws<InvalidDataException>(() => GgufMetadataReader.ReadMetadata(Model(GgufFixture.Create(version))));
        }

        [Theory]
        [InlineData(13u)] [InlineData(99u)] [InlineData(uint.MaxValue)]
        public void UnknownMetadataTypeRejected(uint type)
        {
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(Encoding.ASCII.GetBytes("GGUF")); writer.Write(3u); writer.Write(0ul); writer.Write(1ul);
            GgufFixture.WriteString(writer, "bad"); writer.Write(type);
            Assert.Throws<InvalidDataException>(() => GgufMetadataReader.ReadMetadata(Model(stream.ToArray())));
        }

        [Theory]
        [InlineData(false)] [InlineData(true)]
        public void ArraysAreReadAndNestedArraysRejected(bool nested)
        {
            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(Encoding.ASCII.GetBytes("GGUF")); writer.Write(3u); writer.Write(0ul); writer.Write(1ul);
            GgufFixture.WriteString(writer, "array"); writer.Write(9u); writer.Write(nested ? 9u : 4u); writer.Write(2ul); writer.Write(7u); writer.Write(9u);
            string path = Model(stream.ToArray());
            if (nested) Assert.Throws<InvalidDataException>(() => GgufMetadataReader.ReadMetadata(path));
            else Assert.Equal(new object[] { 7u, 9u }, Assert.IsType<List<object>>(GgufMetadataReader.ReadValue(path, "array")));
        }

        [Theory]
        [InlineData("clip", false)] [InlineData("mamba", true)] [InlineData("", true)]
        public void UnsupportedOrUnknownArchitectureNeverInventsEstimate(string architecture, bool supported)
        {
            string path = Model(GgufFixture.Create(3, new Dictionary<string, object> { { "general.architecture", architecture } }));
            ModelInspection result = ModelInspector.Inspect(path);
            Assert.Equal(supported, result.Supported); Assert.False(result.EstimateAvailable); Assert.Equal(0, result.TotalBytes);
        }

        [Theory]
        [InlineData("bad")] [InlineData(-1)] [InlineData(1.5)] [InlineData(true)]
        public void InvalidNumericMetadataRejected(object invalid)
        {
            Assert.ThrowsAny<Exception>(() => ModelInspector.Inspect(Model(GgufFixture.Create(3, new Dictionary<string, object> { { "llama.block_count", invalid }, { "general.architecture", "llama" } }))));
        }

        [Fact]
        public void MissingPathsAndNullInputsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => GgufMetadataReader.ReadMetadata(null!));
            Assert.Throws<FileNotFoundException>(() => GgufMetadataReader.ReadMetadata(Path.Combine(_Directory, "missing")));
            Assert.Throws<ArgumentNullException>(() => ModelInspector.Inspect(null!));
            Assert.Throws<ArgumentNullException>(() => GgufShards.Select(null!, new GgufFileInfo()));
            Assert.Throws<ArgumentNullException>(() => GgufShards.Select(Array.Empty<GgufFileInfo>(), null!));
        }

        [Theory]
        [InlineData(null)] [InlineData("")] [InlineData("abc")] [InlineData("sha256:abc")]
        [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
        public void MissingOrInvalidDigestFailsClosed(string? value)
        {
            Assert.Throws<InvalidDataException>(() => VerifiedDownload.NormalizeHash(value!));
        }

        [Theory]
        [InlineData("")] [InlineData("sha256:")] [InlineData("SHA256:")]
        public void DigestNormalized(string prefix)
        {
            string hash = new string('a', 64);
            Assert.Equal(hash.ToUpperInvariant(), VerifiedDownload.NormalizeHash(prefix + hash));
        }

        [Fact]
        public void CompleteShardSetOrderedAndResolved()
        {
            Guid id = Guid.NewGuid(); string dir = Path.Combine(_Directory, id.ToString()); Directory.CreateDirectory(dir);
            string first = Path.Combine(dir, "model-00001-of-00002.gguf");
            string second = Path.Combine(dir, "model-00002-of-00002.gguf");
            File.WriteAllBytes(first, GgufFixture.Create()); File.WriteAllBytes(second, GgufFixture.Create());
            Assert.Equal(first, GgufShards.Resolve(_Directory, id));
            Assert.Equal(new[] { first, second }, GgufShards.Files(second));
            Assert.Equal(new FileInfo(first).Length * 2, ModelInspector.Inspect(first).WeightsBytes);
            GgufShards.Delete(_Directory, id); Assert.False(Directory.Exists(dir));
        }

        [Theory]
        [InlineData("model-00001-of-00002.gguf", "model-00001-of-00002.gguf")]
        [InlineData("model-00001-of-00002.gguf", "model-00002-of-00003.gguf")]
        [InlineData("model-00000-of-00002.gguf", "model-00001-of-00002.gguf")]
        [InlineData("model-00001-of-00003.gguf", "model-00003-of-00003.gguf")]
        [InlineData("model-00001-of-00000.gguf", "model-00002-of-00000.gguf")]
        public void AmbiguousOrMissingShardsRejected(string a, string b)
        {
            GgufFileInfo first = new GgufFileInfo { Path = a };
            Assert.Throws<InvalidDataException>(() => GgufShards.Select(new[] { first, new GgufFileInfo { Path = b } }, first));
        }

        [Fact]
        public void EmptyDirectoryAndLegacyStorage()
        {
            Guid id = Guid.NewGuid(); string path = Path.Combine(_Directory, id.ToString());
            Assert.Equal(path, GgufShards.Resolve(_Directory, id));
            File.WriteAllText(path, "legacy"); Assert.Equal(new[] { path }, GgufShards.Files(path));
            GgufShards.Delete(_Directory, id); Assert.False(File.Exists(path));
            Directory.CreateDirectory(path); Assert.Throws<InvalidDataException>(() => GgufShards.Resolve(_Directory, id));
        }

        [Theory]
        [InlineData(10, 5, 15)] [InlineData(0, 1, 1)] [InlineData(9, 1, 10)]
        public void ContextBoundaryAccepted(int prompt, int output, int limit) { ContextLimitException.Validate(prompt, output, limit); }

        [Theory]
        [InlineData(10, 6, 15)] [InlineData(int.MaxValue, int.MaxValue, int.MaxValue)]
        public void ContextOverflowIncludesCounts(int prompt, int output, int limit)
        {
            ContextLimitException error = Assert.Throws<ContextLimitException>(() => ContextLimitException.Validate(prompt, output, limit));
            Assert.Contains("prompt=" + prompt, error.Message); Assert.Contains("limit=" + limit, error.Message);
        }

        [Theory]
        [InlineData(-1, 1, 2)] [InlineData(0, 0, 2)] [InlineData(0, -1, 2)] [InlineData(0, 1, 0)]
        public void InvalidContextArguments(int prompt, int output, int limit) { Assert.Throws<ArgumentOutOfRangeException>(() => ContextLimitException.Validate(prompt, output, limit)); }

        [Fact]
        public void StatisticsAggregateBeforeFiltering()
        {
            InferenceStatistics total = new InferenceStatistics(100);
            total.Add(new InferenceStatistics { PromptTokens = 4, CompletionTokens = 3, TotalDurationNanoseconds = 20, FirstTokenDurationNanoseconds = 8 });
            total.Add(new InferenceStatistics { PromptTokens = 5, CompletionTokens = 7, TotalDurationNanoseconds = 30, FirstTokenDurationNanoseconds = 9, Cancelled = true });
            Assert.Equal(9, total.ToUsage().PromptTokens); Assert.Equal(10, total.ToUsage().CompletionTokens); Assert.Equal(19, total.ToUsage().TotalTokens);
            Assert.Equal(150, total.TotalDurationNanoseconds); Assert.Equal(100, total.LoadDurationNanoseconds); Assert.Equal(8, total.FirstTokenDurationNanoseconds); Assert.True(total.Cancelled);
            Assert.Throws<ArgumentOutOfRangeException>(() => new InferenceStatistics(-1));
        }

        [Theory]
        [InlineData(50, 2, 1d)] [InlineData(95, 20, 19d)] [InlineData(99, 100, 99d)]
        public void PercentilesRequireEnoughSamples(int percentile, int count, double expected)
        {
            Assert.Null(BenchmarkMath.Percentile(Enumerable.Range(1, count - 1).Select(i => (double)i), percentile));
            Assert.Equal(expected, BenchmarkMath.Percentile(Enumerable.Range(1, count).Select(i => (double)i), percentile));
        }

        [Theory]
        [InlineData(-1d)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
        public void BadBenchmarkSamplesRejected(double value) { Assert.Throws<ArgumentException>(() => BenchmarkMath.Percentile(new[] { value }, 50)); }

        [Fact]
        public void BadPercentileArgumentsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => BenchmarkMath.Percentile(null!, 50));
            Assert.Throws<ArgumentOutOfRangeException>(() => BenchmarkMath.Percentile(Array.Empty<double>(), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => BenchmarkMath.Percentile(Array.Empty<double>(), 100));
        }

        [Fact]
        public void StreamUsageOptionDeserializesAndDefaultsOff()
        {
            Assert.Null(new OpenAIGenerateChatCompletionRequest().StreamOptions);
            OpenAIGenerateChatCompletionRequest request = JsonSerializer.Deserialize<OpenAIGenerateChatCompletionRequest>("{\"stream_options\":{\"include_usage\":true}}")!;
            Assert.True(request.StreamOptions.IncludeUsage);
            Assert.False(new OpenAIStreamOptions().IncludeUsage);
            OpenAIGenerateCompletionRequest completion = JsonSerializer.Deserialize<OpenAIGenerateCompletionRequest>("{\"stream_options\":{\"include_usage\":false}}")!;
            Assert.False(completion.StreamOptions.IncludeUsage);
        }

        [Fact]
        public async Task BudgetRejectsBeforeNativeLoadAndDisposedServiceRejectsUse()
        {
            string? previous = Environment.GetEnvironmentVariable(SharpAIEnvironment.ModelMemoryBudgetMb);
            Environment.SetEnvironmentVariable(SharpAIEnvironment.ModelMemoryBudgetMb, "1");
            try
            {
                using ModelEngineService service = new ModelEngineService(new LoggingModule());
                Assert.Equal(1024 * 1024, service.ModelMemoryBudgetBytes);
                await Assert.ThrowsAsync<ModelAdmissionException>(() => service.GetByModelFileAsync(Model()));
                await Assert.ThrowsAsync<ModelAdmissionException>(() => service.GetByModelFileAsync(Model(GgufFixture.Create(3, new Dictionary<string, object>()))));
                Assert.Empty(service.GetLoadedModelPaths());
                Assert.False(service.UnloadModel("missing")); Assert.Equal(0, service.UnloadAllModels());
                service.Dispose();
                await Assert.ThrowsAsync<ObjectDisposedException>(() => service.GetByModelFileAsync(Model()));
            }
            finally { Environment.SetEnvironmentVariable(SharpAIEnvironment.ModelMemoryBudgetMb, previous); }
        }

        [Fact]
        public void RepositoryEndpointsValidateAndUseRevisionNotFileOid()
        {
            HuggingFaceClient client = new HuggingFaceClient(new LoggingModule(), null!, "http://127.0.0.1:4907/hf/");
            Assert.Equal("http://127.0.0.1:4907/hf/org/model/resolve/commit/sub/a%20b.gguf", client.GetRevisionDownloadUrl("org/model", "commit", "sub/a b.gguf"));
            Assert.Throws<ArgumentException>(() => new HuggingFaceClient(new LoggingModule(), null!, "http://example.com"));
            Assert.Throws<ArgumentNullException>(() => client.GetRevisionDownloadUrl(null!, "commit", "file"));
        }
        [Theory]
        [InlineData(0u, 0ul)] [InlineData(5u, 2ul)] [InlineData(1u, 0ul)] [InlineData(1u, ulong.MaxValue)]
        public void InvalidTensorDimensionsRejected(uint rank, ulong dimension)
        {
            using MemoryStream stream = new MemoryStream(); using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(Encoding.ASCII.GetBytes("GGUF")); writer.Write(3u); writer.Write(1ul); writer.Write(0ul);
            GgufFixture.WriteString(writer, "tensor"); writer.Write(rank); writer.Write(dimension);
            Assert.ThrowsAny<Exception>(() => GgufMetadataReader.ReadMetadata(Model(stream.ToArray())));
        }

        [Fact]
        public void ParameterCountComesFromTensorShapesWithoutReadingWeights()
        {
            using MemoryStream stream = new MemoryStream(); using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(Encoding.ASCII.GetBytes("GGUF")); writer.Write(2u); writer.Write(2ul); writer.Write(0ul);
            GgufFixture.WriteString(writer, "matrix"); writer.Write(2u); writer.Write(4ul); writer.Write(8ul); writer.Write(0u); writer.Write(0ul);
            GgufFixture.WriteString(writer, "bias"); writer.Write(1u); writer.Write(8ul); writer.Write(0u); writer.Write(128ul);
            Assert.Equal(40, ModelInspector.Inspect(Model(stream.ToArray())).ParameterCount);
        }

        [Theory]
        [InlineData("metadata")] [InlineData("tensors")] [InlineData("string")] [InlineData("array")]
        public void OversizedHeaderAllocationsRejected(string kind)
        {
            using MemoryStream stream = new MemoryStream(); using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(Encoding.ASCII.GetBytes("GGUF")); writer.Write(3u); writer.Write(kind == "tensors" ? 1000001ul : 0ul); writer.Write(kind == "metadata" ? 1000001ul : kind == "tensors" ? 0ul : 1ul);
            if (kind == "string") writer.Write(1048577ul);
            if (kind == "array") { GgufFixture.WriteString(writer, "a"); writer.Write(9u); writer.Write(4u); writer.Write(10485761ul); }
            Assert.Throws<InvalidDataException>(() => GgufMetadataReader.ReadMetadata(Model(stream.ToArray())));
        }

        [Theory]
        [InlineData("cuda", true)] [InlineData("metal", false)] [InlineData("cpu", false)]
        public void HostAndDevicePoolsDoNotDoubleCountUnifiedMemory(string backend, bool separate)
        {
            string previous = NativeBackendInfo.SelectedBackend; NativeBackendInfo.SelectedBackend = backend;
            try { ModelInspection result = ModelInspector.Inspect(Model()); Assert.Equal(result.TotalBytes, result.HostBytes); Assert.Equal(separate ? result.TotalBytes : 0, result.DeviceBytes); }
            finally { NativeBackendInfo.SelectedBackend = previous; }
        }

        [Fact]
        public void OperationalTelemetryIncludesTimingSourceCancellationAndSlotGauges()
        {
            System.Collections.Generic.List<string> names = new System.Collections.Generic.List<string>();
            using MeterListener listener = new MeterListener();
            listener.InstrumentPublished = (instrument, meter) => { if (instrument.Meter.Name.StartsWith("SharpAI")) meter.EnableMeasurementEvents(instrument); };
            listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) => { names.Add(instrument.Name); Assert.True(measurement >= 0); });
            listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) => { names.Add(instrument.Name); Assert.True(measurement >= 0); });
            listener.Start();
            SharpAITelemetry.RecordLoad("operations-test", 0.1, 2); SharpAITelemetry.ChangeBusySlots("operations-test", 1);
            SharpAITelemetry.RecordDetails("operations-test", new InferenceStatistics { PromptTokens = 3, CompletionTokens = 2, QueueDurationNanoseconds = 10, FirstTokenDurationNanoseconds = 20, PromptDurationNanoseconds = 20, GenerationDurationNanoseconds = 10, Cancelled = true, PhaseTimingsApproximate = true });
            listener.RecordObservableInstruments(); SharpAITelemetry.ChangeBusySlots("operations-test", -1); SharpAITelemetry.RemoveSlots("operations-test");
            Assert.Contains("sharpai.inference.cancellations", names); Assert.Contains("sharpai.inference.time_to_first_token", names);
            Assert.Contains("sharpai.inference.prompt_tokens_per_second", names); Assert.Contains("sharpai.inference.generation_tokens_per_second", names);
            Assert.Contains("sharpai.inference.total_slots", names); Assert.Contains("sharpai.inference.busy_slots", names);
            Assert.Throws<ArgumentNullException>(() => SharpAITelemetry.RecordDetails("model", null!));
        }

        [Theory]
        [InlineData("architecture")] [InlineData("count")] [InlineData("index")]
        public void ShardMetadataMustAgreeWithTheSelectedSet(string mismatch)
        {
            string first = Path.Combine(_Directory, "model-00001-of-00002.gguf");
            string second = Path.Combine(_Directory, "model-00002-of-00002.gguf");
            File.WriteAllBytes(first, GgufFixture.Create(3, new Dictionary<string, object> { { "general.architecture", "llama" }, { "split.count", 2u }, { "split.no", 0u } }));
            File.WriteAllBytes(second, GgufFixture.Create(3, new Dictionary<string, object>
            {
                { "general.architecture", mismatch == "architecture" ? "clip" : "llama" },
                { "split.count", mismatch == "count" ? 3u : 2u }, { "split.no", mismatch == "index" ? 0u : 1u }
            }));
            Assert.Throws<InvalidDataException>(() => ModelInspector.Inspect(first));
        }

        [Fact]
        public void OriginalPublicSignaturesRemainAvailableToCompiledConsumers()
        {
            Type[] grammarParameters = { typeof(string), typeof(int), typeof(float), typeof(string[]), typeof(string), typeof(System.Threading.CancellationToken) };
            foreach (string method in new[] { "GenerateTextAsync", "GenerateTextStreamAsync", "GenerateChatCompletionAsync", "GenerateChatCompletionStreamAsync" })
                Assert.NotNull(typeof(SharpAI.Engines.LlamaSharpEngine).GetMethod(method, grammarParameters));
            Assert.NotNull(typeof(HuggingFaceClient).GetConstructor(new[] { typeof(LoggingModule), typeof(string) }));
            foreach (string method in new[] { "GetModelFilesAsync", "GetGgufFilesAsync" })
                Assert.NotNull(typeof(HuggingFaceClient).GetMethod(method, new[] { typeof(string), typeof(System.Threading.CancellationToken) }));
            Assert.NotNull(typeof(HuggingFaceClient).GetMethod("GetDownloadUrls", new[] { typeof(string), typeof(HuggingFaceModelFile) }));
        }

    }
}
