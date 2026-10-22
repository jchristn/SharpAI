namespace SharpAI.Publisher
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Formats.Tar;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;

    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            try
            {
                string root = Path.GetFullPath(Option(args, "--root") ?? ".");
                JsonObject manifest = (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(root, "publisher.json")))!;
                string project = Path.Combine(root, manifest["project"]!.ToString());
                string version = XDocument.Load(project).Descendants("Version").First().Value;
                string output = Path.GetFullPath(Option(args, "--output") ?? Path.Combine(root, "installers"));
                string channel = Option(args, "--channel") ?? "archive";
                string rid = Option(args, "--rid") ?? System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
                if (!((JsonArray)manifest["runtimes"]!).Any(item => item!.ToString() == rid)) throw new ArgumentException("Unsupported runtime identifier.");
                Directory.CreateDirectory(output);
                string artifact = manifest["artifact"]!.ToString();
                string folder = Path.Combine(output, artifact + "-" + version + "-" + rid);
                string extension = rid.StartsWith("win", StringComparison.Ordinal) ? ".zip" : ".tar.gz";
                string archive = folder + extension;
                if (channel == "archive")
                {
                    Directory.CreateDirectory(folder);
                    await RunAsync("dotnet", new[] { "publish", project, "-c", "Release", "-f", manifest["framework"]!.ToString(), "-r", rid, "--self-contained", "true", "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true", "-p:DebugType=none", "-o", folder }, CancellationToken.None).ConfigureAwait(false);
                    string binary = Path.Combine(folder, artifact + (rid.StartsWith("win", StringComparison.Ordinal) ? ".exe" : ""));
                    if (rid.StartsWith("osx", StringComparison.Ordinal) && OperatingSystem.IsMacOS()) await RunAsync("codesign", new[] { "--force", "--sign", "-", binary }, CancellationToken.None).ConfigureAwait(false);
                    File.Copy(Path.Combine(root, "LICENSE.md"), Path.Combine(folder, "LICENSE.md"), true);
                    File.Copy(Path.Combine(root, "packaging", "INSTALL.txt"), Path.Combine(folder, "INSTALL.txt"), true);
                    File.WriteAllText(Path.Combine(folder, "NOTICE.txt"), manifest["notice"]!.ToString() + Environment.NewLine);
                    if (File.Exists(archive)) File.Delete(archive);
                    if (extension == ".zip") ZipFile.CreateFromDirectory(folder, archive);
                    else { using FileStream file = File.Create(archive); using GZipStream gzip = new GZipStream(file, CompressionLevel.Optimal); TarFile.CreateFromDirectory(folder, gzip, false); }
                    File.WriteAllText(archive + ".sha256", Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archive))).ToLowerInvariant() + "  " + Path.GetFileName(archive) + Environment.NewLine);
                }
                else
                {
                    if (!File.Exists(archive)) throw new FileNotFoundException("Build the matching archive first.", archive);
                    string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archive))).ToLowerInvariant();
                    string url = manifest["repository"] + "/releases/download/v" + version + "/" + Path.GetFileName(archive);
                    await ChannelAsync(channel, root, output, folder, archive, artifact, version, rid, url, hash, CancellationToken.None).ConfigureAwait(false);
                }
                foreach (string package in Directory.EnumerateFiles(output).Where(path => new[] { ".zip", ".gz", ".dmg", ".exe", ".deb", ".rpm", ".nupkg" }.Contains(Path.GetExtension(path))))
                    File.WriteAllText(package + ".sha256", Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(package))).ToLowerInvariant() + "  " + Path.GetFileName(package) + Environment.NewLine);
                return 0;
            }
            catch (Exception error) when (error is IOException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            { Console.Error.WriteLine(error.Message); return 1; }
        }
        private static async Task ChannelAsync(string channel, string root, string output, string folder, string archive, string artifact, string version, string rid, string url, string hash, CancellationToken ct)
        {
            if (channel is "winget" or "choco" or "inno" && !rid.StartsWith("win-", StringComparison.Ordinal)) throw new ArgumentException("This channel requires a Windows runtime.");
            if (channel is "brew" or "dmg" && !rid.StartsWith("osx-", StringComparison.Ordinal)) throw new ArgumentException("This channel requires a macOS runtime.");
            if (channel is "deb" or "rpm" && !rid.StartsWith("linux-", StringComparison.Ordinal)) throw new ArgumentException("This channel requires a Linux runtime.");
            if (channel == "brew")
            {
                string formulaDirectory = Path.Combine(output, "brew", rid); Directory.CreateDirectory(formulaDirectory);
                File.WriteAllText(Path.Combine(formulaDirectory, artifact + ".rb"), "class SharpaiTui < Formula\n  desc \"SharpAI terminal dashboard\"\n  homepage \"https://github.com/jchristn/SharpAI\"\n  url \"" + url + "\"\n  sha256 \"" + hash + "\"\n  version \"" + version + "\"\n  license \"MIT\"\n  def install\n    bin.install \"sharpai-tui\"\n  end\n  test do\n    assert_match \"sharpai-tui\", shell_output(\"#{bin}/sharpai-tui --version\")\n  end\nend\n");
            }
            else if (channel == "winget")
            {
                string arch = rid.EndsWith("arm64", StringComparison.Ordinal) ? "arm64" : "x64";
                File.WriteAllText(Path.Combine(output, "SharpAI.Tui." + arch + ".yaml"), "PackageIdentifier: SharpAI.Tui\nPackageVersion: " + version + "\nPackageLocale: en-US\nPublisher: Joel Christner\nPackageName: SharpAI TUI\nLicense: MIT\nShortDescription: SharpAI terminal dashboard\nInstallers:\n  - Architecture: " + arch + "\n    InstallerType: zip\n    NestedInstallerType: portable\n    NestedInstallerFiles:\n      - RelativeFilePath: sharpai-tui.exe\n        PortableCommandAlias: sharpai-tui\n    InstallerUrl: " + url + "\n    InstallerSha256: " + hash.ToUpperInvariant() + "\nManifestType: singleton\nManifestVersion: 1.9.0\n");
            }
            else if (channel == "choco")
            {
                string package = Path.Combine(output, "choco-" + rid); Directory.CreateDirectory(Path.Combine(package, "tools"));
                File.Copy(Path.Combine(root, "packaging", "sharpai-tui.nuspec"), Path.Combine(package, "sharpai-tui.nuspec"), true);
                foreach (string payload in Directory.EnumerateFiles(folder)) File.Copy(payload, Path.Combine(package, "tools", Path.GetFileName(payload)), true);
                string obsoleteScript = Path.Combine(package, "tools", "chocolateyinstall.ps1"); if (File.Exists(obsoleteScript)) File.Delete(obsoleteScript);
                await RunAsync("choco", new[] { "pack", Path.Combine(package, "sharpai-tui.nuspec"), "--version", version, "--outputdirectory", output }, ct).ConfigureAwait(false);
            }
            else if (channel == "inno")
                await RunAsync("iscc", new[] { "/DAppVersion=" + version, "/DRuntimeIdentifier=" + rid, "/DSourceDirectory=" + folder, "/DOutputDirectory=" + output, Path.Combine(root, "packaging", "sharpai-tui.iss") }, ct).ConfigureAwait(false);
            else if (channel == "dmg")
                await RunAsync("hdiutil", new[] { "create", "-volname", "SharpAI TUI", "-srcfolder", folder, "-ov", "-format", "UDZO", Path.Combine(output, Path.GetFileName(folder) + ".dmg") }, ct).ConfigureAwait(false);
            else if (channel is "deb" or "rpm")
            {
                string arch = rid.EndsWith("arm64", StringComparison.Ordinal) ? (channel == "deb" ? "arm64" : "aarch64") : (channel == "deb" ? "amd64" : "x86_64");
                await RunAsync("fpm", new[] { "--force", "-s", "dir", "-t", channel, "-n", artifact, "-v", version, "-a", arch, "--license", "MIT", "--description", "SharpAI terminal dashboard", "--url", "https://github.com/jchristn/SharpAI", "-p", output, Path.Combine(folder, artifact) + "=/usr/bin/" + artifact, Path.Combine(folder, "LICENSE.md") + "=/usr/share/doc/" + artifact + "/LICENSE.md", Path.Combine(folder, "INSTALL.txt") + "=/usr/share/doc/" + artifact + "/INSTALL.txt", Path.Combine(folder, "NOTICE.txt") + "=/usr/share/doc/" + artifact + "/NOTICE.txt" }, ct).ConfigureAwait(false);
            }
            else throw new ArgumentException("Unknown channel: " + channel);
        }
        private static async Task RunAsync(string executable, IEnumerable<string> arguments, CancellationToken ct)
        {
            ProcessStartInfo start = new ProcessStartInfo(executable) { UseShellExecute = false };
            foreach (string argument in arguments) start.ArgumentList.Add(argument);
            using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not start " + executable);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            if (process.ExitCode != 0) throw new InvalidOperationException(executable + " exited with " + process.ExitCode);
        }
        private static string? Option(string[] args, string name)
        {
            int index = Array.IndexOf(args, name); if (index < 0) return null;
            if (index + 1 == args.Length) throw new ArgumentException("Missing value for " + name);
            return args[index + 1];
        }
    }
}
