namespace SharpAI.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Query parameters for paginated enumeration and summarization of request history. This is the sole
    /// list operation over request history; there is no unbounded "get all".
    /// </summary>
    public class RequestHistoryQuery
    {
        #region Public-Members

        /// <summary>
        /// Page number (1-based). Default 1; values below 1 are clamped to 1.
        /// </summary>
        public int PageNumber
        {
            get
            {
                return _PageNumber;
            }
            set
            {
                _PageNumber = value < 1 ? 1 : value;
            }
        }

        /// <summary>
        /// Number of results per page. Default 25, minimum 1, maximum 1000.
        /// </summary>
        public int PageSize
        {
            get
            {
                return _PageSize;
            }
            set
            {
                _PageSize = Math.Clamp(value, 1, 1000);
            }
        }

        /// <summary>
        /// Filter by tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = null;

        /// <summary>
        /// Filter by user identifier.
        /// </summary>
        public string UserId { get; set; } = null;

        /// <summary>
        /// Filter by HTTP method.
        /// </summary>
        public string Method { get; set; } = null;

        /// <summary>
        /// Filter by exact HTTP status code.
        /// </summary>
        public int? StatusCode { get; set; } = null;

        /// <summary>
        /// Filter to requests whose path contains this substring.
        /// </summary>
        public string PathContains { get; set; } = null;

        /// <summary>
        /// High-level category filter. <c>"inference"</c> restricts results to the inference/embeddings
        /// endpoints (Ollama and OpenAI chat/generate/embeddings). Null or <c>"all"</c> applies no category
        /// restriction. Combine with <see cref="PathContains"/> to narrow to a single endpoint.
        /// </summary>
        public string Category { get; set; } = null;

        /// <summary>
        /// Filter to requests created at or after this UTC timestamp.
        /// </summary>
        public DateTime? FromUtc { get; set; } = null;

        /// <summary>
        /// Filter to requests created before this UTC timestamp.
        /// </summary>
        public DateTime? ToUtc { get; set; } = null;

        /// <summary>
        /// Bucket width in minutes for the summary endpoint. Default 15, minimum 1, maximum 1440.
        /// </summary>
        public int BucketMinutes
        {
            get
            {
                return _BucketMinutes;
            }
            set
            {
                _BucketMinutes = Math.Clamp(value, 1, 1440);
            }
        }

        /// <summary>
        /// SQL OFFSET derived from the page number and page size.
        /// </summary>
        [JsonIgnore]
        public int Offset
        {
            get { return (PageNumber - 1) * PageSize; }
        }

        #endregion

        #region Private-Members

        private int _PageNumber = 1;
        private int _PageSize = 25;
        private int _BucketMinutes = 15;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate with defaults.
        /// </summary>
        public RequestHistoryQuery()
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Apply query-string overrides. Any non-empty query-string value replaces the corresponding value.
        /// </summary>
        /// <param name="queryGetter">Function that retrieves a query-string value by key.</param>
        public void ApplyQuerystringOverrides(Func<string, string> queryGetter)
        {
            if (queryGetter == null) return;

            string value;

            value = queryGetter("pageNumber");
            if (!String.IsNullOrEmpty(value) && Int32.TryParse(value, out int pageNumber)) PageNumber = pageNumber;

            value = queryGetter("pageSize");
            if (!String.IsNullOrEmpty(value) && Int32.TryParse(value, out int pageSize)) PageSize = pageSize;

            value = queryGetter("tenantId");
            if (!String.IsNullOrEmpty(value)) TenantId = value;

            value = queryGetter("userId");
            if (!String.IsNullOrEmpty(value)) UserId = value;

            value = queryGetter("method");
            if (!String.IsNullOrEmpty(value)) Method = value;

            value = queryGetter("statusCode");
            if (!String.IsNullOrEmpty(value) && Int32.TryParse(value, out int statusCode)) StatusCode = statusCode;

            value = queryGetter("pathContains");
            if (!String.IsNullOrEmpty(value)) PathContains = value;

            value = queryGetter("category");
            if (!String.IsNullOrEmpty(value)) Category = value;

            value = queryGetter("fromUtc");
            if (!String.IsNullOrEmpty(value) && DateTime.TryParse(value, out DateTime fromUtc)) FromUtc = fromUtc.ToUniversalTime();

            value = queryGetter("toUtc");
            if (!String.IsNullOrEmpty(value) && DateTime.TryParse(value, out DateTime toUtc)) ToUtc = toUtc.ToUniversalTime();

            value = queryGetter("bucketMinutes");
            if (!String.IsNullOrEmpty(value) && Int32.TryParse(value, out int bucketMinutes)) BucketMinutes = bucketMinutes;
        }

        #endregion
    }
}
