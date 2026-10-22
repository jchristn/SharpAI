namespace Test.Xunit
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using SharpAI.Classes.Runtime;
    using Xunit;

    /// <summary>Backend selection works in the library without referencing the server.</summary>
    public sealed class NativeBackendSelectionTests : IDisposable
    {
        private readonly string _Directory = Path.Combine(Path.GetTempPath(), "sharpai-backend-" + Guid.NewGuid().ToString("N"));

        /// <summary>Create an isolated native package layout.</summary>
        public NativeBackendSelectionTests() { Directory.CreateDirectory(_Directory); }

        /// <summary>Packaged and custom Metal layouts both enable Apple Silicon acceleration.</summary>
        [Theory]
        [InlineData("osx-arm64/native")]
        [InlineData("metal")]
        public void AppleSiliconSelectsMetalFromInstalledLibraries(string layout)
        {
            string directory = Path.Combine(_Directory, "runtimes", layout);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "libllama.dylib"), "fixture");
            File.WriteAllText(Path.Combine(directory, "libggml-metal.dylib"), "fixture");
            Assert.Equal("metal", NativeBackendInitializer.SelectAutomaticBackend(OSPlatform.OSX, Architecture.Arm64, _Directory, false));
            Assert.Equal("cpu", NativeBackendInitializer.SelectAutomaticBackend(OSPlatform.OSX, Architecture.X64, _Directory, true));
        }

        /// <summary>Neither a missing llama binary nor a missing Metal plugin claims acceleration.</summary>
        [Theory]
        [InlineData("libllama.dylib")]
        [InlineData("libggml-metal.dylib")]
        public void IncompleteMetalPackageUsesCpu(string presentFile)
        {
            string directory = Path.Combine(_Directory, "runtimes", "osx-arm64", "native");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, presentFile), "fixture");
            Assert.Equal("cpu", NativeBackendInitializer.SelectAutomaticBackend(OSPlatform.OSX, Architecture.Arm64, _Directory, false));
        }

        /// <summary>Non-Mac platforms preserve CUDA detection and CPU fallback.</summary>
        [Theory]
        [InlineData(false, "cpu")]
        [InlineData(true, "cuda")]
        public void OtherPlatformsUseTheirDetectedBackend(bool cudaAvailable, string expected)
        {
            Assert.Equal(expected, NativeBackendInitializer.SelectAutomaticBackend(OSPlatform.Linux, Architecture.X64, _Directory, cudaAvailable));
            Assert.Equal(expected, NativeBackendInitializer.SelectAutomaticBackend(OSPlatform.Windows, Architecture.X64, _Directory, cudaAvailable));
        }

        /// <summary>Bad explicit configuration is rejected before process-wide selection changes.</summary>
        [Fact]
        public void InvalidBackendDoesNotChangeTheSelectedLibrary()
        {
            string previousPath = NativeLibraryLoader.ConfiguredLibraryPath;
            string previousBackend = NativeBackendInfo.SelectedBackend;
            Assert.Throws<ArgumentException>(() => NativeLibraryLoader.Configure("libllama", "invalid"));
            Assert.Equal(previousPath, NativeLibraryLoader.ConfiguredLibraryPath);
            Assert.Equal(previousBackend, NativeBackendInfo.SelectedBackend);
        }

        /// <summary>Remove only this test's package fixtures.</summary>
        public void Dispose() { Directory.Delete(_Directory, true); }
    }
}
