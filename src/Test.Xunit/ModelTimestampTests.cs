namespace Test.Xunit
{
    using System;
    using System.Text.Json;
    using SharpAI.Models;
    using Xunit;

    /// <summary>Inventory timestamps describe model metadata rather than the current refresh time.</summary>
    public sealed class ModelTimestampTests
    {
        /// <summary>A missing upstream timestamp falls back to the persisted registration time.</summary>
        [Fact]
        public void InventoryUsesPersistedCreationTimeWhenUpstreamTimestampIsMissing()
        {
            ModelFile model = new ModelFile { CreatedUtc = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc) };
            JsonElement details = JsonSerializer.SerializeToElement(model.ToOllamaModelDetails());
            Assert.Equal(model.CreatedUtc, details.GetProperty("modified_at").GetDateTime().ToUniversalTime());
            model.ModelCreationUtc = new DateTime(2024, 6, 7, 8, 9, 10, DateTimeKind.Utc);
            details = JsonSerializer.SerializeToElement(model.ToOllamaModelDetails());
            Assert.Equal(model.ModelCreationUtc.Value, details.GetProperty("modified_at").GetDateTime().ToUniversalTime());
        }
    }
}
