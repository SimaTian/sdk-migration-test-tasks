using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class IncrementalBuildTrackerTests : IDisposable
    {
        private readonly TaskTestContext _ctx;
        private string _projectDir => _ctx.ProjectDir;
        private MockBuildEngine _engine => _ctx.Engine;

        public IncrementalBuildTrackerTests() => _ctx = new TaskTestContext();
        public void Dispose() => _ctx.Dispose();

        [Fact]
        public void ShouldHaveInstanceIsolation()
        {
            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);

            var task1 = new SdkTasks.Build.IncrementalBuildTracker
            {
                BuildEngine = _engine,
                InputFile = "fileA.txt",
                TaskEnvironment = taskEnv
            };

            var task2 = new SdkTasks.Build.IncrementalBuildTracker
            {
                BuildEngine = _engine,
                InputFile = "fileB.txt",
                TaskEnvironment = taskEnv
            };

            task1.Execute();
            task2.Execute();

            Assert.Equal(1, task1.ExecutionNumber);
            Assert.Equal(1, task2.ExecutionNumber);
        }

        [Fact]
        public void Execute_ShouldCallGetAbsolutePath()
        {
            var trackingEnv = new TrackingTaskEnvironment
            {
                ProjectDirectory = _projectDir
            };

            var task = new SdkTasks.Build.IncrementalBuildTracker
            {
                BuildEngine = _engine,
                InputFile = "tracked.txt",
                TaskEnvironment = trackingEnv
            };

            task.Execute();

            // Verify TaskEnvironment.GetAbsolutePath was called
            SharedTestHelpers.AssertUsesGetAbsolutePath(trackingEnv);
            Assert.Contains("tracked.txt", trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void Execute_ShouldResolveRelativePathToProjectDirectory()
        {
            var relativePath = "resolve-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, relativePath), "content");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.IncrementalBuildTracker
            {
                BuildEngine = _engine,
                InputFile = relativePath,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            // Assert CORRECT behavior: path resolves relative to ProjectDirectory, not CWD
            Assert.True(result);
            var expectedPath = Path.Combine(_projectDir, relativePath);
            Assert.Contains(_engine.Messages,
                m => m.Message!.Contains(expectedPath));
        }

        [Fact]
        public void Execute_WithExistingFile_ShouldLogFileSize()
        {
            var relativePath = "sizefile.txt";
            var fileContent = "hello world";
            File.WriteAllText(Path.Combine(_projectDir, relativePath), fileContent);

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.IncrementalBuildTracker
            {
                BuildEngine = _engine,
                InputFile = relativePath,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            // Assert CORRECT behavior: task finds file in projectDir and logs size
            Assert.True(result);
            Assert.Contains(_engine.Messages,
                m => m.Message!.Contains("File size:") && m.Message!.Contains("bytes"));
        }

        [Fact]
        public void Execute_WithNonExistentFile_ShouldStillSucceed()
        {
            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.IncrementalBuildTracker
            {
                BuildEngine = _engine,
                InputFile = "nonexistent.txt",
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(1, task.ExecutionNumber);
        }
    }
}
