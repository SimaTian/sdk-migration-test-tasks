using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class UserInputPromptTests
    {

        [Fact]
        public void Execute_ShouldNotReadFromConsoleIn()
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
                Console.SetIn(new StringReader("should not be read"));

                bool result = task.Execute();

                Assert.True(result);
                // Task must NOT read from Console.In in multithreaded builds
                Assert.NotEqual("should not be read", task.UserInput);
            }
            finally
            {
                Console.SetIn(originalIn);
            }
        }

        [Fact]
        public void Execute_ShouldReturnEmptyUserInput()
        {
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Diagnostics.UserInputPrompt
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest()
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(string.Empty, task.UserInput);
        }

        [Fact]
        public void Execute_ShouldLogWarningAboutInteractiveInput()
        {
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Diagnostics.UserInputPrompt
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest()
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(engine.Warnings,
                w => w.Message!.Contains("Interactive console input is not supported in multithreaded builds"));
        }

        [Fact]
        public void Execute_WithNonCwdProjectDirectory_ShouldSucceed()
        {
            var tempDir = TestHelper.CreateNonCwdTempDirectory();
            try
            {
                var engine = new MockBuildEngine();
                var task = new SdkTasks.Diagnostics.UserInputPrompt
                {
                    BuildEngine = engine,
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(tempDir)
                };

                var originalIn = Console.In;
                try
                {
                    Console.SetIn(new StringReader("console input that must be ignored"));

                    bool result = task.Execute();

                    Assert.True(result);
                    Assert.Equal(string.Empty, task.UserInput);
                    Assert.NotEqual("console input that must be ignored", task.UserInput);
                }
                finally
                {
                    Console.SetIn(originalIn);
                }
            }
            finally
            {
                TestHelper.CleanupTempDirectory(tempDir);
            }
        }
    }
}
