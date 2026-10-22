namespace SharpAI.Server.Classes.Settings
{
    using System;

    /// <summary>
    /// HuggingFace settings.
    /// </summary>
    public class HuggingFaceSettings
    {
        /// <summary>
        /// Optional API key. Null or empty permits public repository access.
        /// </summary>
        public string ApiKey
        {
            get => _ApiKey;
            set => _ApiKey = value;
        }

        private string _ApiKey = Constants.DefaultHuggingFaceApiKey;
        
        /// <summary>
        /// HuggingFace settings.
        /// </summary>
        public HuggingFaceSettings()
        {

        } 
    }
}