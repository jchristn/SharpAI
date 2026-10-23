namespace SharpAI.Http
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Engines;
    using SharpAI.Models;
    using SharpAI.Prompts;

    /// <summary>Host-owned model lookup and loading policy. Implementations must support concurrent requests.</summary>
    public interface IInferenceModels
    {
        /// <summary>Find a visible model by its API name; null means unavailable.</summary>
        ModelFile? Find(string name);
        /// <summary>Find an optional named preset; null means no preset.</summary>
        ModelPreset? FindPreset(string name);
        /// <summary>Resolve the host-owned model to its entry GGUF path.</summary>
        string GetPath(ModelFile model);
        /// <summary>Whether the entry path already has loaded weights.</summary>
        bool IsLoaded(string path);
        /// <summary>Load or obtain the entry engine; cancellation belongs to the caller.</summary>
        Task<LlamaSharpEngine> LoadAsync(string path, CancellationToken cancellationToken);
        /// <summary>Render chat using the host's explicit template or the model/family template.</summary>
        ChatTemplateResult FormatChat(LlamaSharpEngine engine, ModelFile model, List<ChatMessage> messages);
    }
}
