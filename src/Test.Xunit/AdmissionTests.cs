namespace Test.Xunit
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Classes.Runtime;
    using SharpAI.Engines;
    using SharpAI.Exceptions;
    using SharpAI.Services;
    using SyslogLogging;
    using Xunit;

    /// <summary>Deterministic delayed-load and failure tests for admission reservations and lifecycle.</summary>
    public sealed class AdmissionTests : IDisposable
    {
        private readonly string _Directory = Path.Combine(Path.GetTempPath(), "sharpai-admission-" + Guid.NewGuid());
        private readonly string? _PreviousCount = Environment.GetEnvironmentVariable(SharpAIEnvironment.MaxResidentModels);
        private readonly string? _PreviousBudget = Environment.GetEnvironmentVariable(SharpAIEnvironment.ModelMemoryBudgetMb);
        public AdmissionTests() { Directory.CreateDirectory(_Directory); Environment.SetEnvironmentVariable(SharpAIEnvironment.MaxResidentModels, null); Environment.SetEnvironmentVariable(SharpAIEnvironment.ModelMemoryBudgetMb, null); }
        public void Dispose()
        {
            Directory.Delete(_Directory, true);
            Environment.SetEnvironmentVariable(SharpAIEnvironment.MaxResidentModels, _PreviousCount);
            Environment.SetEnvironmentVariable(SharpAIEnvironment.ModelMemoryBudgetMb, _PreviousBudget);
        }
        private string Model() { string path = Path.Combine(_Directory, Guid.NewGuid() + ".gguf"); File.WriteAllBytes(path, GgufFixture.Create()); return path; }
        private static TaskCompletionSource<LlamaSharpEngine> Pending() { return new TaskCompletionSource<LlamaSharpEngine>(TaskCreationOptions.RunContinuationsAsynchronously); }

        [Fact]
        public async Task SameModelSharesLoadAndCancelingWaiterDoesNotCancelLoad()
        {
            int loads = 0; TaskCompletionSource<LlamaSharpEngine> pending = Pending();
            using ModelEngineService service = new ModelEngineService(new LoggingModule(), path => { loads++; return pending.Task; });
            string path = Model(); Task<LlamaSharpEngine> first = service.GetByModelFileAsync(path);
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            Task<LlamaSharpEngine> waiter = service.GetByModelFileAsync(path, cancellation.Token);
            cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);
            Assert.Empty(service.GetLoadedModelPaths()); Assert.False(service.UnloadModel(path));
            pending.SetResult(new LlamaSharpEngine());
            LlamaSharpEngine engine = await first;
            Assert.Same(engine, await service.GetByModelFileAsync(path)); Assert.Equal(1, loads);
            Assert.Single(service.GetLoadedModelPaths()); Assert.True(service.UnloadModel(path)); Assert.True(engine.IsDisposed);
            Assert.False(service.UnloadModel(path));
        }

        [Theory]
        [InlineData(true)] [InlineData(false)]
        public async Task PendingLoadReservesCountAndMemory(bool countLimit)
        {
            if (countLimit) Environment.SetEnvironmentVariable(SharpAIEnvironment.MaxResidentModels, "1");
            else Environment.SetEnvironmentVariable(SharpAIEnvironment.ModelMemoryBudgetMb, "400");
            TaskCompletionSource<LlamaSharpEngine> pending = Pending(); int loads = 0;
            using ModelEngineService service = new ModelEngineService(new LoggingModule(), path => { loads++; return pending.Task; });
            Task<LlamaSharpEngine> first = service.GetByModelFileAsync(Model());
            Assert.Empty(service.GetLoadedModelPaths()); // Must not drop pending reservation.
            await Assert.ThrowsAsync<ModelAdmissionException>(() => service.GetByModelFileAsync(Model()));
            Assert.Equal(1, loads); pending.SetResult(new LlamaSharpEngine()); await first;
        }

        [Fact]
        public async Task DifferentModelsLoadConcurrentlyWithoutGlobalInitializationLock()
        {
            TaskCompletionSource<LlamaSharpEngine> a = Pending(); TaskCompletionSource<LlamaSharpEngine> b = Pending();
            string firstPath = Model(); string secondPath = Model(); int loads = 0;
            using ModelEngineService service = new ModelEngineService(new LoggingModule(), path => { loads++; return path == firstPath ? a.Task : b.Task; });
            Task<LlamaSharpEngine> first = service.GetByModelFileAsync(firstPath); Task<LlamaSharpEngine> second = service.GetByModelFileAsync(secondPath);
            Assert.Equal(2, loads); Assert.False(first.IsCompleted); Assert.False(second.IsCompleted);
            b.SetResult(new LlamaSharpEngine()); Assert.NotNull(await second); Assert.False(first.IsCompleted);
            a.SetResult(new LlamaSharpEngine()); await first; Assert.Equal(2, service.GetLoadedModelPaths().Count);
        }

        [Fact]
        public async Task FailedLoadReleasesReservationAndCanBeRetried()
        {
            int loads = 0; string path = Model();
            using ModelEngineService service = new ModelEngineService(new LoggingModule(), p => ++loads == 1 ? Task.FromException<LlamaSharpEngine>(new InvalidOperationException("load failed")) : Task.FromResult(new LlamaSharpEngine()));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetByModelFileAsync(path));
            Assert.Empty(service.GetLoadedModelPaths()); Assert.NotNull(await service.GetByModelFileAsync(path)); Assert.Equal(2, loads);
        }

        [Fact]
        public async Task DisposalDuringLoadDisposesLateEngine()
        {
            TaskCompletionSource<LlamaSharpEngine> pending = Pending();
            ModelEngineService service = new ModelEngineService(new LoggingModule(), path => pending.Task);
            Task<LlamaSharpEngine> loading = service.GetByModelFileAsync(Model()); service.Dispose(); service.Dispose();
            LlamaSharpEngine engine = new LlamaSharpEngine(); pending.SetResult(engine);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => loading); Assert.True(engine.IsDisposed);
        }

        [Fact]
        public async Task CompletedModelIsEvictedToHonorCountLimit()
        {
            Environment.SetEnvironmentVariable(SharpAIEnvironment.MaxResidentModels, "1");
            using ModelEngineService service = new ModelEngineService(new LoggingModule(), path => Task.FromResult(new LlamaSharpEngine()));
            LlamaSharpEngine first = await service.GetByModelFileAsync(Model()); LlamaSharpEngine second = await service.GetByModelFileAsync(Model());
            Assert.True(first.IsDisposed); Assert.False(second.IsDisposed); Assert.Single(service.GetLoadedModelPaths());
            Assert.Equal(1, service.UnloadAllModels()); Assert.True(second.IsDisposed);
        }

        [Theory]
        [InlineData("SHARPAI_HOST_MEMORY_BUDGET_MB")] [InlineData("SHARPAI_DEVICE_MEMORY_BUDGET_MB")]
        public async Task PoolBudgetRefusesBeforeFactoryRuns(string variable)
        {
            string? previous = Environment.GetEnvironmentVariable(variable); string backend = NativeBackendInfo.SelectedBackend;
            Environment.SetEnvironmentVariable(variable, "1"); NativeBackendInfo.SelectedBackend = "cuda";
            try
            {
                int loads = 0;
                using ModelEngineService service = new ModelEngineService(new LoggingModule(), path => { loads++; return Task.FromResult(new LlamaSharpEngine()); });
                await Assert.ThrowsAsync<ModelAdmissionException>(() => service.GetByModelFileAsync(Model())); Assert.Equal(0, loads);
            }
            finally { Environment.SetEnvironmentVariable(variable, previous); NativeBackendInfo.SelectedBackend = backend; }
        }

        [Fact]
        public async Task InvalidAndCanceledRequestsDoNotLoad()
        {
            int loads = 0;
            using ModelEngineService service = new ModelEngineService(new LoggingModule(), path => { loads++; return Task.FromResult(new LlamaSharpEngine()); });
            await Assert.ThrowsAsync<ArgumentNullException>(() => service.GetByModelFileAsync(null!));
            await Assert.ThrowsAsync<FileNotFoundException>(() => service.GetByModelFileAsync(Path.Combine(_Directory, "missing")));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetByModelFileAsync(Model(), new CancellationToken(true)));
            Assert.Equal(0, loads); Assert.Null(service.GetExpiryUtc(null!)); Assert.Null(service.GetExpiryUtc("missing"));
            Assert.Throws<ArgumentNullException>(() => service.UnloadModel(null!));
        }
    }
}
