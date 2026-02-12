using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class DirectoryContextSwitcherTests : IDisposable
    {
        private readonly string _projectDir;

        public DirectoryContextSwitcherTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldNotModifyGlobalCwd()
        {
            var originalCwd = Environment.CurrentDirectory;

            var task = new SdkTasks.Build.DirectoryContextSwitcher
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                NewDirectory = _projectDir
            };

            task.Execute();

            // Assert CORRECT behavior: Environment.CurrentDirectory should be unchanged
            Assert.Equal(originalCwd, Environment.CurrentDirectory);

            // Restore CWD in case task changed it
            Environment.CurrentDirectory = originalCwd;
        }

        [Fact]
        public void ShouldUseTaskEnvironmentGetAbsolutePath()
        {
            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var relativePath = "subdir";
            Directory.CreateDirectory(Path.Combine(_projectDir, relativePath));

            var task = new SdkTasks.Build.DirectoryContextSwitcher
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = trackingEnv,
                NewDirectory = relativePath
            };

            task.Execute();

            // Assert CORRECT behavior: task should call TaskEnvironment.GetAbsolutePath
            SharedTestHelpers.AssertGetAbsolutePathCalled(trackingEnv);
            Assert.Contains(relativePath, trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void ShouldResolveRelativePathToProjectDirectory()
        {
            var engine = new MockBuildEngine();
            var relativePath = "build-context";
            Directory.CreateDirectory(Path.Combine(_projectDir, relativePath));

            var task = new SdkTasks.Build.DirectoryContextSwitcher
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                NewDirectory = relativePath
            };

            bool result = task.Execute();

            // Assert CORRECT behavior: resolved path should contain projectDir, not CWD
            Assert.True(result);
            var expectedResolved = Path.Combine(_projectDir, relativePath);
            Assert.Contains(engine.Messages, m => m.Message!.Contains(expectedResolved));
        }

        [Fact]
        public void AutoInitializesProjectDirectory_FromBuildEngine()
        {
            var engine = new MockBuildEngine();
            var taskEnv = new TaskEnvironment { ProjectDirectory = string.Empty };

            var task = new SdkTasks.Build.DirectoryContextSwitcher
            {
                BuildEngine = engine,
                TaskEnvironment = taskEnv,
                NewDirectory = "somedir"
            };

            task.Execute();

            // ProjectDirectory should be auto-initialized from BuildEngine.ProjectFileOfTaskNode
            Assert.False(string.IsNullOrEmpty(task.TaskEnvironment.ProjectDirectory));
        }

        [Fact]
        public void ShouldResolveAbsolutePathUnchanged()
        {
            var engine = new MockBuildEngine();
            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };

            var task = new SdkTasks.Build.DirectoryContextSwitcher
            {
                BuildEngine = engine,
                TaskEnvironment = trackingEnv,
                NewDirectory = _projectDir
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.True(trackingEnv.GetAbsolutePathCallCount > 0);
            Assert.Contains(engine.Messages, m => m.Message!.Contains(_projectDir));
        }
    }
}
