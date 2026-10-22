namespace Test.Shared
{
    using System;

    /// <summary>
    /// Minimal assertion helpers for Touchstone descriptors. Assertions signal failure by
    /// throwing, which the Touchstone executor records as a failed test case. This type is
    /// framework-agnostic and produces no console output.
    /// </summary>
    public static class TestAssert
    {
        /// <summary>
        /// Assert that a condition is true.
        /// </summary>
        /// <param name="condition">Condition expected to be true.</param>
        /// <param name="message">Message describing the expectation.</param>
        /// <exception cref="InvalidOperationException">Thrown when the condition is false.</exception>
        public static void True([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Assertion failed: " + message);
        }

        /// <summary>
        /// Assert that two strings are equal (ordinal).
        /// </summary>
        /// <param name="expected">Expected value.</param>
        /// <param name="actual">Actual value.</param>
        /// <exception cref="InvalidOperationException">Thrown when the values differ.</exception>
        public static void Equal(string? expected, string? actual)
        {
            if (!String.Equals(expected, actual, StringComparison.Ordinal))
                throw new InvalidOperationException("Expected [" + Show(expected) + "] but got [" + Show(actual) + "]");
        }

        /// <summary>
        /// Assert that two values are equal.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="expected">Expected value.</param>
        /// <param name="actual">Actual value.</param>
        /// <exception cref="InvalidOperationException">Thrown when the values differ.</exception>
        public static void Equal<T>(T expected, T actual)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException("Expected [" + expected + "] but got [" + actual + "]");
        }

        /// <summary>
        /// Assert that a string contains a substring.
        /// </summary>
        /// <param name="haystack">String to search.</param>
        /// <param name="needle">Substring expected to be present.</param>
        /// <exception cref="InvalidOperationException">Thrown when the substring is absent.</exception>
        public static void Contains(string? haystack, string needle)
        {
            if (haystack == null || haystack.IndexOf(needle, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("Expected to find [" + needle + "] in [" + Show(haystack) + "]");
        }

        /// <summary>
        /// Assert that a string does not contain a substring.
        /// </summary>
        /// <param name="haystack">String to search.</param>
        /// <param name="needle">Substring expected to be absent.</param>
        /// <exception cref="InvalidOperationException">Thrown when the substring is present.</exception>
        public static void DoesNotContain(string? haystack, string needle)
        {
            if (haystack != null && haystack.IndexOf(needle, StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("Did not expect to find [" + needle + "] in [" + Show(haystack) + "]");
        }

        private static string Show(string? value)
        {
            if (value == null) return "<null>";
            return value.Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
}
