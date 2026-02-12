using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class DiagnosticLoggerTests
    {
        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Diagnostics.DiagnosticLogger),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldImplementIMultiThreadableTask()
        {
            var task = new SdkTasks.Diagnostics.DiagnosticLogger();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void ShouldWriteToBuildEngineNotConsole()
        {
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Diagnostics.DiagnosticLogger
            {
                BuildEngine = engine,
                Message = "Hello from task"
            };

            var originalOut = Console.Out;
            try
            {
                using var sw = new StringWriter();
                Console.SetOut(sw);

                task.Execute();

                string consoleOutput = sw.ToString();
                Assert.DoesNotContain("Hello from task", consoleOutput);
                Assert.Contains(engine.Messages, m => m.Message!.Contains("Hello from task"));
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
    }
}
