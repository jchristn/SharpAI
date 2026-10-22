namespace SharpAI.Tui.Services
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class ClipboardService : IClipboard
    {
        /// <inheritdoc/>
        public async Task<bool> CopyAsync(string text, CancellationToken cancellationToken)
        {
            string program = OperatingSystem.IsMacOS() ? "pbcopy" : OperatingSystem.IsWindows() ? "powershell.exe" : Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") != null ? "wl-copy" : "xclip";
            ProcessStartInfo start = new ProcessStartInfo(program) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            if (OperatingSystem.IsWindows()) { start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-NonInteractive"); start.ArgumentList.Add("-Command"); start.ArgumentList.Add("[Console]::In.ReadToEnd() | Set-Clipboard"); }
            else if (program == "xclip") { start.ArgumentList.Add("-selection"); start.ArgumentList.Add("clipboard"); }
            try
            {
                using Process? process = Process.Start(start);
                if (process == null) return false;
                using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(3));
                try
                {
                    await process.StandardInput.WriteAsync(text.AsMemory(), timeout.Token).ConfigureAwait(false); process.StandardInput.Close();
                    await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                    return process.ExitCode == 0;
                }
                finally { if (!process.HasExited) process.Kill(true); }
            }
            catch (Exception error) when (error is IOException or System.ComponentModel.Win32Exception or OperationCanceledException) { return false; }
        }
    }
}
