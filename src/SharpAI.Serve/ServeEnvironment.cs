namespace SharpAI.Serve
{
    using System;
    using System.Globalization;
    using SharpAI.Classes.Runtime;
    using SharpAI.Operations;

    internal static class ServeEnvironment
    {
        internal static void Apply(ServeOptions options)
        {
            Environment.SetEnvironmentVariable(SharpAIEnvironment.ForceBackend, options.Backend);
            Environment.SetEnvironmentVariable(SharpAIEnvironment.RequireBackend, options.Backend == "auto" ? "false" : "true");
            Environment.SetEnvironmentVariable(SharpAIEnvironment.GpuLayers, options.GpuLayers);
            Set(SharpAIEnvironment.MainGpu, options.MainGpu);
            Set(SharpAIEnvironment.NumThreads, options.Threads);
            Set(SharpAIEnvironment.BatchThreads, options.BatchThreads);
            Set(SharpAIEnvironment.MaxConcurrentGenerations, options.Parallel);
            Set(SharpAIEnvironment.GenerationQueueTimeoutMs, 0);
            Set(SharpAIEnvironment.ContextSize, 0);
            ModelInspection inspection = ModelInspector.Inspect(options.ModelPath);
            int context = options.ContextSize > 0 ? options.ContextSize : inspection.TrainedContext > 0 ? inspection.TrainedContext : 4096;
            Set(SharpAIEnvironment.ContextSize, context);
        }
        private static void Set(string name, int value) { Environment.SetEnvironmentVariable(name, value.ToString(CultureInfo.InvariantCulture)); }
    }
}
