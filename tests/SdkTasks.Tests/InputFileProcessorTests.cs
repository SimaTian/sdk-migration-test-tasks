using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class InputFileProcessorTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public InputFileProcessorTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Compilation.InputFileProcessor();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Compilation.InputFileProcessor),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldResolveToProjectDirectory()
        {
            File.WriteAllText(Path.Combine(_projectDir, "test.txt"), "// test");

            var task = new SdkTasks.Compilation.InputFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputFiles = new ITaskItem[] { new TaskItem("test.txt") }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.NotEmpty(task.ProcessedFiles);
            string resolved = task.ProcessedFiles[0].ItemSpec;
            Assert.StartsWith(_projectDir, resolved, StringComparison.OrdinalIgnoreCase);
        }
    }
}
