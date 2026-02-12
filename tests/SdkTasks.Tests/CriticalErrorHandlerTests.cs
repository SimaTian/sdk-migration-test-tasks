using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class CriticalErrorHandlerTests
    {
        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Build.CriticalErrorHandler),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldImplementIMultiThreadableTask()
        {
            var task = new SdkTasks.Build.CriticalErrorHandler();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void ShouldReturnFalseAndLogErrorInsteadOfFailFast()
        {
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Build.CriticalErrorHandler
            {
                BuildEngine = engine,
                ErrorMessage = "Something went wrong"
            };

            bool result = task.Execute();

            Assert.False(result);
            Assert.Contains(engine.Errors!, e => e.Message!.Contains("Something went wrong"));
        }
    }
}
