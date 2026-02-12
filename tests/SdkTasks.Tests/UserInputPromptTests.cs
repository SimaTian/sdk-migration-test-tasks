using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class UserInputPromptTests
    {
        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Diagnostics.UserInputPrompt),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldImplementIMultiThreadableTask()
        {
            var task = new SdkTasks.Diagnostics.UserInputPrompt();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void ShouldReadFromPropertyNotConsole()
        {
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Diagnostics.UserInputPrompt
            {
                BuildEngine = engine
            };

            var originalIn = Console.In;
            try
            {
                Console.SetIn(new StringReader("should not be read"));

                task.Execute();

                Assert.NotEqual("should not be read", task.UserInput);
            }
            finally
            {
                Console.SetIn(originalIn);
            }
        }
    }
}
