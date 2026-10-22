namespace SharpAI.Operations
{
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>Immutable validated Hugging Face source. Parsing never contacts a host or exposes credentials. Thread safe.</summary>
    public sealed class ModelPullSource
    {
        /// <summary>Repository identifier, organization/name.</summary>
        public string Repository { get; }
        /// <summary>Requested revision, or null for the repository default revision.</summary>
        public string? Revision { get; }
        /// <summary>Exact repository-relative GGUF path, or null for quantization selection.</summary>
        public string? FilePath { get; }
        private ModelPullSource(string repository, string? revision, string? filePath)
        {
            Repository = repository; Revision = revision; FilePath = filePath;
        }
        /// <summary>Parse organization/name, a repository HTTPS URL, a tree revision URL, or a resolve/blob GGUF URL.</summary>
        /// <param name="input">Nonempty source without user information, query strings or fragments.</param>
        /// <returns>A source restricted to Hugging Face. Network requests still use the configured repository client origin.</returns>
        /// <exception cref="ArgumentException">Input is invalid, ambiguous or points at an unsupported host/path.</exception>
        public static ModelPullSource Parse(string input)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(input);
            string name = input.Trim().TrimEnd('/');
            string? revision = null; string? filePath = null;
            if (name.Contains("://", StringComparison.Ordinal))
            {
                if (!Uri.TryCreate(name, UriKind.Absolute, out Uri url) || url.Scheme != "https" || !url.IsDefaultPort
                    || !new[] { "huggingface.co", "www.huggingface.co", "hf.co" }.Contains(url.Host, StringComparer.OrdinalIgnoreCase)
                    || !string.IsNullOrEmpty(url.UserInfo) || !string.IsNullOrEmpty(url.Query) || !string.IsNullOrEmpty(url.Fragment)
                    || name.Contains('\\') || Regex.IsMatch(name, @"/(?:\.|%2e){1,2}(?:/|$)", RegexOptions.IgnoreCase))
                    throw new ArgumentException("Use a Hugging Face HTTPS repository or GGUF URL without credentials, query or fragment.", nameof(input));
                string[] parts = url.AbsolutePath.Trim('/').Split('/').Select(Uri.UnescapeDataString).ToArray();
                if (parts.Length < 2 || parts.Any(part => part.Length == 0 || part is "." or ".." || part.Contains('\\'))) throw new ArgumentException("Invalid Hugging Face path.", nameof(input));
                name = parts[0] + "/" + parts[1];
                if (parts.Length > 2)
                {
                    if (parts.Length < 4 || parts[2] is not ("tree" or "resolve" or "blob")) throw new ArgumentException("Use a repository, tree revision, or resolve/blob GGUF URL.", nameof(input));
                    revision = parts[3];
                    if (parts[2] == "tree" && parts.Length != 4) throw new ArgumentException("Tree URLs must select a revision, not a subdirectory.", nameof(input));
                    if (parts[2] != "tree")
                    {
                        if (parts.Length < 5 || !parts[parts.Length - 1].EndsWith(".gguf", StringComparison.OrdinalIgnoreCase) || parts.Skip(4).Any(part => part.Contains('/'))) throw new ArgumentException("File URLs must identify an exact GGUF file.", nameof(input));
                        filePath = string.Join("/", parts.Skip(4));
                    }
                }
            }
            if (!Regex.IsMatch(name, @"^[A-Za-z0-9_][A-Za-z0-9_.-]*/[A-Za-z0-9_][A-Za-z0-9_.-]*$")) throw new ArgumentException("Use an organization/name repository identifier.", nameof(input));
            return new ModelPullSource(name, revision, filePath);
        }
    }
}
