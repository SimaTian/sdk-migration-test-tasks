using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Diagnostics;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class UserInputPromptTests : IDisposable
    {
        private readonly TaskTestContext _ctx;

        public UserInputPromptTests() => _ctx = new TaskTestContext();

        private string CreateTempDir() => _ctx.CreateAdditionalProjectDir();

        public void Dispose() => _ctx.Dispose();

        // ── Does not read from Console.In ───────────────────────────────

        [Fact]
        public void Execute_ShouldNotReadFromConsoleIn()
        {
            var engine = new MockBuildEngine();
            var task = new UserInputPrompt
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

        // ── Returns empty UserInput when no DefaultInput ────────────────

        [Fact]
        public void Execute_ShouldReturnEmptyUserInput()
        {
            var engine = new MockBuildEngine();
            var task = new UserInputPrompt
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest()
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(string.Empty, task.UserInput);
        }

        // ── Logs warning about interactive input ────────────────────────

        [Fact]
        public void Execute_ShouldLogWarningAboutInteractiveInput()
        {
            var engine = new MockBuildEngine();
            var task = new UserInputPrompt
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest()
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(engine.Warnings,
                w => w.Message!.Contains("Interactive console input is not supported in multithreaded builds"));
        }

        // ── With non-CWD ProjectDirectory ───────────────────────────────

        [Fact]
        public void Execute_WithNonCwdProjectDirectory_ShouldNotReadFromConsole()
        {
            var tempDir = CreateTempDir();
            var engine = new MockBuildEngine();
            var task = new UserInputPrompt
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

        // ── DefaultInput property does not cause Console read ───────────

        [Fact]
        public void Execute_WithDefaultInput_ShouldNotReadFromConsole()
        {
            var engine = new MockBuildEngine();
            var task = new UserInputPrompt
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
                // Task should use DefaultInput or empty, not Console.In
                Assert.NotEqual("should not be read", task.UserInput);
            }
            finally
            {
                Console.SetIn(originalIn);
            }
        }

    }
}
