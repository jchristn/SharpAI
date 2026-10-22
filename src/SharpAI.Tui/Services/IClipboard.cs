namespace SharpAI.Tui.Services
{
    using System.Threading;
    using System.Threading.Tasks;

    internal interface IClipboard
    {
        Task<bool> CopyAsync(string text, CancellationToken cancellationToken);
    }
}
