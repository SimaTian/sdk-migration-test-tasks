using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Build;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class WorkingDirectoryResolverTests : IDisposable
    {
        private readonly TaskTestContext _ctx;

        public WorkingDirectoryResolverTests() => _ctx = new TaskTestContext();

        private string CreateTempDir() => _ctx.CreateAdditionalProjectDir();

        public void Dispose() => _ctx.Dispose();

        [Fact]
        public void Execute_ShouldReturnProjectDirectory_NotProcessCwd()
        {
            var projectDir = CreateTempDir();

            var task = new WorkingDirectoryResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir)
            };

            bool result = task.Execute();

            // Assert CORRECT behavior: task should return ProjectDirectory, not process CWD
            Assert.True(result);
            Assert.Equal(projectDir, task.CurrentDir);
            Assert.NotEqual(Directory.GetCurrentDirectory(), task.CurrentDir);
        }

        [Fact]
        public void Execute_ShouldResolveOutputPathRelativeToProjectDirectory()
        {
            var projectDir = CreateTempDir();
            var engine = new MockBuildEngine();

            var task = new WorkingDirectoryResolver
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir)
            };

            bool result = task.Execute();

            // Assert CORRECT behavior: resolved path logged should be under ProjectDirectory
            Assert.True(result);
            var expectedOutputPath = Path.Combine(projectDir, "output");
            Assert.Contains(engine.Messages, m => m.Message!.Contains(expectedOutputPath));
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

            // Assert CORRECT behavior: process CWD should be unchanged
            Assert.Equal(originalCwd, Environment.CurrentDirectory);
        }

        [Fact]
        public void Execute_WithTrackingEnvironment_UsesProjectDirectoryProperty()
        {
            var projectDir = CreateTempDir();
            var tracking = SharedTestHelpers.CreateTrackingEnvironment(projectDir);

            var task = new WorkingDirectoryResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = tracking
            };

            bool result = task.Execute();

            // Task should use TaskEnvironment.ProjectDirectory rather than Environment.CurrentDirectory
            Assert.True(result);
            Assert.Equal(projectDir, task.CurrentDir);
        }

        [Fact]
        public void Execute_AutoInitializesProjectDirectory_WhenEmpty()
        {
            // When ProjectDirectory is empty, the task should auto-initialize from BuildEngine
            var taskEnv = TaskEnvironmentHelper.CreateForTest(string.Empty);

            var task = new WorkingDirectoryResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv
            };

            task.Execute();

            // ProjectDirectory should have been set from BuildEngine.ProjectFileOfTaskNode
            Assert.False(string.IsNullOrEmpty(task.CurrentDir));
        }

        [Fact]
        public void Execute_LogsResolvedPath()
        {
            var projectDir = CreateTempDir();
            var engine = new MockBuildEngine();

            var task = new WorkingDirectoryResolver
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir)
            };

            task.Execute();

            Assert.Contains(engine.Messages, m => m.Message!.Contains("Resolved path:"));
        }
    }
}
