namespace SharpAI.Client
{
    using System;
    using System.Net;

    /// <summary>A bounded public API failure. Contains no request credentials. Immutable and thread safe.</summary>
    public sealed class ApiException : Exception
    {
        /// <summary>HTTP status returned by the server.</summary>
        public HttpStatusCode StatusCode { get; }
        /// <summary>Bounded server error envelope, treated as untrusted text.</summary>
        public string Body { get; }
        /// <summary>Initialize a failure from the public response.</summary>
        public ApiException(HttpStatusCode statusCode, string body)
            : base("HTTP " + (int)statusCode + ": " + statusCode)
        {
            StatusCode = statusCode;
            Body = body;
        }
    }
}
