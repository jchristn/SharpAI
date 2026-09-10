namespace Test.Server
{
    using System;
    using System.Net.Http;

    /// <summary>
    /// Minimal assertions for the live server contract cases. Failures throw, which Touchstone records as a
    /// failed case.
    /// </summary>
    public static class Assert
    {
        /// <summary>Assert a condition.</summary>
        public static void True(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Assertion failed: " + message);
        }

        /// <summary>Assert an exact HTTP status code.</summary>
        public static void Status(HttpResponseMessage response, int expected)
        {
            int actual = (int)response.StatusCode;
            if (actual != expected) throw new InvalidOperationException("Expected HTTP " + expected + " but got " + actual);
        }

        /// <summary>Assert a response body contains a substring.</summary>
        public static void Contains(string body, string needle)
        {
            if (body == null || body.IndexOf(needle, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("Expected response to contain [" + needle + "]");
        }
    }
}
