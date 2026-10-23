namespace SharpAI.Server.API.REST.Routes
{
    using System;
    using System.Threading.Tasks;
    using WatsonWebserver.Core;

    internal static class InferenceErrors
    {
        internal static Task<object> RunInference(ApiRequest request, Func<Task<object>> handler)
        { return SharpAI.Http.InferenceErrors.RunInference(request, handler); }
    }
}
