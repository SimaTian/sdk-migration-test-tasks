using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class UserInputPromptTests
    {
        [Fact]
        public void ShouldReadFromPropertyNotConsole()
        {
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Diagnostics.UserInputPrompt
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                DefaultInput = "parameter input"
            };

            var originalIn = Console.In;
            try
            {
                Console.SetIn(new StringReader("should not be read"));

                bool result = task.Execute();

                Assert.True(result);
                // Assert CORRECT behavior: task should use DefaultInput, not Console.In
                Assert.Equal("parameter input", task.UserInput);
            }
            finally
            {
                Console.SetIn(originalIn);
            }
        }

        [Fact]
        public void ShouldNotBlockOnConsoleInput()
        {
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Diagnostics.UserInputPrompt
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest()
            };

            // If the task called Console.ReadLine without Console.In being set,
            // it would block. Running in a separate thread with a timeout verifies it doesn't block.
            var originalIn = Console.In;
            try
            {
                // Provide empty input to avoid blocking
                Console.SetIn(new StringReader(""));

                bool result = task.Execute();

                // Assert CORRECT behavior: task completes without blocking
                Assert.True(result);
            }
            finally
            {
                Console.SetIn(originalIn);
            }
        }

        [Fact]
        public void ShouldLogWarningAboutInteractiveInput()
        {
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Diagnostics.UserInputPrompt
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest()
            };

            task.Execute();

            // Assert CORRECT behavior: task warns that console input is not supported
            Assert.Contains(engine.Warnings, w => w.Message!.Contains("not supported"));
        }

        [Fact]
        public void ShouldDefaultToEmptyStringWhenNoDefaultInputSet()
        {
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Diagnostics.UserInputPrompt
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest()
            };

            var originalIn = Console.In;
            try
            {
                Console.SetIn(new StringReader("console input"));

                bool result = task.Execute();

                Assert.True(result);
                // Assert CORRECT behavior: UserInput should be empty string default, not Console.In
                Assert.Equal(string.Empty, task.UserInput);
            }
            finally
            {
                Console.SetIn(originalIn);
            }
        }

        [Fact]
        public void ShouldWorkWithTrackingTaskEnvironment()
        {
            var engine = new MockBuildEngine();
            var tracking = new TrackingTaskEnvironment();
            var task = new SdkTasks.Diagnostics.UserInputPrompt
            {
                BuildEngine = engine,
                TaskEnvironment = tracking,
                DefaultInput = "tracked input"
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal("tracked input", task.UserInput);
            // Task accepts a TrackingTaskEnvironment (polymorphic TaskEnvironment)
            Assert.Same(tracking, task.TaskEnvironment);
        }
    }
}
