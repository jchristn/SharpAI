namespace SharpAI.Database.Interfaces
{
    using System;

    using SharpAI.Models;

    /// <summary>
    /// Data-access contract for Modelfile-equivalent model presets. Implementations use portable, handwritten
    /// SQL over the shared driver so every supported provider honors the same contract.
    /// </summary>
    public interface IModelPresetMethods
    {
        /// <summary>
        /// Enumerate presets using a paginated query.
        /// </summary>
        /// <param name="query">Enumeration query. When null, defaults are used.</param>
        /// <returns>Enumeration result.</returns>
        EnumerationResult<ModelPreset> Enumerate(EnumerationQuery query);

        /// <summary>
        /// Get a preset by GUID.
        /// </summary>
        /// <param name="guid">GUID.</param>
        /// <returns>Instance, or null when not found.</returns>
        ModelPreset GetByGuid(Guid guid);

        /// <summary>
        /// Get a preset by name.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <returns>Instance, or null when not found.</returns>
        ModelPreset GetByName(string name);

        /// <summary>
        /// Create a preset. Returns the existing preset when one with the same name already exists.
        /// </summary>
        /// <param name="preset">Preset to create.</param>
        /// <returns>The created (or existing) instance.</returns>
        ModelPreset Add(ModelPreset preset);

        /// <summary>
        /// Update an existing preset.
        /// </summary>
        /// <param name="preset">Preset to update.</param>
        /// <returns>The updated instance.</returns>
        ModelPreset Update(ModelPreset preset);

        /// <summary>
        /// Delete a preset by GUID.
        /// </summary>
        /// <param name="guid">GUID.</param>
        void Delete(Guid guid);
    }
}
