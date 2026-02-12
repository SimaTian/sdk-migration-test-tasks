using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class BatchItemProcessorTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public BatchItemProcessorTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Compilation.BatchItemProcessor();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Compilation.BatchItemProcessor),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldResolveRelativeToProjectDirectory()
        {
            var relativePaths = new[] { "file1.txt", "subdir\\file2.txt" };

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Compilation.BatchItemProcessor
            {
                BuildEngine = _engine,
                RelativePaths = relativePaths,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(Path.Combine(_projectDir, "file1.txt"), task.AbsolutePaths[0]);
            Assert.Equal(Path.Combine(_projectDir, "subdir\\file2.txt"), task.AbsolutePaths[1]);
        }
    }
}
