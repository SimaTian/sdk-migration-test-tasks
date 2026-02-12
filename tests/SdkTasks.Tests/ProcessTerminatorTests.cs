using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class ProcessTerminatorTests
    {
        [Fact]
        public void ShouldReturnFalseAndLogError()
        {
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Tools.ProcessTerminator
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest()
            };

            bool result = task.Execute();

            // Assert CORRECT behavior: task returns false and logs error instead of calling Process.Kill
            Assert.False(result);
            Assert.Single(engine.Errors);
        }

        [Fact]
        public void ShouldNotKillCurrentProcess()
        {
            // If the task called Process.Kill on the current process, the test would terminate.
            // The fact that this test completes successfully proves it does not.
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Tools.ProcessTerminator
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest()
            };

            bool result = task.Execute();

            // We're still alive — Process.Kill was not called on the current process
            Assert.False(result);
            Assert.Contains(engine.Errors!, e => e.Message!.Contains("forbidden"));
        }

        [Fact]
        public void ShouldLogProcessInfo()
        {
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Tools.ProcessTerminator
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest()
            };

            task.Execute();

            // Assert CORRECT behavior: task should log process info via build engine
            Assert.Contains(engine.Messages, m => m.Message!.Contains("PID:"));
        }
    }
}
