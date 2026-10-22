namespace Test.Xunit
{
    using System;
    using Xunit;

    /// <summary>Requires a running SharpAI server with a locally imported test model.</summary>
    public sealed class ServerTheoryAttribute : TheoryAttribute
    {
        public ServerTheoryAttribute()
        {
            if (String.IsNullOrEmpty(Environment.GetEnvironmentVariable("SHARPAI_TEST_SERVER_URL")))
                Skip = "Set SHARPAI_TEST_SERVER_URL and import ops-story to run live API tests.";
        }
    }
}
