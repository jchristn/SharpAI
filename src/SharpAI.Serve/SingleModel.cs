namespace SharpAI.Serve
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LLama;
    using SharpAI.Engines;
    using SharpAI.Http;
    using SharpAI.Models;
    using SharpAI.Operations;
    using SharpAI.Prompts;
    using SyslogLogging;

    internal sealed class SingleModel : IInferenceModels, IDisposable
    {
        private readonly ServeOptions _Options;
        private readonly LlamaSharpEngine _Engine;
        private readonly ModelFile _Model;
        private readonly string? _Template;
        internal SingleModel(ServeOptions options, LoggingModule logging)
        {
            _Options = options;
            ModelInspection inspection = ModelInspector.Inspect(options.ModelPath);
            if (!inspection.Supported) throw new InvalidDataException("The GGUF contains unsupported model components.");
            _Model = new ModelFile
            {
                Name = options.ModelName, Family = inspection.Architecture ?? "unknown", Format = "gguf",
                ContentLength = inspection.Files.Sum(path => new FileInfo(path).Length), ParameterCount = inspection.ParameterCount,
                ModelCreationUtc = File.GetLastWriteTimeUtc(options.ModelPath)
            };
            if (options.ChatTemplatePath != null)
            {
                if (new FileInfo(options.ChatTemplatePath).Length > 1048576) throw new InvalidDataException("Chat template must not exceed 1 MiB.");
                _Template = File.ReadAllText(options.ChatTemplatePath);
                if (String.IsNullOrWhiteSpace(_Template)) throw new InvalidDataException("Chat template must not be empty.");
            }
            _Engine = new LlamaSharpEngine(logging);
        }
        internal async Task InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _Engine.InitializeAsync(_Options.ModelPath).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            _Model.Embeddings = _Engine.SupportsEmbeddings;
            _Model.Completions = _Engine.SupportsGeneration;
            if (!_Model.Embeddings && !_Model.Completions) throw new InvalidDataException("The model has no supported inference capability.");
            if (_Model.Embeddings && !_Model.Completions) await _Engine.GenerateEmbeddingsAsync("ready", cancellationToken).ConfigureAwait(false);
            if (_Template != null)
            {
                if (!_Model.Completions) throw new InvalidDataException("A chat template requires a completion-capable model.");
                FormatChat(_Engine, _Model, new List<ChatMessage> { new ChatMessage { Role = "user", Content = "ready" } });
            }
        }
        internal ModelFile Metadata { get { return _Model; } }
        internal int ContextSize { get { return _Engine.GetContextSize(); } }
        public ModelFile? Find(string name) { return String.Equals(name, _Model.Name, StringComparison.Ordinal) ? _Model : null; }
        public ModelPreset? FindPreset(string name) { return null; }
        public string GetPath(ModelFile model)
        {
            if (!Object.ReferenceEquals(model, _Model)) throw new InvalidOperationException("Only the configured model can be served.");
            return _Options.ModelPath;
        }
        public bool IsLoaded(string path) { return path == _Options.ModelPath && _Engine.IsInitialized; }
        public Task<LlamaSharpEngine> LoadAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsLoaded(path)) throw new InvalidOperationException("Only the configured, loaded model can be served.");
            return Task.FromResult(_Engine);
        }
        public ChatTemplateResult FormatChat(LlamaSharpEngine engine, ModelFile model, List<ChatMessage> messages)
        {
            if (_Template == null) return ChatTemplateResolver.Resolve(engine, model.Family, messages);
            LLamaTemplate template = new LLamaTemplate(_Template) { AddAssistant = true };
            foreach (ChatMessage message in messages) template.Add(message.Role ?? "user", message.Content ?? String.Empty);
            string rendered = Encoding.UTF8.GetString(template.Apply());
            if (String.IsNullOrWhiteSpace(rendered)) throw new InvalidDataException("The explicit chat template rendered an empty prompt.");
            return new ChatTemplateResult { Prompt = rendered, StopSequences = Array.Empty<string>(), UsedEmbeddedTemplate = true };
        }
        public void Dispose() { _Engine.Dispose(); }
    }
}
