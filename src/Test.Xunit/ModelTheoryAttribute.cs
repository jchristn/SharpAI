namespace Test.Xunit
{
    using Test.Shared;
    using Xunit;

    /// <summary>Requires real GGUF inference weights.</summary>
    public sealed class ModelTheoryAttribute : TheoryAttribute
    {
        public ModelTheoryAttribute() { if (!ModelFixture.IsAvailable) Skip = ModelFixture.SkipReason; }
    }
}
