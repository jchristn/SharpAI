namespace Test.Tui
{
    using System;
    using System.Threading.Tasks;
    using Test.Shared;
    using Test.Shared.Tui;
    using Touchstone.Cli;

    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            if (args.Length == 5 && args[0] == "--telemetry-probe")
            {
                string trace = await TuiTelemetryProbe.RunAsync(args[1], args[2], args[3], System.Threading.CancellationToken.None).ConfigureAwait(false);
                System.IO.File.WriteAllText(args[4], trace); return 0;
            }
            string? results = args.Length == 2 && args[0] == "--results" ? args[1] : null;
            return await ConsoleRunner.RunAsync(new[] { ModelPullSourceSuite.Build(), TuiSuite.Build(), TuiThemeSuite.Build(), TuiChatCommandSuite.Build(), TuiHistoryChartSuite.Build(), TuiStateSuite.Build(), TuiLiveSuite.Build(), TuiOperatorSuite.Build() }, resultsPath: results).ConfigureAwait(false);
        }
    }
}
