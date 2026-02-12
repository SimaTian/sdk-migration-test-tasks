using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class BuildAbortHandlerTests
    {

        [Fact]
        public void ShouldReturnFalseAndLogErrorInsteadOfExit()
        {
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Build.BuildAbortHandler
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                ExitCode = 1
            };

            bool result = task.Execute();

            Assert.False(result);
            Assert.Contains(engine.Errors!, e => e.Message!.Contains("exit code 1"));
        }

        [Fact]
        public void ShouldReturnTrueOnZeroExitCode()
        {
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Build.BuildAbortHandler
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                ExitCode = 0
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Empty(engine.Errors);
            Assert.Contains(engine.Messages, m => m.Message!.Contains("Build validation passed"));
        }

        [Fact]
        public void ShouldReturnFalseAndLogErrorForNonZeroExitCode()
        {
            var projectDir = TestHelper.CreateNonCwdTempDirectory();
            try
            {
                var engine = new MockBuildEngine();
                var task = new SdkTasks.Build.BuildAbortHandler
                {
                    BuildEngine = engine,
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                    ExitCode = 42
                };

                bool result = task.Execute();

                // Task should return false and log error instead of calling Environment.Exit
                Assert.False(result);
                Assert.Single(engine.Errors);
                Assert.Contains(engine.Errors!, e => e.Message!.Contains("exit code 42"));
            }
            finally
            {
                TestHelper.CleanupTempDirectory(projectDir);
            }
        }
    }
}
