namespace Test.Xunit
{
    using System.Threading;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Core;
    using Xunit;

    /// <summary>
    /// Exposes each SharpAI Touchstone descriptor as an individual xUnit theory row for per-test
    /// visibility in the Test Explorer.
    /// </summary>
    public sealed class SharpAITheoryTests
    {
        /// <summary>
        /// All non-skipped test cases as theory data.
        /// </summary>
        /// <returns>Theory data of test case descriptors.</returns>
        public static TheoryData<TestSuiteDescriptor, TestCaseDescriptor> TestCases()
        {
            TheoryData<TestSuiteDescriptor, TestCaseDescriptor> data = new TheoryData<TestSuiteDescriptor, TestCaseDescriptor>();

            foreach (TestSuiteDescriptor suite in SharpAISuites.All)
            {
                foreach (TestCaseDescriptor testCase in suite.Cases)
                {
                    if (!testCase.Skip) data.Add(suite, testCase);
                }
            }

            return data;
        }

        /// <summary>
        /// Execute a single descriptor.
        /// </summary>
        /// <param name="suite">Suite containing setup and teardown hooks.</param>
        /// <param name="testCase">Test case to execute.</param>
        /// <returns>Task.</returns>
        [Theory]
        [MemberData(nameof(TestCases))]
        public async Task RunCase(TestSuiteDescriptor suite, TestCaseDescriptor testCase)
        {
            // xUnit analyzer xUnit1030: test methods must not ConfigureAwait(false).
            try
            {
                if (suite.BeforeSuiteAsync != null) await suite.BeforeSuiteAsync(CancellationToken.None);
                await testCase.ExecuteAsync(CancellationToken.None);
            }
            finally
            {
                if (suite.AfterSuiteAsync != null) await suite.AfterSuiteAsync(CancellationToken.None);
            }
        }
    }
}
