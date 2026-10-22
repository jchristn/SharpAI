namespace Test.Shared.Tui
{
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Tui.Services;

    internal sealed class TestClipboard : IClipboard
    {
        internal string? Text { get; private set; }
        internal bool Success { get; set; }
        internal TaskCompletionSource<bool>? Pending { get; set; }
        /// <inheritdoc/>
        public Task<bool> CopyAsync(string text, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Text = text; return Pending?.Task ?? Task.FromResult(Success);
        }
    }
}
