using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;
using SdkTasks.Tools;

namespace SdkTasks.Tests
{
    public class ToolchainInvokerTests : IDisposable
    {
        private readonly TaskTestContext _ctx;

        public ToolchainInvokerTests() => _ctx = new TaskTestContext();

        private string CreateProjectDir() => _ctx.CreateAdditionalProjectDir();

        public void Dispose() => _ctx.Dispose();

        // ── Interface & Attribute ───────────────────────────────────────

        // ── Process runs in ProjectDirectory, not CWD ───────────────────

        [Fact]
        public void ShouldRunInProjectDirectory()
        {
            var dir1 = CreateProjectDir();
            var dir2 = CreateProjectDir();

            var task1 = new ToolchainInvoker
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir1),
                ToolName = "cmd.exe",
                Arguments = "/c cd",
                TimeoutMilliseconds = 5000,
            };

            var task2 = new ToolchainInvoker
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir2),
                ToolName = "cmd.exe",
                Arguments = "/c cd",
                TimeoutMilliseconds = 5000,
            };

            Assert.True(task1.Execute());
            Assert.True(task2.Execute());

            // Assert CORRECT behavior: each process runs in its own ProjectDirectory
            Assert.Contains(dir1, task1.ToolOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(dir2, task2.ToolOutput, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(task1.ToolOutput.Trim(), task2.ToolOutput.Trim());
        }

        // ── Uses TaskEnvironment.GetEnvironmentVariable ─────────────────

        [Fact]
        public void ShouldReadPathFromTaskEnvironment()
        {
            var projectDir = CreateProjectDir();
            var tracking = SharedTestHelpers.CreateTrackingEnvironment(projectDir);

            var task = new ToolchainInvoker
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = tracking,
                ToolName = "cmd.exe",
                Arguments = "/c echo hello",
                TimeoutMilliseconds = 5000,
            };

            task.Execute();

            // Assert CORRECT behavior: task reads PATH via TaskEnvironment, not System.Environment
            SharedTestHelpers.AssertUsesGetEnvironmentVariable(tracking);
        }

        // ── Uses TaskEnvironment.GetProcessStartInfo ────────────────────

        [Fact]
        public void ShouldUseTaskEnvironmentProcessStartInfo()
        {
            var projectDir = CreateProjectDir();

            var task = new ToolchainInvoker
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                ToolName = "cmd.exe",
                Arguments = "/c cd",
                TimeoutMilliseconds = 5000,
            };

            bool result = task.Execute();

            // Assert CORRECT behavior: working directory comes from TaskEnvironment, not process CWD
            Assert.True(result);
            Assert.Contains(projectDir, task.ToolOutput, StringComparison.OrdinalIgnoreCase);
        }

        // ── ProjectDirectory auto-initialization from BuildEngine ───────

        [Fact]
        public void ShouldAutoInitializeProjectDirectoryFromBuildEngine()
        {
            var engine = new MockBuildEngine();

            var task = new ToolchainInvoker
            {
                BuildEngine = engine,
                TaskEnvironment = new TaskEnvironment(),
                ToolName = "cmd.exe",
                Arguments = "/c echo ok",
                TimeoutMilliseconds = 5000,
            };

            bool result = task.Execute();

            // Assert CORRECT behavior: task should still execute by deriving ProjectDirectory from BuildEngine
            Assert.True(result);
        }

        // ── Validation: empty ToolName ──────────────────────────────────

        [Fact]
        public void ShouldFailWhenToolNameIsEmpty()
        {
            var engine = new MockBuildEngine();

            var task = new ToolchainInvoker
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                ToolName = "",
                TimeoutMilliseconds = 5000,
            };

            bool result = task.Execute();

            Assert.False(result);
            Assert.Contains(engine.Errors, e => e.Message!.Contains("ToolName"));
        }

        // ── Task-scoped env vars: two tasks get different PATH values ───

        [Fact]
        public void ShouldUseScopedEnvironmentVariables()
        {
            var dir1 = CreateProjectDir();
            var dir2 = CreateProjectDir();

            var taskEnv1 = TaskEnvironmentHelper.CreateForTest(dir1);
            taskEnv1.SetEnvironmentVariable("PATH", dir1);

            var taskEnv2 = TaskEnvironmentHelper.CreateForTest(dir2);
            taskEnv2.SetEnvironmentVariable("PATH", dir2);

            var task1 = new ToolchainInvoker
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv1,
                ToolName = "cmd.exe",
                Arguments = "/c echo %PATH%",
                TimeoutMilliseconds = 5000,
            };

            var task2 = new ToolchainInvoker
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv2,
                ToolName = "cmd.exe",
                Arguments = "/c echo %PATH%",
                TimeoutMilliseconds = 5000,
            };

            Assert.True(task1.Execute());
            Assert.True(task2.Execute());

            // Assert CORRECT behavior: each task gets its own PATH from TaskEnvironment
            Assert.Contains(dir1, task1.ToolOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(dir2, task2.ToolOutput, StringComparison.OrdinalIgnoreCase);
        }
    }
}
