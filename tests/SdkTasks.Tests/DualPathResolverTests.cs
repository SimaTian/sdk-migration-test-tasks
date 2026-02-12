using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class DualPathResolverTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public DualPathResolverTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldResolveBothPathsToProjectDirectory()
        {
            var primaryFile = "primary.txt";
            var secondaryFile = "secondary.txt";
            File.WriteAllText(Path.Combine(_projectDir, primaryFile), "same");
            File.WriteAllText(Path.Combine(_projectDir, secondaryFile), "same");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.DualPathResolver
            {
                BuildEngine = _engine,
                PrimaryPath = primaryFile,
                SecondaryPath = secondaryFile,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            // Assert CORRECT behavior: both paths resolve to ProjectDir, files found and match
            Assert.True(result);
            Assert.True(task.FilesMatch);
        }

        [Fact]
        public void ShouldCallGetAbsolutePathForBothPaths()
        {
            var primaryFile = "primary.txt";
            var secondaryFile = "secondary.txt";
            File.WriteAllText(Path.Combine(_projectDir, primaryFile), "content");
            File.WriteAllText(Path.Combine(_projectDir, secondaryFile), "content");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new SdkTasks.Build.DualPathResolver
            {
                BuildEngine = _engine,
                PrimaryPath = primaryFile,
                SecondaryPath = secondaryFile,
                TaskEnvironment = trackingEnv
            };

            task.Execute();

            // Verify TaskEnvironment.GetAbsolutePath was called for both paths
            Assert.Equal(2, trackingEnv.GetAbsolutePathCallCount);
            Assert.Contains(primaryFile, trackingEnv.GetAbsolutePathArgs);
            Assert.Contains(secondaryFile, trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void ShouldNotResolveRelativeToProcessCwd()
        {
            // Files exist ONLY in _projectDir (not in process CWD)
            var primaryFile = "cwd-test-primary.txt";
            var secondaryFile = "cwd-test-secondary.txt";
            File.WriteAllText(Path.Combine(_projectDir, primaryFile), "data");
            File.WriteAllText(Path.Combine(_projectDir, secondaryFile), "data");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new SdkTasks.Build.DualPathResolver
            {
                BuildEngine = _engine,
                PrimaryPath = primaryFile,
                SecondaryPath = secondaryFile,
                TaskEnvironment = trackingEnv
            };

            bool result = task.Execute();

            // Paths should resolve to ProjectDirectory, finding the files there
            Assert.True(result);
            Assert.True(task.FilesMatch);
            // Confirm TaskEnvironment was used (not direct Path.GetFullPath)
            Assert.True(trackingEnv.GetAbsolutePathCallCount >= 2);
        }

        [Fact]
        public void ShouldReturnFalseFilesMatchWhenContentDiffers()
        {
            var primaryFile = "diff-primary.txt";
            var secondaryFile = "diff-secondary.txt";
            File.WriteAllText(Path.Combine(_projectDir, primaryFile), "content-a");
            File.WriteAllText(Path.Combine(_projectDir, secondaryFile), "content-b");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.DualPathResolver
            {
                BuildEngine = _engine,
                PrimaryPath = primaryFile,
                SecondaryPath = secondaryFile,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.False(task.FilesMatch);
        }

        [Fact]
        public void ShouldReturnFalseFilesMatchWhenFileDoesNotExist()
        {
            var primaryFile = "nonexistent-primary.txt";
            var secondaryFile = "nonexistent-secondary.txt";

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.DualPathResolver
            {
                BuildEngine = _engine,
                PrimaryPath = primaryFile,
                SecondaryPath = secondaryFile,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            // Task succeeds but files don't match since they don't exist
            Assert.True(result);
            Assert.False(task.FilesMatch);
        }
    }
}
