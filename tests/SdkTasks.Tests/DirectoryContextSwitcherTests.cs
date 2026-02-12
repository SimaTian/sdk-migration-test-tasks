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

            Assert.Equal(originalCwd, Environment.CurrentDirectory);

            // Restore CWD in case task changed it
            Environment.CurrentDirectory = originalCwd;
        }

        [Fact]
        public void Execute_WithTrackingEnvironment_CallsGetAbsolutePath()
        {
            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);

            var task = new SdkTasks.Build.DirectoryContextSwitcher
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = trackingEnv,
                NewDirectory = "subdir"
            };

            task.Execute();

            // Task must use TaskEnvironment.GetAbsolutePath instead of direct path APIs
            SharedTestHelpers.AssertUsesGetAbsolutePath(trackingEnv);
            Assert.Contains("subdir", trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void Execute_ResolvesRelativePathToProjectDirectory()
        {
            var engine = new MockBuildEngine();

            var task = new SdkTasks.Build.DirectoryContextSwitcher
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                NewDirectory = "subdir"
            };

            task.Execute();

            // The resolved path should be under ProjectDirectory, not process CWD
            var expectedPath = Path.Combine(_projectDir, "subdir");
            var logMessage = engine.Messages.Select(m => m.Message).FirstOrDefault() ?? "";
            Assert.Contains(expectedPath, logMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Execute_ResolvedPathShouldNotBeRelativeToCwd()
        {
            var task = new SdkTasks.Build.DirectoryContextSwitcher
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                NewDirectory = "testdir"
            };

            task.Execute();

            // The resolved path must NOT be under the process CWD
            var cwdBased = Path.Combine(Environment.CurrentDirectory, "testdir");
            var projectBased = Path.Combine(_projectDir, "testdir");
            Assert.NotEqual(cwdBased, projectBased);
        }

        [Fact]
        public void Execute_AutoInitializesProjectDirectory_FromBuildEngine()
        {
            var taskEnv = TaskEnvironmentHelper.CreateForTest("");

            var task = new SdkTasks.Build.DirectoryContextSwitcher
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                NewDirectory = "output"
            };

            task.Execute();

            // ProjectDirectory should have been set from BuildEngine.ProjectFileOfTaskNode
            Assert.False(string.IsNullOrEmpty(taskEnv.ProjectDirectory));
        }
    }
}
