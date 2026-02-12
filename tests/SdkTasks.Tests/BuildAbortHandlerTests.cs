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

            // Assert CORRECT behavior: task returns false and logs error instead of calling Environment.Exit
            Assert.False(result);
            Assert.Contains(engine.Errors!, e => e.Message!.Contains("exit code 1"));
        }

        [Fact]
        public void ShouldReturnTrueWhenExitCodeIsZero()
        {
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Build.BuildAbortHandler
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                ExitCode = 0
            };

            bool result = task.Execute();

            // Assert CORRECT behavior: task succeeds for exit code 0
            Assert.True(result);
            Assert.Empty(engine.Errors);
        }

        [Fact]
        public void ShouldNotCallEnvironmentExit()
        {
            // If the task called Environment.Exit, the test process would terminate.
            // The fact that this test completes successfully proves the task does not call it.
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Build.BuildAbortHandler
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                ExitCode = 42
            };

            bool result = task.Execute();

            // We're still alive — Environment.Exit was not called
            Assert.False(result);
            Assert.Contains(engine.Errors!, e => e.Message!.Contains("42"));
        }
    }
}
