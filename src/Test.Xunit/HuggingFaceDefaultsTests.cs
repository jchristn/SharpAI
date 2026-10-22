namespace Test.Xunit
{
    using System.Net.Http;
    using SharpAI;
    using SharpAI.Hosting;
    using Xunit;

    /// <summary>Fresh configurations must permit anonymous access to public repositories.</summary>
    public sealed class HuggingFaceDefaultsTests
    {
        /// <summary>A placeholder credential must never be sent by a default client.</summary>
        [Fact]
        public void PublicRepositoryDefaultsDoNotSendAnInvalidToken()
        {
            HuggingFaceClient repository = new HuggingFaceClient(null, Constants.DefaultHuggingFaceApiKey);
            using HttpClient http = repository.CreateDownloadClient();
            Assert.Null(http.DefaultRequestHeaders.Authorization);
        }
    }
}
