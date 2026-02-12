using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class OutputRedirectorTests
    {
        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Diagnostics.OutputRedirector),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldImplementIMultiThreadableTask()
        {
            var task = new SdkTasks.Diagnostics.OutputRedirector();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void ShouldNotChangeConsoleOut()
        {
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Diagnostics.OutputRedirector
            {
                BuildEngine = engine,
                LogFilePath = "somefile.log"
            };

            var originalOut = Console.Out;

            bool result = task.Execute();

            Assert.True(result);
            Assert.Same(originalOut, Console.Out);
            Assert.Contains(engine.Messages, m => m.Message!.Contains("Redirected output to log file."));
        }
    }
}
