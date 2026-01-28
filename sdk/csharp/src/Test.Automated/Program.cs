namespace Test.Automated
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Test.Automated.Tests;

    /// <summary>
    /// SharpAI SDK automated tests.
    /// </summary>
    public static partial class Program
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        /*
         * 
         * Modify TestEnvironment.cs to set the SharpAI server hostname and port, along with other 
         * important variables.
         * 
         */

        private static string _Line = new string('-', Console.WindowWidth - 1);

        /// <summary>
        /// Entry point.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <returns>Task.</returns>
        public static async Task Main(string[] args)
        {
            #region Welcome

            Console.WriteLine();
            Console.WriteLine("SharpAI SDK Automated Tests");
            Console.WriteLine(_Line);

            List<TestResult> results = new List<TestResult>();

            #endregion

            #region Tests

            Console.WriteLine("Running tests");
            results.Add(await RunTest(new Test1(), true));
            results.Add(await RunTest(new Test2(), true));
            results.Add(await RunTest(new Test3(), true));
            results.Add(await RunTest(new Test4(), true));
            results.Add(await RunTest(new Test5(), true));
            results.Add(await RunTest(new Test6(), true));
            results.Add(await RunTest(new Test7(), true));
            results.Add(await RunTest(new Test8(), true));
            results.Add(await RunTest(new Test9(), true));

            #endregion

            #region Summary

            Console.WriteLine();
            Console.WriteLine("Summary results");
            Console.WriteLine(_Line);

            int success = 0;
            int failure = 0;

            foreach (TestResult result in results)
            {
                if (result.Success) success++;
                else failure++;
                Console.WriteLine($"| {result.ToString()}");

                if (!result.Success)
                {
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };
                    Console.WriteLine(JsonSerializer.Serialize(result, options));

                    if (result.Exception != null)
                        Console.WriteLine(result.Exception.ToString());

                    Console.WriteLine();
                }
            }

            Console.WriteLine();
            Console.WriteLine($"{success} test(s) passed");
            Console.WriteLine($"{failure} test(s) failed");
            Console.WriteLine();

            if (failure == 0) Console.WriteLine("Test succeeded");
            else Console.WriteLine("Test failed");
            Console.WriteLine();

            #endregion
        }

        private static async Task<TestResult> RunTest(TestBase test, bool cleanAfter = true)
        {
            Console.WriteLine("Running test: " + test.Name);

            TestResult result = new TestResult
            {
                Name = test.Name,
                TestEnvironment = test.TestEnvironment,
                StartUtc = DateTime.Now
            };

            try
            {
                await test.Run(result); 
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Exception = ex;
            }
            finally
            {
                if (cleanAfter)
                {
                    test.SharpAISdk?.Dispose();
                    Helpers.Cleanup(true);
                }
            }

            result.EndUtc = DateTime.Now;
            return result;
        }

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
