using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class IncrementalBuildTrackerTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public IncrementalBuildTrackerTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

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
        public void ShouldCallTaskEnvironmentGetAbsolutePath()
        {
            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Build.IncrementalBuildTracker
            {
                BuildEngine = _engine,
                InputFile = "track-test.txt",
                TaskEnvironment = trackingEnv
            };

            task.Execute();

            SharedTestHelpers.AssertGetAbsolutePathCalled(trackingEnv);
        }

        [Fact]
        public void ShouldResolvePathRelativeToProjectDirectory()
        {
            var fileName = "incremental-file.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "data");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.IncrementalBuildTracker
            {
                BuildEngine = _engine,
                InputFile = fileName,
                TaskEnvironment = taskEnv
            };

            task.Execute();

            // Messages should reference the projectDir path
            Assert.Contains(_engine.Messages, m =>
                m.Message!.Contains(_projectDir) && m.Message!.Contains(fileName));
        }

        [Fact]
        public void ShouldNotShareStateBetweenInstances()
        {
            var taskEnv1 = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var taskEnv2 = TaskEnvironmentHelper.CreateForTest(_projectDir);

            var task1 = new SdkTasks.Build.IncrementalBuildTracker
            {
                BuildEngine = _engine,
                InputFile = "instance1.txt",
                TaskEnvironment = taskEnv1
            };

            var task2 = new SdkTasks.Build.IncrementalBuildTracker
            {
                BuildEngine = _engine,
                InputFile = "instance2.txt",
                TaskEnvironment = taskEnv2
            };

            task1.Execute();
            task1.Execute();
            task2.Execute();

            // task2 should not be affected by task1's execution count
            Assert.Equal(1, task2.ExecutionNumber);
        }

        [Fact]
        public void ShouldAutoInitializeProjectDirectoryFromBuildEngine()
        {
            var task = new SdkTasks.Build.IncrementalBuildTracker
            {
                BuildEngine = _engine,
                InputFile = "auto-init.txt",
                TaskEnvironment = new TaskEnvironment()
            };

            bool result = task.Execute();

            Assert.True(result);
        }
    }
}
