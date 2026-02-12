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
        public void ShouldNotResolveRelativeToCwd()
        {
            var relativePaths = new[] { "batch-item.txt" };

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Compilation.BatchItemProcessor
            {
                BuildEngine = _engine,
                RelativePaths = relativePaths,
                TaskEnvironment = taskEnv
            };

            task.Execute();

            // Resolved paths should contain projectDir, not CWD
            Assert.All(task.AbsolutePaths, p =>
                Assert.StartsWith(_projectDir, p, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ShouldResolveMultiplePathsToProjectDirectory()
        {
            var relativePaths = new[] { "a.txt", "b.txt", "sub\\c.txt" };

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Compilation.BatchItemProcessor
            {
                BuildEngine = _engine,
                RelativePaths = relativePaths,
                TaskEnvironment = taskEnv
            };

            task.Execute();

            Assert.Equal(3, task.AbsolutePaths.Length);
            foreach (var path in task.AbsolutePaths)
            {
                Assert.StartsWith(_projectDir, path, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void ShouldUseProjectDirectoryNotEnvironmentCurrentDirectory()
        {
            var cwd = Environment.CurrentDirectory;
            var relativePaths = new[] { "lambda-test.txt" };

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Compilation.BatchItemProcessor
            {
                BuildEngine = _engine,
                RelativePaths = relativePaths,
                TaskEnvironment = taskEnv
            };

            task.Execute();

            // Verify that the resolved path starts with projectDir, not CWD
            Assert.NotEqual(cwd, _projectDir);
            Assert.StartsWith(_projectDir, task.AbsolutePaths[0], StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(cwd, task.AbsolutePaths[0]);
        }

        [Fact]
        public void ShouldHandleEmptyPaths()
        {
            var relativePaths = Array.Empty<string>();

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Compilation.BatchItemProcessor
            {
                BuildEngine = _engine,
                RelativePaths = relativePaths,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Empty(task.AbsolutePaths);
        }

        [Fact]
        public void ShouldCallTaskEnvironmentGetAbsolutePath()
        {
            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Compilation.BatchItemProcessor
            {
                BuildEngine = _engine,
                RelativePaths = new[] { "tracked.txt", "sub\\tracked2.txt" },
                TaskEnvironment = trackingEnv
            };

            task.Execute();

            Assert.Equal(2, trackingEnv.GetAbsolutePathCallCount);
            Assert.Contains("tracked.txt", trackingEnv.GetAbsolutePathArgs);
            Assert.Contains("sub\\tracked2.txt", trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void ShouldAutoInitializeProjectDirectoryFromBuildEngine()
        {
            var task = new SdkTasks.Compilation.BatchItemProcessor
            {
                BuildEngine = _engine,
                RelativePaths = new[] { "auto-init.txt" },
                TaskEnvironment = new TaskEnvironment()
            };

            bool result = task.Execute();

            Assert.True(result);
        }
    }
}
