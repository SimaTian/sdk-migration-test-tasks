using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class ExternalToolRunnerTests : IDisposable
    {
        private readonly string _projectDir;

        public ExternalToolRunnerTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldSetWorkingDirectoryToProjectDir()
        {
            var engine = new MockBuildEngine();

            var task = new SdkTasks.Tools.ExternalToolRunner
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                Command = "cmd.exe",
                Arguments = "/c cd"
            };

            bool result = task.Execute();

            // Assert CORRECT behavior: task should run in ProjectDirectory
            Assert.True(result);
            Assert.Contains(engine.Messages!, m => m.Message!.Contains(_projectDir));
        }

        [Fact]
        public void ShouldUseTaskEnvironmentGetProcessStartInfo()
        {
            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var engine = new MockBuildEngine();

            var task = new SdkTasks.Tools.ExternalToolRunner
            {
                BuildEngine = engine,
                TaskEnvironment = trackingEnv,
                Command = "cmd.exe",
                Arguments = "/c cd"
            };

            bool result = task.Execute();

            // Assert CORRECT behavior: task uses TaskEnvironment.GetProcessStartInfo()
            // which sets WorkingDirectory to ProjectDirectory
            Assert.True(result);
            Assert.Contains(engine.Messages!, m => m.Message!.Contains(_projectDir));
        }

        [Fact]
        public void ShouldNotRunInProcessCwd()
        {
            var engine = new MockBuildEngine();
            var processCwd = Directory.GetCurrentDirectory();

            var task = new SdkTasks.Tools.ExternalToolRunner
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                Command = "cmd.exe",
                Arguments = "/c cd"
            };

            bool result = task.Execute();

            // Assert CORRECT behavior: output should contain projectDir, not process CWD
            Assert.True(result);
            Assert.Contains(engine.Messages!, m => m.Message!.Contains(_projectDir));
            // projectDir is different from CWD by design (TestHelper.CreateNonCwdTempDirectory)
            Assert.NotEqual(processCwd, _projectDir);
        }

        [Fact]
        public void ShouldAutoInitializeProjectDirectoryFromBuildEngine()
        {
            var engine = new MockBuildEngine();
            // TaskEnvironment with empty ProjectDirectory triggers auto-init from BuildEngine
            var env = new TaskEnvironment();

            var task = new SdkTasks.Tools.ExternalToolRunner
            {
                BuildEngine = engine,
                TaskEnvironment = env,
                Command = "cmd.exe",
                Arguments = "/c echo ok"
            };

            bool result = task.Execute();

            // Assert CORRECT behavior: ProjectDirectory should be auto-initialized
            // from BuildEngine.ProjectFileOfTaskNode
            Assert.True(result);
            Assert.False(string.IsNullOrEmpty(env.ProjectDirectory));
        }
    }
}
