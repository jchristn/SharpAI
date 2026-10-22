namespace Test.Xunit
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using SharpAI.Database;
    using SharpAI.Operations;
    using SharpAI.Services;
    using SyslogLogging;
    using Xunit;

    public sealed class RecoveryTheoryAttribute : TheoryAttribute
    {
        public RecoveryTheoryAttribute()
        {
            if (!File.Exists(Environment.GetEnvironmentVariable("SHARPAI_TEST_WORKER_DLL")) ||
                String.IsNullOrEmpty(Environment.GetEnvironmentVariable("SHARPAI_TEST_HF_URL")))
                Skip = "Requires built Test.Automated worker and Postman simulation.";
        }
    }

    public sealed class CrashRecoveryTests
    {
        private static Process Start(string directory, string mode, string checkpoint)
        {
            ProcessStartInfo start = new ProcessStartInfo("dotnet") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (string arg in new[] { Environment.GetEnvironmentVariable("SHARPAI_TEST_WORKER_DLL")!, "--recovery-probe", mode, directory,
                Environment.GetEnvironmentVariable("SHARPAI_TEST_HF_URL")!, checkpoint }) start.ArgumentList.Add(arg);
            Process? process = Process.Start(start)!;
            // Drain both pipes so native/debug output cannot block checkpoint progress.
            process.OutputDataReceived += (_, _) => { }; process.ErrorDataReceived += (_, _) => { };
            process.BeginOutputReadLine(); process.BeginErrorReadLine();
            return process;
        }

        [RecoveryTheory]
        [InlineData("download", "partial")] [InlineData("pull", "verified")] [InlineData("pull", "registered")] [InlineData("pull", "registering")]
        public async Task AbruptTerminationThenRestartPreservesIntegrityAndRegistry(string mode, string checkpoint)
        {
            string directory = Path.Combine(Path.GetTempPath(), "sharpai-crash-" + Guid.NewGuid()); Directory.CreateDirectory(directory);
            try
            {
                using (Process? child = Start(directory, mode, checkpoint))
                {
                    try
                    {
                        DateTime deadline = DateTime.UtcNow.AddSeconds(30);
                        while (!File.Exists(Path.Combine(directory, "checkpoint")) && !child.HasExited && DateTime.UtcNow < deadline) await Task.Delay(25);
                        Assert.True(File.Exists(Path.Combine(directory, "checkpoint")), "Child did not reach " + checkpoint);
                        child.Kill(true); await child.WaitForExitAsync(); Assert.NotEqual(0, child.ExitCode);
                    }
                    finally { if (!child.HasExited) { child.Kill(true); await child.WaitForExitAsync(); } }
                }
                string? original = File.Exists(Path.Combine(directory, "result")) ? File.ReadAllText(Path.Combine(directory, "result")) : null;
                if (mode == "download")
                {
                    Assert.False(File.Exists(Path.Combine(directory, "model")));
                    Assert.True(new FileInfo(Path.Combine(directory, "model.partial")).Length > 0);
                }
                else
                {
                    using DatabaseDriverBase? db = await DatabaseDriverFactory.CreateAndInitializeAsync(new DatabaseSettings { Filename = Path.Combine(directory, "registry.db") }, new LoggingModule());
                    ModelFileService? registry = new ModelFileService(new LoggingModule(), db.Models, directory);
                    Assert.Equal(checkpoint == "registered", registry.GetByName("org/split") != null);
                }
                using (Process? restarted = Start(directory, mode, "none"))
                {
                    using System.Threading.CancellationTokenSource? timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
                    try { await restarted.WaitForExitAsync(timeout.Token); Assert.Equal(0, restarted.ExitCode); }
                    finally { if (!restarted.HasExited) { restarted.Kill(true); await restarted.WaitForExitAsync(); } }
                }
                if (mode == "download")
                {
                    byte[] data = File.ReadAllBytes(Path.Combine(directory, "model"));
                    Assert.Equal(1024 * 1024, data.Length); Assert.All(data, b => Assert.Equal((byte)120, b));
                    Assert.False(File.Exists(Path.Combine(directory, "model.partial")));
                }
                else
                {
                    using DatabaseDriverBase? db = await DatabaseDriverFactory.CreateAndInitializeAsync(new DatabaseSettings { Filename = Path.Combine(directory, "registry.db") }, new LoggingModule());
                    ModelFileService? registry = new ModelFileService(new LoggingModule(), db.Models, directory);
                    SharpAI.Models.ModelFile? model = registry.GetByName("org/split"); Assert.NotNull(model);
                    Assert.Equal(2, GgufShards.Files(registry.GetFilename("org/split")).Count);
                    Assert.Equal(64, model.SHA256Hash.Length);
                    Assert.Empty(Directory.GetDirectories(directory, ".pull-*"));
                    Assert.Single(Directory.GetDirectories(directory));
                    Assert.Empty(Directory.GetFiles(directory, ".registration-*"));
                    if (original != null) Assert.Equal(original, model.GUID.ToString());
                }
            }
            finally { Directory.Delete(directory, true); }
        }
    }
}
