using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class SourceFileProcessorTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public SourceFileProcessorTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Compilation.SourceFileProcessor();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Compilation.SourceFileProcessor),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldResolveToProjectDirectory()
        {
            File.WriteAllText(Path.Combine(_projectDir, "source.cs"), "// src");

            var task = new SdkTasks.Compilation.SourceFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                Sources = new ITaskItem[] { new TaskItem("source.cs") }
            };

            task.Execute();

            Assert.NotEmpty(task.ResolvedSources);
            string resolved = task.ResolvedSources[0].ItemSpec;
            Assert.StartsWith(_projectDir, resolved, StringComparison.OrdinalIgnoreCase);
        }
    }
}
