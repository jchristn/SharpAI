namespace Test.Xunit
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using SharpAI.Database;
    using SharpAI.Database.Sqlite;
    using SharpAI.Models;
    using SyslogLogging;
    using Xunit;

    /// <summary>Separate generation and embedding history filters apply before paging and summary aggregation.</summary>
    public sealed class HistoryCategoryTests
    {
        /// <summary>The list, pagination counts, and charts share the same operation category and tenant restrictions.</summary>
        [Fact]
        public async Task CategoriesRestrictRowsCountsAndSummaryConsistently()
        {
            string filename = Path.Combine(Path.GetTempPath(), "sharpai-history-categories-" + Guid.NewGuid().ToString("N") + ".db");
            using LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;
            try
            {
                using SqliteDatabaseDriver db = new SqliteDatabaseDriver(new DatabaseSettings { Filename = filename }, logging);
                await db.InitializeAsync();
                foreach (string path in new[] { "/api/chat", "/api/generate", "/v1/chat/completions", "/v1/completions", "/api/embed", "/api/embeddings", "/v1/embeddings", "/api/tags", "/api/chatty", "/v1/embeddings-stats" })
                    db.RequestHistory.Create(new RequestHistoryEntry { Path = path, Method = "POST", StatusCode = 200, TenantId = "tenant-a" });
                db.RequestHistory.Create(new RequestHistoryEntry { Path = "/api/embed", Method = "POST", StatusCode = 200, TenantId = "tenant-b" });
                RequestHistoryQuery generations = new RequestHistoryQuery { Category = "generation", TenantId = "tenant-a", PageSize = 1 };
                Assert.Equal(4, db.RequestHistory.Enumerate(generations).TotalRecords);
                Assert.Single(db.RequestHistory.Enumerate(generations).Objects);
                Assert.Equal(4, db.RequestHistory.Summarize(generations).TotalCount);
                RequestHistoryQuery embeddings = new RequestHistoryQuery { Category = "embeddings", TenantId = "tenant-a" };
                Assert.Equal(3, db.RequestHistory.Enumerate(embeddings).TotalRecords);
                Assert.All(db.RequestHistory.Enumerate(embeddings).Objects, entry => Assert.Contains(entry.Path, new[] { "/api/embed", "/api/embeddings", "/v1/embeddings" }));
                Assert.Equal(3, db.RequestHistory.Summarize(embeddings).TotalCount);
                embeddings.PathContains = "/v1/";
                Assert.Single(db.RequestHistory.Enumerate(embeddings).Objects);
                Assert.Equal(1, db.RequestHistory.Summarize(embeddings).TotalCount);
                RequestHistoryQuery legacy = new RequestHistoryQuery { Category = "inference", TenantId = "tenant-a" };
                Assert.Equal(9, db.RequestHistory.Enumerate(legacy).TotalRecords);
                Assert.Equal(10, db.RequestHistory.Enumerate(new RequestHistoryQuery { TenantId = "tenant-a" }).TotalRecords);
            }
            finally { if (File.Exists(filename)) File.Delete(filename); }
        }
    }
}
