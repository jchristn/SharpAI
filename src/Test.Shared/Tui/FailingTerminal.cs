namespace Test.Shared.Tui
{
    using System.IO;
    using TUIKit;
    using TUIKit.Terminal;

    internal sealed class FailingTerminal : ITerminalBackend, ISharedTerminalBackend
    {
        private readonly HeadlessBackend _Inner = new HeadlessBackend(120, 40) { ClaimsTerminal = false };
        private bool _Failed;
        internal bool Restored { get { return _Inner.IsStopped; } }
        /// <inheritdoc/>
        public TerminalCapabilities Capabilities { get { return _Inner.Capabilities; } }
        /// <inheritdoc/>
        public Size Size { get { return _Inner.Size; } }
        /// <inheritdoc/>
        public bool IsInteractive { get { return true; } }
        /// <inheritdoc/>
        public bool ClaimsTerminal { get { return false; } }
        /// <inheritdoc/>
        public void Start() { _Inner.Start(); }
        /// <inheritdoc/>
        public void Write(string data) { if (!_Failed) { _Failed = true; throw new IOException("Injected terminal write failure"); } _Inner.Write(data); }
        /// <inheritdoc/>
        public void Flush() { _Inner.Flush(); }
        /// <inheritdoc/>
        public int ReadInput(byte[] buffer, int offset, int count) { return _Inner.ReadInput(buffer, offset, count); }
        /// <inheritdoc/>
        public void Stop() { _Inner.Stop(); }
        /// <inheritdoc/>
        public void Dispose() { _Inner.Dispose(); }
    }
}
