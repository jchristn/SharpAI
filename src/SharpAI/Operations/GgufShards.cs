namespace SharpAI.Operations
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using SharpAI.Hosting.HuggingFace;

    /// <summary>Validates split GGUF sets and resolves legacy single-file or directory storage. Thread-safe.</summary>
    public static class GgufShards
    {
        private static readonly Regex _Pattern = new Regex(@"^(.*)-(\d{5})-of-(\d{5})\.gguf$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Select every shard of the chosen model, rejecting incomplete or ambiguous sets.</summary>
        /// <param name="files">Repository files.</param><param name="selected">Selected model file.</param>
        /// <returns>Ordered shard set, or a single unsplit file.</returns>
        public static List<GgufFileInfo> Select(IReadOnlyList<GgufFileInfo> files, GgufFileInfo selected)
        {
            ArgumentNullException.ThrowIfNull(files);
            ArgumentNullException.ThrowIfNull(selected);
            Match match = _Pattern.Match(selected.Path);
            if (!match.Success) return new List<GgufFileInfo> { selected };
            int count = Int32.Parse(match.Groups[3].Value);
            if (count < 1) throw new InvalidDataException("Invalid GGUF shard count.");
            List<GgufFileInfo> result = files.Where(f => {
                Match m = _Pattern.Match(f.Path);
                return m.Success && m.Groups[1].Value == match.Groups[1].Value;
            }).OrderBy(f => f.Path, StringComparer.Ordinal).ToList();
            if (result.Count != count) throw new InvalidDataException("Incomplete GGUF shard set.");
            for (int i = 0; i < count; i++)
            {
                Match m = _Pattern.Match(result[i].Path);
                if (Int32.Parse(m.Groups[2].Value) != i + 1 || Int32.Parse(m.Groups[3].Value) != count)
                    throw new InvalidDataException("Inconsistent GGUF shard numbering.");
            }
            return result;
        }

        /// <summary>Resolve the entry file for registered storage and verify shard completeness.</summary>
        /// <param name="directory">Model storage root.</param><param name="id">Registry identifier.</param>
        /// <returns>Legacy GUID path or first shard.</returns>
        public static string Resolve(string directory, Guid id)
        {
            string path = Path.Combine(directory, id.ToString());
            if (!Directory.Exists(path)) return path;
            List<string> files = Directory.GetFiles(path).Where(f => f.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f, StringComparer.Ordinal).ToList();
            if (files.Count == 0) throw new InvalidDataException("Model directory contains no GGUF files.");
            List<GgufFileInfo> descriptors = files.Select(f => new GgufFileInfo { Path = f }).ToList();
            if (Select(descriptors, descriptors[0]).Count != files.Count) throw new InvalidDataException("Unexpected files in model shard set.");
            return files[0];
        }

        /// <summary>List and validate the files belonging to an entry path.</summary>
        /// <param name="path">Entry file.</param><returns>Ordered model files.</returns>
        public static List<string> Files(string path)
        {
            Match match = _Pattern.Match(path);
            if (!match.Success) return new List<string> { path };
            List<GgufFileInfo> files = Directory.GetFiles(Path.GetDirectoryName(Path.GetFullPath(path))).Where(f => f.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
                .Select(f => new GgufFileInfo { Path = f }).ToList();
            return Select(files, new GgufFileInfo { Path = Path.GetFullPath(path) }).Select(f => f.Path).ToList();
        }

        /// <summary>Delete all registered files after unloading the model.</summary>
        /// <param name="directory">Storage root.</param><param name="id">Registry identifier.</param>
        public static void Delete(string directory, Guid id)
        {
            string path = Path.Combine(directory, id.ToString());
            if (Directory.Exists(path)) Directory.Delete(path, true);
            else File.Delete(path);
        }
    }
}
