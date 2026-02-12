using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class ToolchainInvokerTests : IDisposable
    {
        private readonly List<string> _tempDirs = new();

        private string CreateProjectDir()
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
        public void ShouldRunInProjectDirectory()
        {
            var dir1 = CreateProjectDir();
            var dir2 = CreateProjectDir();

            var task1 = new SdkTasks.Tools.ToolchainInvoker
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir1),
                ToolName = "cmd.exe",
                Arguments = "/c cd",
                TimeoutMilliseconds = 5000,
            };

            var task2 = new SdkTasks.Tools.ToolchainInvoker
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir2),
                ToolName = "cmd.exe",
                Arguments = "/c cd",
                TimeoutMilliseconds = 5000,
            };

            Assert.True(task1.Execute());
            Assert.True(task2.Execute());

            Assert.Contains(dir1, task1.ToolOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(dir2, task2.ToolOutput, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(task1.ToolOutput.Trim(), task2.ToolOutput.Trim());
        }

        [Fact]
        public void ShouldCallGetEnvironmentVariableOnTaskEnvironment()
        {
            var projectDir = CreateProjectDir();
            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = projectDir };

            var task = new SdkTasks.Tools.ToolchainInvoker
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = trackingEnv,
                ToolName = "cmd.exe",
                Arguments = "/c echo hello",
                TimeoutMilliseconds = 5000,
            };

            Assert.True(task.Execute());

            SharedTestHelpers.AssertGetEnvironmentVariableCalled(trackingEnv);
        }

        [Fact]
        public void ShouldNotRunInProcessCwd()
        {
            var projectDir = CreateProjectDir();
            var cwd = Directory.GetCurrentDirectory();

            var task = new SdkTasks.Tools.ToolchainInvoker
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                ToolName = "cmd.exe",
                Arguments = "/c cd",
                TimeoutMilliseconds = 5000,
            };

            Assert.True(task.Execute());

            // Process should run in ProjectDirectory, not the test process CWD
            Assert.Contains(projectDir, task.ToolOutput, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(cwd, task.ToolOutput, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldUseTaskEnvironmentEnvVarsForProcess()
        {
            var projectDir = CreateProjectDir();
            var taskEnv = TaskEnvironmentHelper.CreateForTest(projectDir);
            taskEnv.SetEnvironmentVariable("MY_TOOL_VAR", "task-scoped-value");

            var task = new SdkTasks.Tools.ToolchainInvoker
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                ToolName = "cmd.exe",
                Arguments = "/c cd",
                TimeoutMilliseconds = 5000,
            };

            Assert.True(task.Execute());
            Assert.Contains(projectDir, task.ToolOutput, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldSetWorkingDirectoryFromTaskEnvironment()
        {
            var projectDir = CreateProjectDir();
            var engine = new MockBuildEngine();

            var task = new SdkTasks.Tools.ToolchainInvoker
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                ToolName = "cmd.exe",
                Arguments = "/c cd",
                TimeoutMilliseconds = 5000,
            };

            Assert.True(task.Execute());

            // The tool output (cd) should report the ProjectDirectory
            Assert.StartsWith(projectDir, task.ToolOutput.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
