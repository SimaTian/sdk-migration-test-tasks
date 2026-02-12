using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;
using SdkTasks.Tools;

namespace SdkTasks.Tests
{
    public class ToolchainInvokerMultiThreadingTests : IDisposable
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
        public void ItImplementsIMultiThreadableTask()
        {
            var task = new ToolchainInvoker();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void ItHasMSBuildMultiThreadableTaskAttribute()
        {
            Assert.NotEmpty(
                typeof(ToolchainInvoker).GetCustomAttributes(typeof(MSBuildMultiThreadableTaskAttribute), false));
        }

        [Fact]
        public void ItUsesTaskEnvironmentForProcessStartInfo()
        {
            var projectDir = CreateProjectDir();
            var toolName = Path.Combine(projectDir, "testtool.cmd");
            File.WriteAllText(toolName, "@echo TOOL_OUTPUT_OK");

            var task = new ToolchainInvoker
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                ToolName = toolName,
                Arguments = string.Empty,
                TimeoutMilliseconds = 10000,
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.Contains("TOOL_OUTPUT_OK", task.ToolOutput);
        }

        [Fact]
        public void ItReadsPathEnvironmentVariableViaTaskEnvironment()
        {
            var projectDir = CreateProjectDir();
            var customToolDir = Path.Combine(Path.GetTempPath(), "tooldir-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(customToolDir);
            _tempDirs.Add(customToolDir);

            var toolPath = Path.Combine(customToolDir, "customtool.cmd");
            File.WriteAllText(toolPath, "@echo CUSTOM_PATH_OK");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(projectDir);
            taskEnv.SetEnvironmentVariable("PATH", customToolDir);

            var task = new ToolchainInvoker
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                ToolName = toolPath,
                Arguments = string.Empty,
                TimeoutMilliseconds = 10000,
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.Contains("CUSTOM_PATH_OK", task.ToolOutput);
        }

        [Fact]
        public void ItReturnsFalseAndLogsErrorOnTimeout()
        {
            var projectDir = CreateProjectDir();
            var toolName = Path.Combine(projectDir, "slowtool.cmd");
            File.WriteAllText(toolName, "@ping -n 30 127.0.0.1 > nul");

            var engine = new MockBuildEngine();
            var task = new ToolchainInvoker
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                ToolName = toolName,
                Arguments = string.Empty,
                TimeoutMilliseconds = 500,
            };

            var result = task.Execute();

            Assert.False(result);
            Assert.Contains(engine.Errors, e => e.Message.Contains("did not exit within"));
        }

        [Fact]
        public void ItReturnsFalseOnNonZeroExitCode()
        {
            var projectDir = CreateProjectDir();
            var toolName = Path.Combine(projectDir, "failtool.cmd");
            File.WriteAllText(toolName, "@exit /b 1");

            var engine = new MockBuildEngine();
            var task = new ToolchainInvoker
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                ToolName = toolName,
                Arguments = string.Empty,
                TimeoutMilliseconds = 10000,
            };

            var result = task.Execute();

            Assert.False(result);
            Assert.Contains(engine.Errors, e => e.Message.Contains("exited with code 1"));
        }

        [Fact]
        public void ItCallsGetEnvironmentVariableOnTaskEnvironment()
        {
            var projectDir = CreateProjectDir();
            var trackingEnv = SharedTestHelpers.CreateTrackingEnv(projectDir);

            var task = new ToolchainInvoker
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = trackingEnv,
                ToolName = "cmd.exe",
                Arguments = "/c echo hello",
                TimeoutMilliseconds = 10000,
            };

            task.Execute();

            SharedTestHelpers.AssertGetEnvironmentVariableCalled(trackingEnv);
        }
    }
}
