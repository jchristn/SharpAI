namespace Test.Xunit
{
    using System;
    using System.Collections.Generic;
    using SharpAI.Models;
    using Xunit;

    /// <summary>Browser-style query encoding must preserve filters and time ranges.</summary>
    public sealed class RequestHistoryQueryTests
    {
        /// <summary>Decode URLSearchParams values exactly once, including encoded plus signs.</summary>
        [Fact]
        public void EncodedBrowserFiltersAreApplied()
        {
            Dictionary<string, string> values = new Dictionary<string, string>
            {
                ["fromUtc"] = "2026-10-21T12%3A00%3A00.000Z",
                ["toUtc"] = "2026-10-21T13%3A00%3A00.000Z",
                ["pathContains"] = "%2Fapi%2Fchat%3Fmodel%3DC%2B%2B",
                ["tenantId"] = "tenant+with+spaces",
                ["pageNumber"] = "2",
                ["pageSize"] = "10"
            };
            RequestHistoryQuery query = new RequestHistoryQuery();
            query.ApplyQuerystringOverrides(key => values.TryGetValue(key, out string? value) ? value! : null!);
            Assert.Equal(new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc), query.FromUtc);
            Assert.Equal(new DateTime(2026, 10, 21, 13, 0, 0, DateTimeKind.Utc), query.ToUtc);
            Assert.Equal("/api/chat?model=C++", query.PathContains);
            Assert.Equal("tenant with spaces", query.TenantId);
            Assert.Equal(10, query.Offset);
        }

        /// <summary>Literal percent-encoded text is decoded only once.</summary>
        [Fact]
        public void NestedEscapesRemainLiteral()
        {
            RequestHistoryQuery query = new RequestHistoryQuery();
            query.ApplyQuerystringOverrides(key => key == "pathContains" ? "%252Fapi" : null!);
            Assert.Equal("%2Fapi", query.PathContains);
        }
    }
}
