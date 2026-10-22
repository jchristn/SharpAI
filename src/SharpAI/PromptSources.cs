namespace SharpAI
{
    internal sealed class PromptSources
    {
        private readonly string _DocumentExtract;
        private readonly string _VectorHits;

        internal PromptSources(string documentExtract, string vectorHits)
        {
            _DocumentExtract = documentExtract;
            _VectorHits = vectorHits;
        }

        internal string DocumentExtract { get { return _DocumentExtract; } }
        internal string VectorHits { get { return _VectorHits; } }
    }
}
