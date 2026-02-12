using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Build;
using SdkTasks.Tests.Infrastructure;

#nullable disable

namespace SdkTasks.Tests
{
    public class CriticalErrorHandlerTests
    {
        [Fact]
        public void ShouldAcceptTrackingTaskEnvironment()
        {
            var projectDir = TestHelper.CreateNonCwdTempDirectory();
            try
            {
                var tracking = SharedTestHelpers.CreateTrackingEnvironment(projectDir);
                var engine = new MockBuildEngine();
                var task = new CriticalErrorHandler
                {
                    BuildEngine = engine,
                    TaskEnvironment = tracking,
                    ErrorMessage = string.Empty
                };

                bool result = task.Execute();

                Assert.True(result);
            }
            finally
            {
                TestHelper.CleanupTempDirectory(projectDir);
            }
        }

        // ── Execute behavior ────────────────────────────────────────────

        [Fact]
        public void ShouldReturnTrueWhenNoErrorMessage()
        {
            var engine = new MockBuildEngine();
            var projectDir = TestHelper.CreateNonCwdTempDirectory();
            try
            {
                var task = new CriticalErrorHandler
                {
                    BuildEngine = engine,
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                    ErrorMessage = string.Empty
                };

                bool result = task.Execute();

                // Assert CORRECT behavior: no errors means success
                Assert.True(result);
                Assert.Empty(engine.Errors);
            }
            finally
            {
                TestHelper.CleanupTempDirectory(projectDir);
            }
        }

        [Fact]
        public void ShouldReturnTrueWhenErrorMessageIsNull()
        {
            var engine = new MockBuildEngine();
            var task = new CriticalErrorHandler
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                ErrorMessage = null
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Empty(engine.Errors);
        }

        [Fact]
        public void ShouldReturnFalseAndLogErrorInsteadOfFailFast()
        {
            var engine = new MockBuildEngine();
            var projectDir = TestHelper.CreateNonCwdTempDirectory();
            try
            {
                var task = new CriticalErrorHandler
                {
                    BuildEngine = engine,
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                    ErrorMessage = "Something went wrong"
                };

                // If the task called Environment.FailFast, this would crash the test host
                bool result = task.Execute();

                // Assert CORRECT behavior: task should return false and log the error
                Assert.False(result);
                Assert.Contains(engine.Errors, e => e.Message.Contains("Something went wrong"));
            }
            finally
            {
                TestHelper.CleanupTempDirectory(projectDir);
            }
        }

        [Fact]
        public void ShouldLogCheckingMessage_WhenExecuted()
        {
            var engine = new MockBuildEngine();
            var task = new CriticalErrorHandler
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                ErrorMessage = string.Empty
            };

            task.Execute();

            // Assert CORRECT behavior: task should log informational message
            Assert.Contains(engine.Messages, m => m.Message.Contains("Checking for critical errors"));
        }

        [Fact]
        public void ShouldNotCrashProcess_WhenCriticalErrorDetected()
        {
            // Verifies the task logs errors via BuildEngine instead of calling Environment.FailFast
            var engine = new MockBuildEngine();
            var task = new CriticalErrorHandler
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                ErrorMessage = "Fatal failure"
            };

            bool result = task.Execute();

            Assert.False(result);
            Assert.Contains(engine.Errors, e => e.Message.Contains("Fatal failure"));
        }
    }
}
