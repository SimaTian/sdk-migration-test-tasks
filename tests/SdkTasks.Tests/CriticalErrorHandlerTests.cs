using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class CriticalErrorHandlerTests
    {
        private readonly MockBuildEngine _engine = new();

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Build.CriticalErrorHandler),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldNotImplementIMultiThreadableTask()
        {
            // Attribute-only tasks must NOT implement IMultiThreadableTask.
            var task = new SdkTasks.Build.CriticalErrorHandler();
            Assert.False(task is IMultiThreadableTask,
                "CriticalErrorHandler has no forbidden APIs and should not implement IMultiThreadableTask");
        }

        [Fact]
        public void ShouldReturnTrueWhenNoErrorMessage()
        {
            var task = new SdkTasks.Build.CriticalErrorHandler
            {
                BuildEngine = _engine,
                ErrorMessage = string.Empty
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Empty(_engine.Errors);
            Assert.Contains(_engine.Messages, m => m.Message.Contains("No critical errors found"));
        }

        [Fact]
        public void ShouldReturnTrueWhenErrorMessageIsNull()
        {
            var task = new SdkTasks.Build.CriticalErrorHandler
            {
                BuildEngine = _engine,
                ErrorMessage = null!
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Empty(_engine.Errors);
        }

        [Fact]
        public void ShouldReturnFalseAndLogErrorWhenErrorMessageIsSet()
        {
            var task = new SdkTasks.Build.CriticalErrorHandler
            {
                BuildEngine = _engine,
                ErrorMessage = "Disk full"
            };

            bool result = task.Execute();

            Assert.False(result);
            Assert.Contains(_engine.Errors, e => e.Message.Contains("Critical error detected: Disk full"));
        }

        [Fact]
        public void ShouldLogCheckingMessageBeforeEvaluating()
        {
            var task = new SdkTasks.Build.CriticalErrorHandler
            {
                BuildEngine = _engine,
                ErrorMessage = "something"
            };

            task.Execute();

            Assert.Contains(_engine.Messages, m => m.Message.Contains("Checking for critical errors"));
        }
    }
}
