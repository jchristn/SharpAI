namespace SharpAI.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A Modelfile-equivalent preset: a named configuration layered over a base model that supplies a
    /// default system prompt, sampling parameters, stop sequences, and an optional template override. A
    /// request that names the preset (as its <c>model</c>) runs against the preset's base model with these
    /// defaults applied, unless the request overrides them explicitly.
    /// </summary>
    public class ModelPreset
    {
        #region Public-Members

        /// <summary>
        /// GUID (primary identifier).
        /// </summary>
        public Guid GUID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Preset name. Unique; used as the <c>model</c> value in inference requests to select this preset.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Name of the base model (in the registry) that this preset runs against. Required.
        /// </summary>
        public string ModelName { get; set; } = null;

        /// <summary>
        /// Default system prompt injected ahead of the conversation, or null for none.
        /// </summary>
        public string SystemPrompt { get; set; } = null;

        /// <summary>
        /// Default sampling temperature applied when the request does not specify one, or null.
        /// </summary>
        public float? Temperature { get; set; } = null;

        /// <summary>
        /// Default maximum tokens to generate when the request does not specify one, or null.
        /// </summary>
        public int? MaxTokens { get; set; } = null;

        /// <summary>
        /// Default top-p (nucleus) sampling value applied when the request does not specify one, or null.
        /// </summary>
        public float? TopP { get; set; } = null;

        /// <summary>
        /// Optional raw chat-template override (GGUF/Jinja-style) stored with the preset, or null to use the
        /// model's own template.
        /// </summary>
        public string TemplateOverride { get; set; } = null;

        /// <summary>
        /// Default stop sequences merged with any supplied by the request. Never null.
        /// </summary>
        public List<string> Stop { get; set; } = new List<string>();

        /// <summary>
        /// Timestamp from creation, in UTC time.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp from last update, in UTC time.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// A Modelfile-equivalent preset.
        /// </summary>
        public ModelPreset()
        {
        }

        #endregion
    }
}
