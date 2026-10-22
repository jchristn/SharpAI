namespace Test.HuggingFace
{
    using GetSomeInput;
    using SharpAI.Hosting;
    using SharpAI.Hosting.HuggingFace;
    using SharpAI.Serialization;
    using SyslogLogging;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Test application for exercising the HuggingFace model files client.
    /// </summary>
    public class Program
    {
        #region Private-Members

        private static readonly string _Header = "[Test.HuggingFace] ";
        private static LoggingModule _Logging;
        private static HuggingFaceClient _HuggingFaceClient;
        private static Serializer _Serializer = new Serializer();

        #endregion

        #region Public-Methods

        /// <summary>
        /// Main entry point for the test application.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public static async Task Main(string[] args)
        {
            try
            {
                Console.Clear();
                Console.WriteLine();
                Console.WriteLine("This application allows you to explore HuggingFace model repositories");
                Console.WriteLine("and analyze GGUF files available for download.");
                Console.WriteLine();

                Console.WriteLine("Retrieve your HuggingFace API key; these can be generated at https://huggingface.co/settings/tokens");
                string apiKey = Inputty.GetString("HuggingFace API key:", null, false);

                _Logging = new LoggingModule();
                _Logging.Settings.EnableColors = true;
                _Logging.Settings.EnableConsole = true;

                _HuggingFaceClient = new HuggingFaceClient(_Logging, apiKey);

                bool runForever = true;

                while (runForever)
                {
                    try
                    {
                        Console.WriteLine();

                        string modelName = GetModelName();
                        if (string.IsNullOrEmpty(modelName)) break;

                        await ExploreModel(modelName);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception:" + Environment.NewLine + ex.ToString());
                        Console.WriteLine();
                    }
                }
            }
            catch (Exception ex)
            {
                _Logging?.Error(_Header + $"unhandled exception:{Environment.NewLine}{ex.ToString()}");
                Console.WriteLine($"An error occurred:{Environment.NewLine}{ex.ToString()}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }

        #endregion

        #region Private-Methods
                
        private static string GetModelName()
        {
            Console.WriteLine("Enter a HuggingFace model name:");
            Console.WriteLine("Examples:");
            Console.WriteLine("  - microsoft/DialoGPT-medium");
            Console.WriteLine("  - microsoft/phi-2");
            Console.WriteLine("  - TheBloke/Llama-2-7B-Chat-GGUF");
            Console.WriteLine("  - bartowski/Meta-Llama-3.1-8B-Instruct-GGUF");
            Console.WriteLine();

            string modelName = Inputty.GetString("Model name [empty to quit]:", null, true);
            return modelName?.Trim();
        }

        private static async Task ExploreModel(string modelName)
        {
            Console.WriteLine();
            Console.WriteLine("Retrieving model metadata");
            Console.WriteLine(_Serializer.SerializeJson(_HuggingFaceClient.GetModelMetadata(modelName).Result, true));

            Console.WriteLine();
            Console.WriteLine($"Retrieving files for model: {modelName}");
            List<HuggingFaceModelFile> allFiles = await _HuggingFaceClient.GetModelFilesAsync(modelName);

            DisplayAllFiles(allFiles);

            Console.WriteLine();
            Console.WriteLine("Analyzing GGUF files");
            List<GgufFileInfo> ggufFiles = await _HuggingFaceClient.GetGgufFilesAsync(modelName);

            if (ggufFiles.Count == 0)
            {
                Console.WriteLine("  No GGUF files found in this model repository.");
                return;
            }

            DisplayGgufAnalysis(ggufFiles);
            await DisplayDetailedGgufAnalysis(modelName, ggufFiles);

            // Offer file downloads
            bool downloadFiles = Inputty.GetBoolean("Download files?", false);
            if (downloadFiles) await HandleFileDownloads(modelName, allFiles, ggufFiles);
        }

        private static void DisplayAllFiles(List<HuggingFaceModelFile> allFiles)
        {
            Console.WriteLine();
            Console.WriteLine($"Repository Summary:");
            Console.WriteLine($"   Total files: {allFiles.Count}");

            List<HuggingFaceModelFile> files = allFiles.Where(f => f.Type == "file").ToList();
            List<HuggingFaceModelFile> directories = allFiles.Where(f => f.Type == "directory").ToList();

            Console.WriteLine($"   Files: {files.Count}");
            Console.WriteLine($"   Directories: {directories.Count}");

            Dictionary<string, int> extensions = new Dictionary<string, int>();
            foreach (HuggingFaceModelFile file in files)
            {
                if (!string.IsNullOrEmpty(file.Path))
                {
                    string extension = Path.GetExtension(file.Path).ToLowerInvariant();
                    if (string.IsNullOrEmpty(extension))
                        extension = "(null)";

                    if (!extensions.ContainsKey(extension))
                        extensions[extension] = 0;
                    extensions[extension]++;
                }
            }

            Console.WriteLine();
            Console.WriteLine("File Types:");
            foreach (KeyValuePair<string, int> kvp in extensions.OrderByDescending(x => x.Value).Take(10))
            {
                Console.WriteLine($"   {kvp.Key}: {kvp.Value} files");
            }

            List<HuggingFaceModelFile> largestFiles = files
                .Where(f => f.Size.HasValue)
                .OrderByDescending(f => f.Size.Value)
                .Take(5)
                .ToList();

            if (largestFiles.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Largest Files:");
                foreach (HuggingFaceModelFile file in largestFiles)
                {
                    string formattedSize = FormatFileSize(file.Size);
                    Console.WriteLine($"   {Path.GetFileName(file.Path)} - {formattedSize}");
                }
            }
        }

        private static void DisplayGgufAnalysis(List<GgufFileInfo> ggufFiles)
        {
            Console.WriteLine();
            Console.WriteLine($"Found {ggufFiles.Count} GGUF files:");

            foreach (GgufFileInfo file in ggufFiles)
            {
                string fileName = Path.GetFileName(file.Path);
                string indicator = file.IsMainModel ? "🎯" : "🔗";
                Console.WriteLine($"   {indicator} {fileName}");
                Console.WriteLine($"      Size: {file.SizeFormatted}");
                Console.WriteLine($"      Quantization: {file.QuantizationType}");
                Console.WriteLine();
            }
        }

        private static async Task DisplayDetailedGgufAnalysis(string modelName, List<GgufFileInfo> ggufFiles)
        {
            Console.WriteLine();
            Console.WriteLine("Detailed GGUF Analysis");

            List<string> quantTypes = await _HuggingFaceClient.GetAvailableQuantizationTypesAsync(modelName);
            Console.WriteLine();
            Console.WriteLine($"Available Quantization Types ({quantTypes.Count}):");
            foreach (string quant in quantTypes)
            {
                List<GgufFileInfo> filesOfType = ggufFiles.Where(f => f.QuantizationType == quant).ToList();
                Console.WriteLine($"   - {quant}: {filesOfType.Count} files");
            }

            if (quantTypes.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Available quantization types:");
                for (int i = 0; i < quantTypes.Count; i++)
                    Console.WriteLine($"   {i + 1}. {quantTypes[i]}");

                int choice = Inputty.GetInteger("Select quantization type (number):", 1, true, false);
                if (choice >= 1 && choice <= quantTypes.Count)
                {
                    string selectedQuant = quantTypes[choice - 1];
                    List<GgufFileInfo> filteredFiles = await _HuggingFaceClient.GetGgufFilesByQuantizationAsync(modelName, selectedQuant);

                    Console.WriteLine();
                    Console.WriteLine($"Files with {selectedQuant} quantization ({filteredFiles.Count}):");
                    foreach (GgufFileInfo file in filteredFiles)
                    {
                        Console.WriteLine($"   - {Path.GetFileName(file.Path)} ({file.SizeFormatted})");
                    }
                }
            }
        }

        private static async Task HandleFileDownloads(string modelName, List<HuggingFaceModelFile> allFiles, List<GgufFileInfo> ggufFiles)
        {
            Console.WriteLine();

            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("Select download option:");
                Console.WriteLine("   1. Download specific files (choose from all files)");
                Console.WriteLine("   2. Download GGUF files only");
                Console.WriteLine("   3. Return to main menu");

                int choice = Inputty.GetInteger("Your choice", 1, true, false);

                switch (choice)
                {
                    case 1:
                        await DownloadSpecificFiles(modelName, allFiles);
                        break;
                    case 2:
                        await DownloadGgufFiles(modelName, ggufFiles);
                        break;
                    case 3:
                        return;
                    default:
                        Console.WriteLine("Invalid choice. Please select 1-3.");
                        break;
                }

                bool continueDownloading = Inputty.GetBoolean("Continue?", false);
                if (!continueDownloading) break;
            }
        }

        private static async Task DownloadSpecificFiles(string modelName, List<HuggingFaceModelFile> allFiles)
        {
            Console.WriteLine();
            Console.WriteLine("Available Files:");

            List<HuggingFaceModelFile> files = allFiles.Where(f => f.Type == "file").ToList();

            if (files.Count == 0)
            {
                Console.WriteLine("No files available for download.");
                return;
            }

            for (int i = 0; i < files.Count; i++) // Limit to first 50 files
            {
                HuggingFaceModelFile file = files[i];
                string fileName = Path.GetFileName(file.Path);
                string fileSize = FormatFileSize(file.Size);
                Console.WriteLine($"   {i + 1:D2}. {fileName} ({fileSize})");
            }

            Console.WriteLine();
            string input = Inputty.GetString("File numbers (comma-separated, i.e. 1,3,5):", null, true);

            if (string.IsNullOrWhiteSpace(input)) return;

            List<int> selectedNumbers = ParseNumberList(input, files.Count);
            if (selectedNumbers.Count == 0)
            {
                Console.WriteLine("No valid file numbers selected.");
                return;
            }

            string downloadDir = GetDownloadDirectory();
            if (string.IsNullOrEmpty(downloadDir)) return;

            List<HuggingFaceModelFile> selectedFiles = selectedNumbers.Select(num => files[num - 1]).ToList();
            await DownloadSelectedFiles(modelName, selectedFiles, downloadDir);
        }

        private static async Task DownloadGgufFiles(string modelName, List<GgufFileInfo> ggufFiles)
        {
            if (ggufFiles.Count == 0)
            {
                Console.WriteLine("No GGUF files available for download.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Available GGUF Files:");

            for (int i = 0; i < ggufFiles.Count; i++)
            {
                GgufFileInfo file = ggufFiles[i];
                string fileName = Path.GetFileName(file.Path);
                string indicator = file.IsMainModel ? "🎯" : "🔗";
                Console.WriteLine($"   {i + 1:D2}. {indicator} {fileName} ({file.SizeFormatted}) - {file.QuantizationType}");
            }

            Console.WriteLine();
            string input = Inputty.GetString("File numbers to download (comma-separated, i.e. 1,3,5):", null, true);

            if (string.IsNullOrWhiteSpace(input))return;

            List<int> selectedNumbers = ParseNumberList(input, ggufFiles.Count);
            if (selectedNumbers.Count == 0)
            {
                Console.WriteLine("No valid file numbers selected.");
                return;
            }

            string downloadDir = GetDownloadDirectory();
            if (string.IsNullOrEmpty(downloadDir)) return;

            List<HuggingFaceModelFile> selectedFiles = selectedNumbers.Select(num => (HuggingFaceModelFile)ggufFiles[num - 1]).ToList();
            await DownloadSelectedFiles(modelName, selectedFiles, downloadDir);
        }

        private static async Task DownloadByFileType(string modelName, List<HuggingFaceModelFile> allFiles)
        {
            List<HuggingFaceModelFile> files = allFiles.Where(f => f.Type == "file").ToList();

            Dictionary<string, List<HuggingFaceModelFile>> filesByExtension = new Dictionary<string, List<HuggingFaceModelFile>>();

            foreach (HuggingFaceModelFile file in files)
            {
                string extension = Path.GetExtension(file.Path)?.ToLowerInvariant();
                if (string.IsNullOrEmpty(extension))
                    extension = "(no extension)";

                if (!filesByExtension.ContainsKey(extension))
                    filesByExtension[extension] = new List<HuggingFaceModelFile>();

                filesByExtension[extension].Add(file);
            }

            Console.WriteLine();
            Console.WriteLine("Available File Types:");

            List<string> extensions = filesByExtension.Keys.OrderByDescending(k => filesByExtension[k].Count).ToList();
            for (int i = 0; i < extensions.Count; i++)
            {
                string ext = extensions[i];
                int count = filesByExtension[ext].Count;
                Console.WriteLine($"   {i + 1:D2}. {ext} ({count} files)");
            }

            Console.WriteLine();
            int choice = Inputty.GetInteger("Select file type to download:", 1, true, false);

            if (choice < 1 || choice > extensions.Count)
            {
                Console.WriteLine("Invalid choice.");
                return;
            }

            string selectedExtension = extensions[choice - 1];
            List<HuggingFaceModelFile> selectedFiles = filesByExtension[selectedExtension];

            Console.WriteLine($"Selected {selectedFiles.Count} files with extension '{selectedExtension}':");
            foreach (HuggingFaceModelFile file in selectedFiles)
            {
                Console.WriteLine($"   - {Path.GetFileName(file.Path)} ({FormatFileSize(file.Size)})");
            }

            string downloadDir = GetDownloadDirectory();
            if (string.IsNullOrEmpty(downloadDir)) return;

            await DownloadSelectedFiles(modelName, selectedFiles, downloadDir);
        }

        private static async Task DownloadSelectedFiles(string modelName, List<HuggingFaceModelFile> files, string downloadDir)
        {
            Console.WriteLine();
            Console.WriteLine($"Starting download of {files.Count} files");

            // Show download plan and test URLs
            Console.WriteLine("Download Plan:");
            foreach (HuggingFaceModelFile file in files)
            {
                List<string> urls = _HuggingFaceClient.GetDownloadUrls(modelName, file);
                Console.WriteLine($"   - {Path.GetFileName(file.Path)} ← {file.Path}");
                Console.WriteLine($"     Primary URL: {urls[0]}");

                if (urls.Count > 0)
                {
                    bool isAccessible = await _HuggingFaceClient.TestDownloadUrlAsync(urls[0]);
                    Console.WriteLine($"     Status: {(isAccessible ? "✅ accessible" : "❌ not accessible")}");
                }
            }

            Console.WriteLine();

            Action<string, long, decimal> progressCallback = (filename, bytesDownloaded, percentComplete) =>
            {
                if (percentComplete < 0)
                {
                    Console.WriteLine($"\n\n❌ Download failed: {filename}");
                }
                else if (percentComplete >= 1.0m)
                {
                    Console.WriteLine($"\n\n✅ Download complete: {filename}");
                    Console.WriteLine($"   Total size: {bytesDownloaded}");
                }
                else
                {
                    Console.Write($"\rDownloaded: {bytesDownloaded}        ");
                }
            };

            int successCount = await _HuggingFaceClient.DownloadFilesAsync(modelName, files, downloadDir, progressCallback);

            Console.WriteLine();
            Console.WriteLine("Download Summary:");
            Console.WriteLine($"   ✅ Successful: {successCount}");
            Console.WriteLine($"   ❌ Failed: {files.Count - successCount}");
            Console.WriteLine($"   📁 Downloaded to: {downloadDir}");
        }

        private static string GetDownloadDirectory()
        {
            Console.WriteLine();
            string defaultDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "HuggingFace");

            Console.WriteLine($"Default download directory: {defaultDir}");
            string downloadDir = Inputty.GetString("Download directory (press Enter for default)", defaultDir, true);

            if (string.IsNullOrWhiteSpace(downloadDir))
                downloadDir = defaultDir;

            try
            {
                if (!Directory.Exists(downloadDir))
                {
                    bool createDir = Inputty.GetBoolean($"Directory '{downloadDir}' doesn't exist. Create it?", true);
                    if (createDir)
                    {
                        Directory.CreateDirectory(downloadDir);
                        Console.WriteLine($"✅ Created directory: {downloadDir}");
                    }
                    else
                    {
                        return null;
                    }
                }

                return downloadDir;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error with download directory:{Environment.NewLine}{ex.ToString()}");
                return null;
            }
        }

        private static string FormatFileSize(long? bytes)
        {
            if (!bytes.HasValue)
                return "Unknown";

            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            if (bytes >= GB)
                return $"{bytes.Value / (double)GB:F2} GB";
            if (bytes >= MB)
                return $"{bytes.Value / (double)MB:F2} MB";
            if (bytes >= KB)
                return $"{bytes.Value / (double)KB:F2} KB";

            return $"{bytes.Value} bytes";
        }

        private static List<int> ParseNumberList(string input, int maxCount)
        {
            List<int> numbers = new List<int>();

            string[] parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (string part in parts)
            {
                if (int.TryParse(part.Trim(), out int number))
                {
                    if (number >= 1 && number <= maxCount)
                    {
                        if (!numbers.Contains(number))
                            numbers.Add(number);
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Number {number} is out of range (1-{maxCount})");
                    }
                }
                else
                {
                    Console.WriteLine($"Warning: '{part.Trim()}' is not a valid number");
                }
            }

            return numbers.OrderBy(n => n).ToList();
        }

        #endregion
    }
}