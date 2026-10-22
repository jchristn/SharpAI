namespace SharpAI.Operations
{
    using System.Collections.Generic;

    /// <summary>Metadata-only preflight and conservative peak allocation estimate. Observed memory is not implied.</summary>
    public sealed class ModelInspection
    {
        private string _Architecture = null;
        /// <summary>Architecture; zero or null means unavailable. Memory values use bytes.</summary>
        public string Architecture { get { return _Architecture; } internal set { _Architecture = value; } }
        private long _WeightsBytes = 0;
        /// <summary>WeightsBytes; zero or null means unavailable. Memory values use bytes.</summary>
        public long WeightsBytes { get { return _WeightsBytes; } internal set { _WeightsBytes = value; } }
        private long _KvBytesPerSlot = 0;
        /// <summary>KvBytesPerSlot; zero or null means unavailable. Memory values use bytes.</summary>
        public long KvBytesPerSlot { get { return _KvBytesPerSlot; } internal set { _KvBytesPerSlot = value; } }
        private long _BuffersBytes = 0;
        /// <summary>BuffersBytes; zero or null means unavailable. Memory values use bytes.</summary>
        public long BuffersBytes { get { return _BuffersBytes; } internal set { _BuffersBytes = value; } }
        private long _HeadroomBytes = 0;
        /// <summary>HeadroomBytes; zero or null means unavailable. Memory values use bytes.</summary>
        public long HeadroomBytes { get { return _HeadroomBytes; } internal set { _HeadroomBytes = value; } }
        private long _TotalBytes = 0;
        /// <summary>TotalBytes; zero or null means unavailable. Memory values use bytes.</summary>
        public long TotalBytes { get { return _TotalBytes; } internal set { _TotalBytes = value; } }
        private long _HostBytes = 0;
        /// <summary>HostBytes; zero or null means unavailable. Memory values use bytes.</summary>
        public long HostBytes { get { return _HostBytes; } internal set { _HostBytes = value; } }
        private long _DeviceBytes = 0;
        /// <summary>DeviceBytes; zero or null means unavailable. Memory values use bytes.</summary>
        public long DeviceBytes { get { return _DeviceBytes; } internal set { _DeviceBytes = value; } }
        private int _TrainedContext = 0;
        /// <summary>TrainedContext; zero or null means unavailable. Memory values use bytes.</summary>
        public int TrainedContext { get { return _TrainedContext; } internal set { _TrainedContext = value; } }
        private int _EffectiveContext = 0;
        /// <summary>EffectiveContext; zero or null means unavailable. Memory values use bytes.</summary>
        public int EffectiveContext { get { return _EffectiveContext; } internal set { _EffectiveContext = value; } }
        private int _Slots = 1;
        /// <summary>Slots; zero or null means unavailable. Memory values use bytes.</summary>
        public int Slots { get { return _Slots; } internal set { _Slots = value; } }
        private int _Layers = 0;
        /// <summary>Layers; zero or null means unavailable. Memory values use bytes.</summary>
        public int Layers { get { return _Layers; } internal set { _Layers = value; } }
        private int _Heads = 0;
        /// <summary>Heads; zero or null means unavailable. Memory values use bytes.</summary>
        public int Heads { get { return _Heads; } internal set { _Heads = value; } }
        private int _KvHeads = 0;
        /// <summary>KvHeads; zero or null means unavailable. Memory values use bytes.</summary>
        public int KvHeads { get { return _KvHeads; } internal set { _KvHeads = value; } }
        private long _ParameterCount = 0;
        /// <summary>ParameterCount; zero or null means unavailable. Memory values use bytes.</summary>
        public long ParameterCount { get { return _ParameterCount; } internal set { _ParameterCount = value; } }
        private uint _QuantizationType = 0;
        /// <summary>QuantizationType; zero or null means unavailable. Memory values use bytes.</summary>
        public uint QuantizationType { get { return _QuantizationType; } internal set { _QuantizationType = value; } }
        private bool _Supported = true;
        /// <summary>Supported; zero or null means unavailable. Memory values use bytes.</summary>
        public bool Supported { get { return _Supported; } internal set { _Supported = value; } }
        private bool _EstimateAvailable = false;
        /// <summary>EstimateAvailable; zero or null means unavailable. Memory values use bytes.</summary>
        public bool EstimateAvailable { get { return _EstimateAvailable; } internal set { _EstimateAvailable = value; } }
        private string _Assumptions = null;
        /// <summary>Assumptions; zero or null means unavailable. Memory values use bytes.</summary>
        public string Assumptions { get { return _Assumptions; } internal set { _Assumptions = value; } }
        private List<string> _Files = new List<string>();
        /// <summary>Files; zero or null means unavailable. Memory values use bytes.</summary>
        public List<string> Files { get { return _Files; } internal set { _Files = value; } }
    }
}
