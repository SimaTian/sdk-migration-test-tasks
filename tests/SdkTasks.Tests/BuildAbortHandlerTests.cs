using Microsoft.Build.Framework;
using SdkTasks.Build;
using SdkTasks.Tests.Infrastructure;
using Xunit;

#nullable disable

namespace SdkTasks.Tests
{
    public class BuildAbortHandlerTests
    {
        // ── Correct behavior: log error instead of Environment.Exit ─────

        [Fact]
        public void ShouldReturnTrueOnZeroExitCode()
        {
            var engine = new MockBuildEngine();
            var task = new BuildAbortHandler
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                ExitCode = 0
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Empty(engine.Errors);
            Assert.Contains(engine.Messages, m => m.Message.Contains("Build validation passed"));
        }

        [Fact]
        public void ShouldReturnFalseAndLogErrorInsteadOfExit()
        {
            var engine = new MockBuildEngine();
            var task = new BuildAbortHandler
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                ExitCode = 1
            };

            bool result = task.Execute();

            Assert.False(result);
            Assert.Contains(engine.Errors, e => e.Message.Contains("exit code 1"));
        }

        [Fact]
        public void ShouldReturnFalseAndLogErrorForNonZeroExitCode()
        {
            var projectDir = TestHelper.CreateNonCwdTempDirectory();
            try
            {
                var engine = new MockBuildEngine();
                var task = new BuildAbortHandler
                {
                    BuildEngine = engine,
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                    ExitCode = 42
                };

                bool result = task.Execute();

                // Task should return false and log error instead of calling Environment.Exit
                Assert.False(result);
                Assert.Single(engine.Errors);
                Assert.Contains(engine.Errors, e => e.Message.Contains("exit code 42"));
            }
            finally
            {
                TestHelper.CleanupTempDirectory(projectDir);
            }
        }

        // ── TaskEnvironment integration ─────────────────────────────────

        [Fact]
        public void ShouldAcceptTrackingTaskEnvironment()
        {
            var projectDir = TestHelper.CreateNonCwdTempDirectory();
            try
            {
                var engine = new MockBuildEngine();
                var tracking = SharedTestHelpers.CreateTrackingEnvironment(projectDir);
                var task = new BuildAbortHandler
                {
                    BuildEngine = engine,
                    TaskEnvironment = tracking,
                    ExitCode = 0
                };

                bool result = task.Execute();

                Assert.True(result);
            }
            finally
            {
                TestHelper.CleanupTempDirectory(projectDir);
            }
        }
    }
}
