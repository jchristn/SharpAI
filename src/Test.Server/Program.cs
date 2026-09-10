// Live server contract harness. Runs HTTP contract assertions against an already-running SharpAI server.
//
//   dotnet run --project src/Test.Server -- http://127.0.0.1:8000 [--results results.json]
//
// Returns a non-zero exit code if any contract fails, so it gates CI. Process management (starting/stopping
// the server) is the responsibility of the caller/workflow so the assertions stay simple and portable.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Test.Server;
using Touchstone.Cli;
using Touchstone.Core;

string baseUrl = args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal) ? args[0] : "http://127.0.0.1:8000";

string? resultsPath = null;
bool authMode = false;
bool telemetryMode = false;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--results" && i + 1 < args.Length) resultsPath = args[i + 1];
    if (args[i] == "--auth") authMode = true;
    if (args[i] == "--telemetry") telemetryMode = true;
}

using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

TestSuiteDescriptor selected;
if (authMode) selected = ServerAuthContractSuite.Build(client, baseUrl);
else if (telemetryMode) selected = ServerTelemetryContractSuite.Build(client, baseUrl);
else selected = ServerContractSuite.Build(client, baseUrl);

List<TestSuiteDescriptor> suites = new List<TestSuiteDescriptor> { selected };
return await ConsoleRunner.RunAsync(suites, resultsPath: resultsPath).ConfigureAwait(false);
