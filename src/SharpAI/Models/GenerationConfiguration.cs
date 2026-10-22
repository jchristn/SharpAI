namespace SharpAI.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>Reusable generation parameters. Null numeric values use engine defaults; excludes the system prompt and model.</summary>
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public class GenerationConfiguration
    {
        /// <summary>Temperature. Optional; allowed range 0 through 2.</summary>
        public double? Temperature
        {
            get { return _Temperature; }
            set
            {
                if (value.HasValue && (!Double.IsFinite(value.Value) || value.Value < 0 || value.Value > 2))
                    throw new ArgumentOutOfRangeException(nameof(Temperature), "Temperature must be between 0 and 2.");
                _Temperature = value;
            }
        }
        private double? _Temperature;

        /// <summary>MaxTokens. Optional; allowed range 1 through 32768.</summary>
        public int? MaxTokens
        {
            get { return _MaxTokens; }
            set
            {
                if (value.HasValue && (value.Value < 1 || value.Value > 32768))
                    throw new ArgumentOutOfRangeException(nameof(MaxTokens), "MaxTokens must be between 1 and 32768.");
                _MaxTokens = value;
            }
        }
        private int? _MaxTokens;

        /// <summary>TopP. Optional; allowed range 0 through 1.</summary>
        public double? TopP
        {
            get { return _TopP; }
            set
            {
                if (value.HasValue && (!Double.IsFinite(value.Value) || value.Value < 0 || value.Value > 1))
                    throw new ArgumentOutOfRangeException(nameof(TopP), "TopP must be between 0 and 1.");
                _TopP = value;
            }
        }
        private double? _TopP;

        /// <summary>TopK. Optional; allowed range 0 through 100.</summary>
        public int? TopK
        {
            get { return _TopK; }
            set
            {
                if (value.HasValue && (value.Value < 0 || value.Value > 100))
                    throw new ArgumentOutOfRangeException(nameof(TopK), "TopK must be between 0 and 100.");
                _TopK = value;
            }
        }
        private int? _TopK;

        /// <summary>MinP. Optional; allowed range 0 through 1.</summary>
        public double? MinP
        {
            get { return _MinP; }
            set
            {
                if (value.HasValue && (!Double.IsFinite(value.Value) || value.Value < 0 || value.Value > 1))
                    throw new ArgumentOutOfRangeException(nameof(MinP), "MinP must be between 0 and 1.");
                _MinP = value;
            }
        }
        private double? _MinP;

        /// <summary>RepeatPenalty. Optional; allowed range 0 through 2.</summary>
        public double? RepeatPenalty
        {
            get { return _RepeatPenalty; }
            set
            {
                if (value.HasValue && (!Double.IsFinite(value.Value) || value.Value < 0 || value.Value > 2))
                    throw new ArgumentOutOfRangeException(nameof(RepeatPenalty), "RepeatPenalty must be between 0 and 2.");
                _RepeatPenalty = value;
            }
        }
        private double? _RepeatPenalty;

        /// <summary>RepeatLastN. Optional; allowed range 0 through 32768.</summary>
        public int? RepeatLastN
        {
            get { return _RepeatLastN; }
            set
            {
                if (value.HasValue && (value.Value < 0 || value.Value > 32768))
                    throw new ArgumentOutOfRangeException(nameof(RepeatLastN), "RepeatLastN must be between 0 and 32768.");
                _RepeatLastN = value;
            }
        }
        private int? _RepeatLastN;

        /// <summary>PresencePenalty. Optional; allowed range -2 through 2.</summary>
        public double? PresencePenalty
        {
            get { return _PresencePenalty; }
            set
            {
                if (value.HasValue && (!Double.IsFinite(value.Value) || value.Value < -2 || value.Value > 2))
                    throw new ArgumentOutOfRangeException(nameof(PresencePenalty), "PresencePenalty must be between -2 and 2.");
                _PresencePenalty = value;
            }
        }
        private double? _PresencePenalty;

        /// <summary>FrequencyPenalty. Optional; allowed range -2 through 2.</summary>
        public double? FrequencyPenalty
        {
            get { return _FrequencyPenalty; }
            set
            {
                if (value.HasValue && (!Double.IsFinite(value.Value) || value.Value < -2 || value.Value > 2))
                    throw new ArgumentOutOfRangeException(nameof(FrequencyPenalty), "FrequencyPenalty must be between -2 and 2.");
                _FrequencyPenalty = value;
            }
        }
        private double? _FrequencyPenalty;

        /// <summary>Seed. Optional; allowed range 0 through 2147483647.</summary>
        public int? Seed
        {
            get { return _Seed; }
            set
            {
                if (value.HasValue && (value.Value < 0 || value.Value > 2147483647))
                    throw new ArgumentOutOfRangeException(nameof(Seed), "Seed must be between 0 and 2147483647.");
                _Seed = value;
            }
        }
        private int? _Seed;

        /// <summary>Literal stop sequences. Defaults to empty.</summary>
        public List<string> Stop { get; set; } = new List<string>();
        /// <summary>Whether replies stream incrementally. Default true.</summary>
        public bool StreamResponse { get; set; } = true;
        /// <summary>Request a JSON response. Default false.</summary>
        public bool JsonOutput { get; set; } = false;
        /// <summary>Show model thinking when supported. Default false.</summary>
        public bool DisplayThinking { get; set; } = false;
    }
}
