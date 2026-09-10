namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using SharpAI.Models;

    using Touchstone.Core;

    /// <summary>
    /// Contract suite for <see cref="RequestHistoryQuery"/> query-string parsing and numeric clamping. This
    /// is the deterministic filter/pagination surface behind the request-history API and dashboard.
    /// </summary>
    public static class RequestHistoryQuerySuite
    {
        #region Public-Methods

        /// <summary>
        /// Build the request-history query suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor("RequestHistoryQuery", "Defaults", "Sensible defaults",
                    ct =>
                    {
                        RequestHistoryQuery q = new RequestHistoryQuery();
                        TestAssert.Equal(1, q.PageNumber);
                        TestAssert.Equal(25, q.PageSize);
                        TestAssert.Equal(15, q.BucketMinutes);
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("RequestHistoryQuery", "Parses_All", "Parses every query-string override",
                    ct =>
                    {
                        Dictionary<string, string> qs = new Dictionary<string, string>
                        {
                            { "method", "GET" },
                            { "statusCode", "404" },
                            { "pathContains", "/api/chat" },
                            { "pageNumber", "2" },
                            { "pageSize", "50" },
                            { "bucketMinutes", "30" },
                            { "fromUtc", "2026-01-01T00:00:00Z" },
                            { "toUtc", "2026-02-01T00:00:00Z" },
                            { "tenantId", "ten_1" },
                            { "userId", "usr_1" }
                        };
                        RequestHistoryQuery q = new RequestHistoryQuery();
                        q.ApplyQuerystringOverrides(key => qs.TryGetValue(key, out string? v) ? v : null);

                        TestAssert.Equal("GET", q.Method);
                        TestAssert.True(q.StatusCode == 404, "statusCode parsed");
                        TestAssert.Equal("/api/chat", q.PathContains);
                        TestAssert.Equal(2, q.PageNumber);
                        TestAssert.Equal(50, q.PageSize);
                        TestAssert.Equal(30, q.BucketMinutes);
                        TestAssert.True(q.FromUtc.HasValue && q.FromUtc.Value.Year == 2026, "fromUtc parsed to UTC");
                        TestAssert.True(q.ToUtc.HasValue, "toUtc parsed");
                        TestAssert.Equal("ten_1", q.TenantId);
                        TestAssert.Equal("usr_1", q.UserId);
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("RequestHistoryQuery", "Clamps", "Page size / number / bucket are clamped",
                    ct =>
                    {
                        Dictionary<string, string> qs = new Dictionary<string, string>
                        {
                            { "pageSize", "99999" },
                            { "pageNumber", "0" },
                            { "bucketMinutes", "99999" }
                        };
                        RequestHistoryQuery q = new RequestHistoryQuery();
                        q.ApplyQuerystringOverrides(key => qs.TryGetValue(key, out string? v) ? v : null);
                        TestAssert.Equal(1000, q.PageSize);
                        TestAssert.Equal(1, q.PageNumber);
                        TestAssert.Equal(1440, q.BucketMinutes);
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("RequestHistoryQuery", "Ignores_Invalid", "Malformed values are ignored, defaults kept",
                    ct =>
                    {
                        Dictionary<string, string> qs = new Dictionary<string, string>
                        {
                            { "statusCode", "abc" },
                            { "pageSize", "not-a-number" }
                        };
                        RequestHistoryQuery q = new RequestHistoryQuery();
                        q.ApplyQuerystringOverrides(key => qs.TryGetValue(key, out string? v) ? v : null);
                        TestAssert.True(q.StatusCode == null, "non-numeric statusCode ignored");
                        TestAssert.Equal(25, q.PageSize);
                        return Task.CompletedTask;
                    }),

                new TestCaseDescriptor("RequestHistoryQuery", "Null_Getter", "A null getter leaves defaults intact",
                    ct =>
                    {
                        RequestHistoryQuery q = new RequestHistoryQuery();
                        q.ApplyQuerystringOverrides(null);
                        TestAssert.Equal(25, q.PageSize);
                        TestAssert.True(q.Method == null, "method stays null");
                        return Task.CompletedTask;
                    })
            };

            return new TestSuiteDescriptor("RequestHistoryQuery", "Request-history query parsing", cases, null, null);
        }

        #endregion
    }
}
