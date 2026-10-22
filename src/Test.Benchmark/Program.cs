namespace Test.Benchmark
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Client;
    using SharpAI.Tui;
    using SharpAI.Tui.Services;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Terminal;
    using TUIKit.Widgets;

    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            if (args.Length == 1 && args[0] == "--idle-probe") { Console.WriteLine((await MeasureIdleAsync().ConfigureAwait(false)).ToString(System.Globalization.CultureInfo.InvariantCulture)); return 0; }
            string output = args.Length > 0 ? args[0] : "tui-benchmark.json";
            string endpoint = Environment.GetEnvironmentVariable("SHARPAI_TUI_TEST_URL") ?? throw new InvalidOperationException("The stream benchmark requires the checked Postman simulation through SHARPAI_TUI_TEST_URL.");
            JsonArray measurements = new JsonArray();
            for (int iteration = 0; iteration < 12; iteration++)
            {
                Stopwatch processClock = Stopwatch.StartNew();
                using Process startupProcess = Process.Start(new ProcessStartInfo("dotnet") { ArgumentList = { typeof(SharpAITuiApp).Assembly.Location, "--version" }, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false }) ?? throw new InvalidOperationException("Could not start terminal executable.");
                await startupProcess.WaitForExitAsync().ConfigureAwait(false);
                if (startupProcess.ExitCode != 0) throw new InvalidOperationException("Terminal startup failed.");
                double processStartupMs = processClock.Elapsed.TotalMilliseconds;
                long allocated = GC.GetTotalAllocatedBytes(true); Stopwatch clock = Stopwatch.StartNew();
                using HeadlessBackend backend = new HeadlessBackend(120, 40) { ClaimsTerminal = false };
                using ServerClient client = new ServerClient("http://127.0.0.1:1");
                using SharpAITuiApp app = new SharpAITuiApp(client, backend, new ConnectionProfile(), new ProfileStore());
                app.Host.Start(); app.Navigate("/api-explorer"); app.Host.RenderOnce(); double startup = clock.Elapsed.TotalMilliseconds;
                clock.Restart(); for (int frame = 0; frame < 100; frame++) app.Host.RenderOnce(); double render = clock.Elapsed.TotalMilliseconds / 100;
                backend.Resize(80, 24); app.Host.RenderOnce(); backend.Resize(160, 50); app.Host.RenderOnce();
                DataTable<JsonNode> table = new DataTable<JsonNode>().Column("Id", item => item["Id"]!.ToString()).Column("Name", item => item["Name"]!.ToString());
                List<JsonNode> rows = new List<JsonNode>(); for (int index = 0; index < 10000; index++) rows.Add(new JsonObject { ["Id"] = "seed-42-" + index, ["Name"] = "Row " + index });
                clock.Restart(); table.Bind(rows); app.Host.Bind("body", table); app.Host.RenderOnce(); for (int scroll = 0; scroll < 100; scroll++) { backend.FeedKey("Down"); app.Host.PumpInputOnce(); app.Host.RenderOnce(); }
                double tableMs = clock.Elapsed.TotalMilliseconds;
                int bytes = Encoding.UTF8.GetByteCount(backend.TakeOutput());
                MarkdownView transcript = new MarkdownView(); app.Host.Bind("body", transcript); StringBuilder content = new StringBuilder();
                using ServerClient streamClient = new ServerClient(endpoint);
                using CancellationTokenSource streamTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                clock.Restart(); int streamFrames = 0;
                await foreach (JsonNode frame in streamClient.StreamAsync("/fixture/stream/burst", new JsonObject(), streamTimeout.Token).ConfigureAwait(false))
                {
                    content.Append(frame["message"]?["content"]?.ToString()); streamFrames++;
                    if (streamFrames % 32 == 0) { transcript.Markdown = content.ToString(); app.Host.RenderOnce(); }
                }
                if (streamFrames != 2001) throw new InvalidOperationException("Incomplete stream benchmark.");
                double streamMs = clock.Elapsed.TotalMilliseconds;
                bytes += Encoding.UTF8.GetByteCount(backend.TakeOutput());
                measurements.Add(new JsonObject { ["startupMs"] = startup, ["processStartupMs"] = processStartupMs, ["streamMs"] = streamMs, ["streamFrames"] = streamFrames, ["frameMs"] = render, ["largeTableMs"] = tableMs, ["allocatedBytes"] = GC.GetTotalAllocatedBytes(true) - allocated, ["renderedBytes"] = bytes });
            }
            using Process process = Process.GetCurrentProcess();
            using Process idleProbe = Process.Start(new ProcessStartInfo("dotnet") { ArgumentList = { typeof(Program).Assembly.Location, "--idle-probe" }, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false }) ?? throw new InvalidOperationException("Could not start idle CPU probe.");
            string idleOutput = await idleProbe.StandardOutput.ReadToEndAsync().ConfigureAwait(false); await idleProbe.WaitForExitAsync().ConfigureAwait(false);
            if (idleProbe.ExitCode != 0) throw new InvalidOperationException("Idle CPU probe failed.");
            double idleCpu = double.Parse(idleOutput.Trim(), System.Globalization.CultureInfo.InvariantCulture);
            JsonObject result = new JsonObject { ["schemaVersion"] = 2, ["rid"] = RuntimeInformation.RuntimeIdentifier, ["framework"] = RuntimeInformation.FrameworkDescription, ["os"] = RuntimeInformation.OSDescription, ["tUIKit"] = typeof(TuiApplication).Assembly.GetName().Version!.ToString(), ["seed"] = 42, ["terminal"] = "headless 120x40, resize 80x24/160x50", ["idleCpuMsOver2Seconds"] = idleCpu, ["workingSetBytes"] = process.WorkingSet64, ["samples"] = measurements, ["completed"] = true, ["binarySha256"] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(SharpAITuiApp).Assembly.Location))), ["modelSha256"] = Environment.GetEnvironmentVariable("SHARPAI_TUI_MODEL_SHA256") };
            File.WriteAllText(output, result.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            if (args.Length > 1)
            {
                JsonObject baseline = (JsonObject)JsonNode.Parse(File.ReadAllText(args[1]))!;
                if (baseline["schemaVersion"]!.GetValue<int>() != 2) throw new InvalidOperationException("Benchmark schema must match the isolated idle/process-startup methodology.");
                if (baseline["rid"]!.ToString() != result["rid"]!.ToString()) throw new InvalidOperationException("Benchmark runtime must match the baseline.");
                foreach (string metric in new[] { "idleCpuMsOver2Seconds", "workingSetBytes" })
                {
                    double current = double.Parse(result[metric]!.ToString(), System.Globalization.CultureInfo.InvariantCulture); double prior = double.Parse(baseline[metric]!.ToString(), System.Globalization.CultureInfo.InvariantCulture);
                    if (current > Math.Max(prior * 2.5, prior + (metric == "idleCpuMsOver2Seconds" ? 100 : 0))) throw new InvalidOperationException("Benchmark regression: " + metric);
                }
                foreach (string metric in new[] { "startupMs", "processStartupMs", "frameMs", "largeTableMs", "streamMs", "allocatedBytes", "renderedBytes" })
                {
                    double current = Median(measurements, metric); double prior = Median((JsonArray)baseline["samples"]!, metric);
                    if (current > Math.Max(prior * 2.5, prior + (metric.EndsWith("Ms", StringComparison.Ordinal) ? 10 : 0))) throw new InvalidOperationException("Benchmark regression: " + metric);
                }
            }
            Console.WriteLine(output); return 0;
        }
        private static async Task<double> MeasureIdleAsync()
        {
            using HeadlessBackend backend = new HeadlessBackend(120, 40) { ClaimsTerminal = false };
            using TuiApplication host = new TuiApplication(backend) { TargetFps = 30, IdleFps = 2 };
            using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(6));
            Task running = host.RunAsync(timeout.Token);
            await Task.Delay(1000).ConfigureAwait(false);
            GC.Collect(); GC.WaitForPendingFinalizers();
            using Process process = Process.GetCurrentProcess(); TimeSpan before = process.TotalProcessorTime;
            await Task.Delay(2000).ConfigureAwait(false);
            double measured = (process.TotalProcessorTime - before).TotalMilliseconds;
            timeout.Cancel(); try { await running.ConfigureAwait(false); } catch (OperationCanceledException) { }
            return measured;
        }
        private static double Median(JsonArray values, string metric)
        {
            double[] sorted = values.Skip(2).Select(item => double.Parse(item![metric]!.ToString(), System.Globalization.CultureInfo.InvariantCulture)).OrderBy(value => value).ToArray(); return sorted[sorted.Length / 2];
        }
    }
}
