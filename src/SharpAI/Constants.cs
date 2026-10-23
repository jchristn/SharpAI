namespace SharpAI
{
    using System;
    /// <summary>
    /// Constants.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Logo.
        /// Gracias, as always, to: https://patorjk.com/ using font Rectangles
        /// </summary>
        public static string Logo =
            "                                     " + Environment.NewLine +
            "  _____ _               _____ _____  " + Environment.NewLine +
            " |  ___| |_ ___ ___ ___|  _  |_   _| " + Environment.NewLine +
            " |___  |   | .'|  _| . |     |_| |_  " + Environment.NewLine +
            " |_____|_|_|__,|_| |  _|__|__|_____| " + Environment.NewLine +
            "                   |_|               " + Environment.NewLine;

        /// <summary>
        /// Default HTML homepage.
        /// </summary>
        public static string HtmlHomepage =
            @"<html>" + Environment.NewLine +
            @"  <head>" + Environment.NewLine +
            @"    <title>Node is Operational</title>" + Environment.NewLine +
            @"  </head>" + Environment.NewLine +
            @"  <body>" + Environment.NewLine +
            @"    <div>" + Environment.NewLine +
            @"      <pre>" + Environment.NewLine + Environment.NewLine +
            Logo + Environment.NewLine +
            @"      </pre>" + Environment.NewLine +
            @"    </div>" + Environment.NewLine +
            @"    <div style='font-family: Arial, sans-serif;'>" + Environment.NewLine +
            @"      <h2>Your node is operational</h2>" + Environment.NewLine +
            @"      <p>Congratulations, your node is operational.  Please refer to the documentation for use.</p>" + Environment.NewLine +
            @"    <div>" + Environment.NewLine +
            @"  </body>" + Environment.NewLine +
            @"</html>" + Environment.NewLine;

        /// <summary>
        /// Timestamp format.
        /// </summary>
        public static string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.ffffffZ";

        /// <summary>
        /// Settings file.
        /// </summary>
        public static string SettingsFile = "./sharpai.json";

        /// <summary>
        /// Database filename.
        /// </summary>
        public static string DatabaseFile = "./sharpai.db";

        /// <summary>
        /// Log filename.
        /// </summary>
        public static string LogFilename = "./sharpai.log";

        /// <summary>
        /// Log directory.
        /// </summary>
        public static string LogDirectory = "./logs/";

        /// <summary>
        /// Product name.
        /// </summary>
        public static string ProductName = "SharpAI";

        /// <summary>
        /// Copyright.
        /// </summary>
        public static string Copyright = "(c)2025 Joel Christner";

        /// <summary>
        /// Binary content type.
        /// </summary>
        public static string BinaryContentType = "application/octet-stream";

        /// <summary>
        /// JSON content type.
        /// </summary>
        public static string JsonContentType = "application/json";

        /// <summary>
        /// Newline-delimited JSON content type.
        /// </summary>
        public static string NdJsonContentType = "application/x-ndjson";

        /// <summary>
        /// Event-stream content type for server-sent events (SSE).
        /// </summary>
        public static string EventStreamContentType = "text/event-stream";

        /// <summary>
        /// HTML content type.
        /// </summary>
        public static string HtmlContentType = "text/html";

        /// <summary>
        /// PNG content type.
        /// </summary>
        public static string PngContentType = "image/png";

        /// <summary>
        /// Text content type.
        /// </summary>
        public static string TextContentType = "text/plain";

        /// <summary>
        /// Favicon filename.
        /// </summary>
        public static string FaviconFilename = "assets/favicon.ico";

        /// <summary>
        /// Favicon content type.
        /// </summary>
        public static string FaviconContentType = "image/x-icon";

        /// <summary>
        /// HuggingFace API key.
        /// </summary>
        public static string DefaultHuggingFaceApiKey = null;

        /// <summary>
        /// SharpAI request ID header.
        /// </summary>
        public static string RequestIdHeader = "X-SharpAI-Request";
    }
}
