namespace SharpAI.Server.Classes.Runtime
{
    using System;
    using System.Net;

    /// <summary>Identifies library payloads that must not bypass item ownership through shared request history or debug body logs.</summary>
    public static class LibraryPrivacy
    {
        /// <summary>Whether a request targets a managed library collection or item.</summary>
        public static bool IsLibraryPath(string path)
        {
            string normalized=WebUtility.UrlDecode((path ?? String.Empty).Split('?')[0]);
            foreach(string root in new[] {"/v1.0/system-prompts","/v1.0/model-configurations"})
                if(normalized.Equals(root,StringComparison.OrdinalIgnoreCase)||normalized.StartsWith(root+"/",StringComparison.OrdinalIgnoreCase))return true;
            return false;
        }
    }
}
