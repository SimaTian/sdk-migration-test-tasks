using System;
using System.IO;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;
using Xunit;

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
        public void ShouldCanonicalizeResolvedPaths()
        {
            var relativePath = Path.Combine("data", "..", "data", "input.txt");
            var expectedPath = Path.GetFullPath(Path.Combine(_projectDir, relativePath));
            Directory.CreateDirectory(Path.GetDirectoryName(expectedPath)!);
            File.WriteAllText(expectedPath, "payload");

            var taskEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Build.IncrementalBuildTracker
            {
                BuildEngine = _engine,
                InputFile = relativePath,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains(expectedPath));
            Assert.True(taskEnv.GetCanonicalFormCallCount > 0);
        }

        [Fact]
        public void ShouldAutoInitializeProjectDirectory()
        {
            var fileName = "auto-init.txt";
            var absolutePath = Path.Combine(_projectDir, fileName);
            File.WriteAllText(absolutePath, "payload");

            _engine.ProjectFileOfTaskNode = Path.Combine(_projectDir, "test.csproj");
            var taskEnv = new TaskEnvironment();

            var task = new SdkTasks.Build.IncrementalBuildTracker
            {
                BuildEngine = _engine,
                InputFile = fileName,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains(absolutePath));
        }

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
    }
}
