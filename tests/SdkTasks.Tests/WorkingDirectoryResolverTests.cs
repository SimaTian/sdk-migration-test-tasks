using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class WorkingDirectoryResolverTests : IDisposable
    {
        private readonly string _projectDir;

        public WorkingDirectoryResolverTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldReadProjectDirectory()
        {
            var task = new SdkTasks.Build.WorkingDirectoryResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
            };

            task.Execute();

            Assert.Equal(_projectDir, task.CurrentDir);
        }

        [Fact]
        public void ShouldNotReturnProcessCwd()
        {
            // ProjectDirectory is a temp dir different from process CWD
            var task = new SdkTasks.Build.WorkingDirectoryResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
            };

            task.Execute();

            // CurrentDir must be ProjectDirectory, not the process working directory
            Assert.NotEqual(Environment.CurrentDirectory, task.CurrentDir);
        }

        [Fact]
        public void ResolvedPathShouldBeUnderProjectDirectory()
        {
            var engine = new MockBuildEngine();

            var task = new SdkTasks.Build.WorkingDirectoryResolver
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
            };

            task.Execute();

            // The task combines CurrentDir with "output"; verify log message contains ProjectDirectory
            var loggedPath = Path.Combine(_projectDir, "output");
            Assert.Contains(loggedPath, engine.Messages.Select(m => m.Message).FirstOrDefault() ?? "",
                StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Execute_WithTrackingEnvironment_UsesTaskEnvironment()
        {
            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);

            var task = new SdkTasks.Build.WorkingDirectoryResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = trackingEnv
            };

            task.Execute();

            // Task should use TaskEnvironment.ProjectDirectory rather than Environment.CurrentDirectory
            Assert.Equal(_projectDir, task.CurrentDir);
        }

        [Fact]
        public void Execute_AutoInitializesProjectDirectory_FromBuildEngine()
        {
            // When ProjectDirectory is empty, the task should auto-initialize from BuildEngine
            var taskEnv = TaskEnvironmentHelper.CreateForTest("");

            var task = new SdkTasks.Build.WorkingDirectoryResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv
            };

            task.Execute();

            // ProjectDirectory should have been set from BuildEngine.ProjectFileOfTaskNode
            Assert.False(string.IsNullOrEmpty(task.CurrentDir));
        }
    }
}
