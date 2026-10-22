namespace Test.Shared.Tui
{
    using System;
    using System.IO;
    using SharpAI.Tui.Services;

    internal sealed class TestProfileStore : IDisposable
    {
        private readonly string _Directory = Path.Combine(Path.GetTempPath(), "sharpai-tui-test-" + Guid.NewGuid().ToString("N"));
        internal ProfileStore Store { get; }
        internal TestProfileStore() { Store = new ProfileStore(_Directory); }
        /// <inheritdoc/>
        public void Dispose() { if (Directory.Exists(_Directory)) Directory.Delete(_Directory, true); }
    }
}
