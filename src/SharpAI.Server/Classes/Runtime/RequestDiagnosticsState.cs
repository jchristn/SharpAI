namespace SharpAI.Server.Classes.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using SharpAI.Operations;

    /// <summary>Single-request diagnostic state. Routing owns writes; post-routing reads after completion.</summary>
    public sealed class RequestDiagnosticsState
    {
        private readonly int _MaximumBytes;
        private readonly List<byte> _Body = new List<byte>();
        /// <summary>Engine measurements, null until an engine is acquired.</summary>
        public InferenceStatistics Statistics { get; set; }
        /// <summary>Original response byte count, including bytes omitted from the diagnostic copy.</summary>
        public long ResponseBytes { get; private set; }
        /// <summary>Bounded retained response text.</summary>
        public string ResponseBody { get { return Encoding.UTF8.GetString(_Body.ToArray()); } }
        /// <summary>Whether diagnostic content was omitted.</summary>
        public bool Truncated { get { return ResponseBytes > _Body.Count; } }
        /// <summary>Create a bounded capture. Maximum is clamped to 0 through 1 MiB.</summary>
        public RequestDiagnosticsState(int maximumBytes) { _MaximumBytes = Math.Clamp(maximumBytes, 0, 1024 * 1024); }
        /// <summary>Append successfully emitted response bytes without retaining unbounded model output.</summary>
        public void Append(byte[] bytes)
        {
            ResponseBytes += bytes.Length;
            int available = Math.Max(0, _MaximumBytes - (int)_Body.Count);
            for (int i = 0; i < Math.Min(available, bytes.Length); i++) _Body.Add(bytes[i]);
        }
    }
}
