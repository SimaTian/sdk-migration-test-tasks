using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;
using SdkTasks.Tools;
using Xunit;

#nullable disable

namespace SdkTasks.Tests
{
    public class ProcessTerminatorTests
    {

        [Fact]
        public void Execute_ShouldReturnFalseAndLogError()
        {
            var engine = new MockBuildEngine();
            var task = new ProcessTerminator
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest()
            };

            bool result = task.Execute();

            Assert.False(result);
            Assert.Single(engine.Errors);
        }

        [Fact]
        public void Execute_ShouldLogForbiddenOperationError()
        {
            var engine = new MockBuildEngine();
            var task = new ProcessTerminator
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest()
            };

            task.Execute();

            Assert.Contains(engine.Errors, e => e.Message.Contains("kill"));
        }

        [Fact]
        public void Execute_ShouldLogCleanupMessage()
        {
            var engine = new MockBuildEngine();
            var task = new ProcessTerminator
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest()
            };

            task.Execute();

            Assert.Contains(engine.Messages, m => m.Message.Contains("cleanup", System.StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Execute_WithProjectDirectory_ShouldNotAffectErrorBehavior()
        {
            var engine = new MockBuildEngine();
            var projectDir = TestHelper.CreateNonCwdTempDirectory();
            try
            {
                var task = new ProcessTerminator
                {
                    BuildEngine = engine,
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir)
                };

                bool result = task.Execute();

                Assert.False(result);
                Assert.Single(engine.Errors);
                Assert.Contains(engine.Errors, e => e.Message.Contains("forbidden", System.StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                TestHelper.CleanupTempDirectory(projectDir);
            }
        }
    }
}
