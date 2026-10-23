namespace SharpAI.Server.Classes.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using SharpAI.Database.Interfaces;
    using SharpAI.Engines;
    using SharpAI.Http;
    using SharpAI.Models;
    using SharpAI.Prompts;
    using SharpAI.Server.Classes.Settings;
    using SharpAI.Services;

    internal sealed class ManagedInferenceModels : IInferenceModels
    {
        private readonly Settings _Settings;
        private readonly ModelFileService _Files;
        private readonly ModelEngineService _Engines;
        private readonly IModelPresetMethods _Presets;
        internal ManagedInferenceModels(Settings settings, ModelFileService files, ModelEngineService engines, IModelPresetMethods presets)
        { _Settings = settings; _Files = files; _Engines = engines; _Presets = presets; }
        public ModelFile Find(string name) { return _Files.GetByName(name); }
        public ModelPreset FindPreset(string name) { return _Presets?.GetByName(name); }
        public string GetPath(ModelFile model) { return SharpAI.Operations.GgufShards.Resolve(_Settings.Storage.ModelsDirectory, model.GUID); }
        public bool IsLoaded(string path) { return _Engines.GetLoadedModelPaths().Contains(System.IO.Path.GetFullPath(path)); }
        public Task<LlamaSharpEngine> LoadAsync(string path, CancellationToken cancellationToken) { return _Engines.GetByModelFileAsync(path, cancellationToken); }
        public ChatTemplateResult FormatChat(LlamaSharpEngine engine, ModelFile model, List<ChatMessage> messages)
        { return ChatTemplateResolver.Resolve(engine, model.Family, messages); }
    }
}
