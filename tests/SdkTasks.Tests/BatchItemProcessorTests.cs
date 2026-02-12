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

        [Fact]
        public void ShouldCallGetAbsolutePathOnTaskEnvironment()
        {
            var relativePaths = new[] { "tracked1.txt", "tracked2.txt" };

            var tracking = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new SdkTasks.Compilation.BatchItemProcessor
            {
                BuildEngine = _engine,
                RelativePaths = relativePaths,
                TaskEnvironment = tracking
            };

            task.Execute();

            Assert.Equal(2, tracking.GetAbsolutePathCallCount);
            Assert.Contains("tracked1.txt", tracking.GetAbsolutePathArgs);
            Assert.Contains("tracked2.txt", tracking.GetAbsolutePathArgs);
        }

        [Fact]
        public void ShouldNotResolveRelativeToCwd()
        {
            var relativePaths = new[] { "cwd-check.txt" };

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Compilation.BatchItemProcessor
            {
                BuildEngine = _engine,
                RelativePaths = relativePaths,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            // Resolved path must point to projectDir, not CWD
            Assert.StartsWith(_projectDir, task.AbsolutePaths[0]);
        }

        [Fact]
        public void ShouldResolveAllPathsInBatch()
        {
            var relativePaths = new[] { "a.txt", "b.txt", "c.txt" };

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Compilation.BatchItemProcessor
            {
                BuildEngine = _engine,
                RelativePaths = relativePaths,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(3, task.AbsolutePaths.Length);
            foreach (var absPath in task.AbsolutePaths)
            {
                Assert.StartsWith(_projectDir, absPath);
            }
        }
    }
}
