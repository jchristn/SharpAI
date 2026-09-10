using System;
using System.Globalization;
using System.Threading.Tasks;

using SharpAI.Database;
using SyslogLogging;
using Test.Shared;

using Touchstone.Cli;

string? resultsPath = null;
bool benchmark = false;
bool dbmatrix = false;
int iterations = 8;
int concurrency = 4;
int maxTokens = 64;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--results" && i + 1 < args.Length) resultsPath = args[i + 1];
    else if (args[i] == "--benchmark") benchmark = true;
    else if (args[i] == "--dbmatrix") dbmatrix = true;
    else if (args[i] == "--iterations" && i + 1 < args.Length) int.TryParse(args[i + 1], out iterations);
    else if (args[i] == "--concurrency" && i + 1 < args.Length) int.TryParse(args[i + 1], out concurrency);
    else if (args[i] == "--max-tokens" && i + 1 < args.Length) int.TryParse(args[i + 1], out maxTokens);
}

if (benchmark)
{
    if (!ModelFixture.IsAvailable)
    {
        Console.WriteLine("Benchmark requires a model. " + ModelFixture.SkipReason);
        return 2;
    }

    Console.WriteLine("SharpAI concurrency benchmark");
    Console.WriteLine("  model:       " + ModelFixture.Path);
    Console.WriteLine("  iterations:  " + iterations.ToString(CultureInfo.InvariantCulture));
    Console.WriteLine("  concurrency: " + concurrency.ToString(CultureInfo.InvariantCulture));
    Console.WriteLine("  max_tokens:  " + maxTokens.ToString(CultureInfo.InvariantCulture));
    Console.WriteLine("");

    ConcurrencyBenchmark.BenchmarkResult r =
        await ConcurrencyBenchmark.RunAsync(ModelFixture.Path!, iterations, concurrency, maxTokens).ConfigureAwait(false);

    Console.WriteLine("Sequential (1 at a time):");
    Console.WriteLine("  total:        " + r.SequentialSeconds.ToString("F2", CultureInfo.InvariantCulture) + " s");
    Console.WriteLine("  avg latency:  " + r.SequentialAvgLatencyMs.ToString("F1", CultureInfo.InvariantCulture) + " ms/req");
    Console.WriteLine("  throughput:   " + r.SequentialTokensPerSecond.ToString("F1", CultureInfo.InvariantCulture) + " tok/s");
    Console.WriteLine("");
    Console.WriteLine("Concurrent (" + concurrency.ToString(CultureInfo.InvariantCulture) + " slots):");
    Console.WriteLine("  total:        " + r.ConcurrentSeconds.ToString("F2", CultureInfo.InvariantCulture) + " s");
    Console.WriteLine("  throughput:   " + r.ConcurrentTokensPerSecond.ToString("F1", CultureInfo.InvariantCulture) + " tok/s");
    Console.WriteLine("  wall speedup: " + r.Speedup.ToString("F2", CultureInfo.InvariantCulture) + "x");
    return 0;
}

if (dbmatrix)
{
    string providerName = Environment.GetEnvironmentVariable("SHARPAI_DBTEST_PROVIDER") ?? "sqlite";
    DatabaseSettings dbSettings = new DatabaseSettings();

    if (providerName.Equals("postgresql", StringComparison.OrdinalIgnoreCase))
        dbSettings.Type = DatabaseTypeEnum.Postgresql;
    else if (providerName.Equals("mysql", StringComparison.OrdinalIgnoreCase))
        dbSettings.Type = DatabaseTypeEnum.Mysql;
    else if (providerName.Equals("sqlserver", StringComparison.OrdinalIgnoreCase))
        dbSettings.Type = DatabaseTypeEnum.SqlServer;
    else
        dbSettings.Type = DatabaseTypeEnum.Sqlite;

    if (dbSettings.Type != DatabaseTypeEnum.Sqlite)
    {
        dbSettings.Hostname = Environment.GetEnvironmentVariable("SHARPAI_DBTEST_HOST") ?? "127.0.0.1";
        dbSettings.Port = int.TryParse(Environment.GetEnvironmentVariable("SHARPAI_DBTEST_PORT"), out int p) ? p : 0;
        dbSettings.DatabaseName = Environment.GetEnvironmentVariable("SHARPAI_DBTEST_DB") ?? "sharpai";
        dbSettings.Username = Environment.GetEnvironmentVariable("SHARPAI_DBTEST_USER");
        dbSettings.Password = Environment.GetEnvironmentVariable("SHARPAI_DBTEST_PASS");
    }

    Console.WriteLine("DB matrix contract — provider: " + dbSettings.Type +
        (dbSettings.Type != DatabaseTypeEnum.Sqlite ? " (" + dbSettings.Hostname + ":" + dbSettings.Port + "/" + dbSettings.DatabaseName + ")" : ""));

    DatabaseDriverBase driver = DatabaseDriverFactory.Create(dbSettings, new LoggingModule());
    await driver.InitializeAsync().ConfigureAwait(false);

    DbMatrixRunner.DbMatrixResult r = DbMatrixRunner.Run(driver);
    driver.Dispose();

    Console.WriteLine("  " + r.Passed + "/" + r.Total + " checks passed");
    foreach (string f in r.Failures) Console.WriteLine("  FAIL " + f);
    return r.Failures.Count == 0 ? 0 : 1;
}

return await ConsoleRunner.RunAsync(SharpAISuites.All, resultsPath: resultsPath).ConfigureAwait(false);
