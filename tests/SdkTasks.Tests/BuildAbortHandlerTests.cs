using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class BuildAbortHandlerTests
    {
        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Build.BuildAbortHandler),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldImplementIMultiThreadableTask()
        {
            var task = new SdkTasks.Build.BuildAbortHandler();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void ShouldReturnFalseAndLogErrorInsteadOfExit()
        {
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Build.BuildAbortHandler
            {
                BuildEngine = engine,
                ExitCode = 1
            };

            bool result = task.Execute();

            Assert.False(result);
            Assert.Contains(engine.Errors!, e => e.Message!.Contains("exit code 1"));
        }
    }
}
