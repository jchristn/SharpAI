namespace Test.Automated.Tests
{
    using System;
    using System.Threading.Tasks;
    using SharpAI.Sdk;

    /// <summary>
    /// Base class for SharpAI SDK tests.
    /// </summary>
    public abstract class TestBase
    {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

        #region Public-Members

        /// <summary>
        /// Name of the test.
        /// </summary>
        public string Name
        {
            get => _Name;
            set => _Name = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(Name)));
        }

        /// <summary>
        /// Test environment variables.
        /// </summary>
        public TestEnvironment TestEnvironment
        {
            get => _TestEnvironment;
            set => _TestEnvironment = (value != null ? value : throw new ArgumentNullException(nameof(TestEnvironment)));
        }

        /// <summary>
        /// SharpAI SDK instance.
        /// </summary>
        public SharpAISdk SharpAISdk
        {
            get => _SharpAISdk;
            set => _SharpAISdk = (value != null ? value : throw new ArgumentNullException(nameof(SharpAISdk)));
        }

        private string _Name = "My test";
        private TestEnvironment _TestEnvironment = new TestEnvironment();
        private SharpAISdk _SharpAISdk = null;

        #endregion

        #region Abstract-Methods

        /// <summary>
        /// Run the test.
        /// </summary>
        /// <param name="result">Test result.</param>
        /// <returns>Task.</returns>
        public abstract Task Run(TestResult result);

        #endregion

        #region Public-Methods

        /// <summary>
        /// Initialize the test environment.
        /// </summary>
        /// <returns>Task.</returns>
        public void InitializeTestEnvironment()
        {
            string endpoint = $"http://{TestEnvironment.SharpAIHostname}:{TestEnvironment.SharpAIPort}";

            SharpAISdk = new SharpAISdk(endpoint)
            {
                LogRequests = TestEnvironment.LogRequests,
                LogResponses = TestEnvironment.LogResponses,
                TimeoutMs = TestEnvironment.TimeoutMs
            };
        }

        /// <summary>
        /// Log a message using the configured logger.
        /// </summary>
        /// <param name="level">Log level.</param>
        /// <param name="message">Message to log.</param>
        public void Log(string level, string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                Console.WriteLine($"[{level}] {message}");
            }
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Create API details for tracking.
        /// </summary>
        /// <param name="step">Step name.</param>
        /// <param name="request">Request object.</param>
        /// <returns>API details.</returns>
        protected ApiDetails CreateApiDetails(string step, object request = null)
        {
            return new ApiDetails
            {
                Step = step,
                Request = request,
                StartUtc = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Complete API details with response.
        /// </summary>
        /// <param name="apiDetails">API details to complete.</param>
        /// <param name="response">Response object.</param>
        /// <param name="statusCode">Status code.</param>
        protected void CompleteApiDetails(ApiDetails apiDetails, object response = null, int statusCode = 200)
        {
            apiDetails.Response = response;
            apiDetails.StatusCode = statusCode;
            apiDetails.EndUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Validate that a result is not null.
        /// </summary>
        /// <param name="result">Result to validate.</param>
        /// <param name="testResult">Test result to update.</param>
        /// <param name="errorMessage">Error message if validation fails.</param>
        /// <returns>True if valid, false otherwise.</returns>
        protected bool ValidateNotNull(object result, TestResult testResult, string errorMessage)
        {
            if (result == null)
            {
                Console.WriteLine(errorMessage);
                testResult.Success = false;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Validate that a string is not null or empty.
        /// </summary>
        /// <param name="value">String to validate.</param>
        /// <param name="testResult">Test result to update.</param>
        /// <param name="errorMessage">Error message if validation fails.</param>
        /// <returns>True if valid, false otherwise.</returns>
        protected bool ValidateString(string value, TestResult testResult, string errorMessage)
        {
            if (string.IsNullOrEmpty(value))
            {
                Console.WriteLine(errorMessage);
                testResult.Success = false;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Validate that a collection has the expected count.
        /// </summary>
        /// <param name="collection">Collection to validate.</param>
        /// <param name="expectedCount">Expected count.</param>
        /// <param name="testResult">Test result to update.</param>
        /// <param name="errorMessage">Error message if validation fails.</param>
        /// <returns>True if valid, false otherwise.</returns>
        protected bool ValidateCollectionCount(System.Collections.ICollection collection, int expectedCount, TestResult testResult, string errorMessage)
        {
            if (collection == null || collection.Count != expectedCount)
            {
                Console.WriteLine(errorMessage);
                testResult.Success = false;
                return false;
            }
            return true;
        }

        #endregion

#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
    }
}
