using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Build;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class WorkingDirectoryResolverTests : IDisposable
    {
        private readonly List<string> _tempDirs = new();

        private string CreateTempDir()
        {
            var dir = TestHelper.CreateNonCwdTempDirectory();
            _tempDirs.Add(dir);
            return dir;
        }

        public void Dispose()
        {
            foreach (var dir in _tempDirs)
                TestHelper.CleanupTempDirectory(dir);
        }

        [Fact]
        public void Execute_ShouldReturnProjectDirectory_NotProcessCwd()
        {
            var projectDir = CreateTempDir();

            var task = new WorkingDirectoryResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir)
            };

            var result = task.Execute();

            Assert.True(result);
            // Task should return ProjectDirectory, not process CWD
            Assert.Equal(projectDir, task.CurrentDir);
            Assert.NotEqual(Environment.CurrentDirectory, task.CurrentDir);
        }

        [Fact]
        public void Execute_ShouldResolvePath_RelativeToProjectDirectory()
        {
            var projectDir = CreateTempDir();

            var engine = new MockBuildEngine();
            var task = new WorkingDirectoryResolver
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir)
            };

            task.Execute();

            // The logged message should contain a path rooted in the project directory
            var expectedPath = Path.Combine(projectDir, "output");
            Assert.Contains(engine.Messages, m => m.Message?.Contains(expectedPath) == true);
        }

        [Fact]
        public void Execute_ShouldNotModifyProcessCwd()
        {
            var projectDir = CreateTempDir();
            var originalCwd = Environment.CurrentDirectory;

            var task = new WorkingDirectoryResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir)
            };

            task.Execute();

            // Process CWD should remain unchanged
            Assert.Equal(originalCwd, Environment.CurrentDirectory);
        }

        [Fact]
        public void Execute_WithTrackingEnvironment_ShouldUseTaskEnvironment()
        {
            var projectDir = CreateTempDir();

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = projectDir };
            var task = new WorkingDirectoryResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = trackingEnv
            };

            task.Execute();

            // Task should use TaskEnvironment.ProjectDirectory, not Environment.CurrentDirectory
            Assert.Equal(projectDir, task.CurrentDir);
        }

        [Fact]
        public void Execute_WithDifferentProjectDirs_ShouldReflectEachDirectory()
        {
            var projectDir1 = CreateTempDir();
            var projectDir2 = CreateTempDir();

            var task1 = new WorkingDirectoryResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir1)
            };
            var task2 = new WorkingDirectoryResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir2)
            };

            task1.Execute();
            task2.Execute();

            Assert.Equal(projectDir1, task1.CurrentDir);
            Assert.Equal(projectDir2, task2.CurrentDir);
        }

        [Fact]
        public void Execute_AutoInitializesProjectDirectory_FromBuildEngine()
        {
            var engine = new MockBuildEngine();
            var taskEnv = new TaskEnvironment { ProjectDirectory = string.Empty };

            var task = new WorkingDirectoryResolver
            {
                BuildEngine = engine,
                TaskEnvironment = taskEnv
            };

            task.Execute();

            // ProjectDirectory should be auto-initialized from BuildEngine.ProjectFileOfTaskNode
            Assert.False(string.IsNullOrEmpty(task.TaskEnvironment.ProjectDirectory));
        }
    }
}
