namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using SharpAI.Operations;
    using Touchstone.Core;

    /// <summary>Pure source-contract cases shared by every backend runner. Does not make network requests.</summary>
    public static class ModelPullSourceSuite
    {
        /// <summary>Build repository URL and exact revision/file parsing contracts.</summary>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            foreach (string input in new[] { "org/model", "https://huggingface.co/org/model", "https://hf.co/org/model/", "https://www.huggingface.co/org/model/tree/main" })
                cases.Add(new TestCaseDescriptor("PullSource", "Repository" + cases.Count, "Repository source " + input, ct => { TestAssert.Equal("org/model", ModelPullSource.Parse(input).Repository); return Task.CompletedTask; }));
            foreach (string kind in new[] { "resolve", "blob" })
                cases.Add(new TestCaseDescriptor("PullSource", kind, "Preserve requested revision and nested GGUF path", ct => { ModelPullSource source = ModelPullSource.Parse("https://huggingface.co/org/model/" + kind + "/commit123/folder/model.gguf"); TestAssert.Equal("commit123", source.Revision); TestAssert.Equal("folder/model.gguf", source.FilePath); return Task.CompletedTask; }));
            foreach (string input in new[] { "model", "../model", "https://evil.example/org/model", "http://huggingface.co/org/model", "https://user:pass@huggingface.co/org/model", "https://huggingface.co/org/model?token=secret", "https://huggingface.co/org/model#fragment", "https://huggingface.co/org/model/tree/main/subdir", "https://huggingface.co/org/model/resolve/main/model.bin", "https://huggingface.co/org/../model", "https://huggingface.co/org/model/resolve/main/a%2fb.gguf" })
                cases.Add(new TestCaseDescriptor("PullSource", "Reject" + cases.Count, "Reject unsafe or ambiguous model source", ct =>
                {
                    bool rejected = false; try { ModelPullSource.Parse(input); } catch (ArgumentException) { rejected = true; }
                    TestAssert.True(rejected, "unsupported source must be rejected before network access"); return Task.CompletedTask;
                }));
            return new TestSuiteDescriptor("PullSource", "Shared model pull source contract", cases);
        }
    }
}
