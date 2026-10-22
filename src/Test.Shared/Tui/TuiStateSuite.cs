namespace Test.Shared.Tui
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Client;
    using SharpAI.Tui;
    using SharpAI.Tui.Localization;
    using SharpAI.Tui.Services;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Terminal;
    using TUIKit.Testing;

    /// <summary>Locale, width, permission, empty and transport-failure terminal frames through real simulated HTTP.</summary>
    public static class TuiStateSuite
    {
        private static readonly string[] _Routes = { "/", "/models", "/system-prompts", "/model-configurations", "/playground", "/request-history", "/request-history/inference", "/request-history/embeddings", "/api-explorer", "/observability", "/access/tenants", "/access/users", "/access/credentials", "/access/roles", "/access/permissions", "/access/assignments", "/access/sessions", "/access/audit", "/settings" };
        /// <summary>Build state descriptors. The Postman simulation provides synthetic data and explicit failures.</summary>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            bool skip = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SHARPAI_TUI_TEST_URL"));
            foreach (string locale in Localizer.Languages)
                foreach (string state in new[] { "populated", "empty", "forbidden", "failed" })
                {
                    string selectedLocale = locale; string selectedState = state;
                    cases.Add(new TestCaseDescriptor("TuiStates", locale + "." + state, locale + " " + state + " route frames", ct => FramesAsync(selectedLocale, selectedState, 120, 40, ct), skip: skip, skipReason: "Start the checked Postman TUI simulation."));
                }
            foreach (int width in new[] { 80, 100, 120, 160 })
            {
                int selected = width;
                cases.Add(new TestCaseDescriptor("TuiStates", "Width" + width, "Keyboard focus at " + width + " columns", ct => FramesAsync("en", "populated", selected, selected == 80 ? 24 : selected == 100 ? 30 : selected == 120 ? 40 : 50, ct), skip: skip, skipReason: "Start the checked Postman TUI simulation."));
            }
            return new TestSuiteDescriptor("TuiStates", "Terminal rendering state matrix", cases);
        }
        private static async Task FramesAsync(string locale, string state, int width, int height, CancellationToken ct)
        {
            string endpoint = Environment.GetEnvironmentVariable("SHARPAI_TUI_TEST_URL")!;
            using ServerClient client = new ServerClient(endpoint);
            using HeadlessBackend backend = new HeadlessBackend(width, height) { ClaimsTerminal = false };
            using TestProfileStore profileStore = new TestProfileStore(); using SharpAITuiApp app = new SharpAITuiApp(client, backend, new ConnectionProfile { Server = endpoint, Locale = locale, Ascii = width == 100 }, profileStore.Store);
            app.Host.Start(); await app.ConnectAsync().ConfigureAwait(false); await app.DrainAsync(ct).ConfigureAwait(false);
            client.Token = "fixture-" + state;
            foreach (string route in _Routes)
            {
                app.Navigate(route); await app.DrainAsync(ct).ConfigureAwait(false); app.Host.RenderOnce();
                CellBuffer frame = app.Host.CaptureFrame()!; TestAssert.Equal(width, frame.Width); TestAssert.Equal(height, frame.Height);
                StringBuilder cells = new StringBuilder(); for (int y = 0; y < height; y++) { for (int x = 0; x < width; x++) cells.Append(frame.Get(x, y).Grapheme); cells.AppendLine(); }
                TestAssert.DoesNotContain(cells.ToString(), "fixture-" + state); TestAssert.DoesNotContain(cells.ToString(), "\u001b");
                if (state is "forbidden" or "failed" && route != "/api-explorer") TestAssert.Contains(cells.ToString(), state == "forbidden" ? "403" : "503");
                if (state == "populated") FocusAudit.Run(app.Host).ThrowIfProblems();
                string? evidence = Environment.GetEnvironmentVariable("SHARPAI_TUI_RECORDINGS");
                if (evidence != null && locale == "en" && state == "populated") { Directory.CreateDirectory(evidence); File.WriteAllText(Path.Combine(evidence, "frame-" + width + "-" + route.Replace('/', '_') + ".txt"), cells.ToString()); }
            }
        }
    }
}
