namespace Test.Automated
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Test result.
    /// </summary>
    public class TestResult
    {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

        /// <summary>
        /// Name of the test.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Test environment.
        /// </summary>
        public TestEnvironment TestEnvironment { get; set; } = null;

        /// <summary>
        /// Boolean indicating if the test succeeded.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// API invocation details.
        /// </summary>
        public List<ApiDetails> ApiDetails
        {
            get => _ApiDetails;
            set => _ApiDetails = (value != null ? value : new List<ApiDetails>());
        }

        /// <summary>
        /// Start timestamp in UTC time.
        /// </summary>
        public DateTime StartUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// End timestamp in UTC time.
        /// </summary>
        public DateTime EndUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Runtime.
        /// </summary>
        public TimeSpan Runtime
        {
            get => (EndUtc - StartUtc);
        }

        private List<ApiDetails> _ApiDetails = new List<ApiDetails>();

        /// <summary>
        /// Exception.
        /// </summary>
        [JsonIgnore]
        public Exception Exception { get; set; } = null;

        /// <summary>
        /// Test result.
        /// </summary>
        public TestResult()
        {

        }

        /// <summary>
        /// Produce a human-readable string version of the object.
        /// </summary>
        /// <returns>String.</returns>
        public override string ToString()
        {
            return $"{Name} success {Success} runtime {Runtime.TotalMilliseconds}ms";
        }

#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
