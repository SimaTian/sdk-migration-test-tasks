using Xunit;
using Microsoft.Build.Framework;
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
                var task = new SdkTasks.Build.CriticalErrorHandler
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

        [Fact]
        public void ShouldReturnTrueWhenNoErrorMessage()
        {
            var engine = new MockBuildEngine();
            var projectDir = TestHelper.CreateNonCwdTempDirectory();
            try
            {
                var task = new SdkTasks.Build.CriticalErrorHandler
                {
                    BuildEngine = engine,
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                    ErrorMessage = string.Empty
                };

                bool result = task.Execute();

                Assert.True(result);
                Assert.Empty(engine.Errors);
            }
            finally
            {
                TestHelper.CleanupTempDirectory(projectDir);
            }
        }

        [Fact]
        public void ShouldReturnFalseAndLogErrorInsteadOfFailFast()
        {
            var engine = new MockBuildEngine();
            var projectDir = TestHelper.CreateNonCwdTempDirectory();
            try
            {
                var task = new SdkTasks.Build.CriticalErrorHandler
                {
                    BuildEngine = engine,
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                    ErrorMessage = "Something went wrong"
                };

                bool result = task.Execute();

                Assert.False(result);
                Assert.Contains(engine.Errors, e => e.Message.Contains("Something went wrong"));
            }
            finally
            {
                TestHelper.CleanupTempDirectory(projectDir);
            }
        }
    }
}
