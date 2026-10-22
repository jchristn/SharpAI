namespace Test.Xunit
{
    using System;
    using Xunit;

    /// <summary>Requires the locally running Postman download simulation.</summary>
    public sealed class SimulationTheoryAttribute : TheoryAttribute
    {
        public SimulationTheoryAttribute()
        {
            if (String.IsNullOrEmpty(Environment.GetEnvironmentVariable("SHARPAI_TEST_HF_URL")))
                Skip = "Start postman/simulations/downloads.sim.yaml and set SHARPAI_TEST_HF_URL to its /hf URL.";
        }
    }
}
