namespace SharpAI
{
    internal sealed class ScoredTextChunk
    {
        private readonly int _Score;
        private readonly string _Chunk;

        internal ScoredTextChunk(int score, string chunk)
        {
            _Score = score;
            _Chunk = chunk;
        }

        internal int Score { get { return _Score; } }
        internal string Chunk { get { return _Chunk; } }
    }
}
