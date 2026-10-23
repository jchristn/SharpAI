namespace Test.Serve
{
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Cli;

    internal static class Program
    {
        private static Task<int> Main(string[] args)
        {
            string? results = args.Length == 2 && args[0] == "--results" ? args[1] : null;
            return ConsoleRunner.RunAsync(new[] { ServeSuite.Build() }, resultsPath: results);
        }
    }
}
