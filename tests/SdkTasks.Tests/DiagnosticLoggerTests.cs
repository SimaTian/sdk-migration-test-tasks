using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class DiagnosticLoggerTests
    {
        [Fact]
        public void ShouldWriteToBuildEngineNotConsole()
        {
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Diagnostics.DiagnosticLogger
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                Message = "Hello from task"
            };

            var originalOut = Console.Out;
            try
            {
                using var sw = new StringWriter();
                Console.SetOut(sw);

                bool result = task.Execute();

                Assert.True(result);
                // Assert CORRECT behavior: output should NOT go to Console
                string consoleOutput = sw.ToString();
                Assert.DoesNotContain("Hello from task", consoleOutput);
                // Assert CORRECT behavior: output should go to build engine
                Assert.Contains(engine.Messages, m => m.Message!.Contains("Hello from task"));
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        [Fact]
        public void ShouldLogMessageViaBuildEngine()
        {
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Diagnostics.DiagnosticLogger
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                Message = "Diagnostic info: build step completed"
            };

            bool result = task.Execute();

            // Assert CORRECT behavior: message is logged via MSBuild engine
            Assert.True(result);
            Assert.Contains(engine.Messages, m => m.Message!.Contains("Diagnostic info: build step completed"));
        }

        [Fact]
        public void ShouldHandleNullMessage()
        {
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Diagnostics.DiagnosticLogger
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                Message = null
            };

            bool result = task.Execute();

            // Assert CORRECT behavior: task should handle null message gracefully
            Assert.True(result);
        }

        [Fact]
        public void ShouldWorkWithNonCwdProjectDirectory()
        {
            var tempDir = TestHelper.CreateNonCwdTempDirectory();
            try
            {
                var engine = new MockBuildEngine();
                var tracking = new TrackingTaskEnvironment { ProjectDirectory = tempDir };
                var task = new SdkTasks.Diagnostics.DiagnosticLogger
                {
                    BuildEngine = engine,
                    TaskEnvironment = tracking,
                    Message = "Logging from non-CWD project"
                };

                bool result = task.Execute();

                Assert.True(result);
                // Verify message went to build engine, not console
                Assert.Contains(engine.Messages, m => m.Message!.Contains("Logging from non-CWD project"));
            }
            finally
            {
                TestHelper.CleanupTempDirectory(tempDir);
            }
        }

        [Fact]
        public void ShouldNotWriteToConsoleWithTrackingEnvironment()
        {
            var tempDir = TestHelper.CreateNonCwdTempDirectory();
            try
            {
                var engine = new MockBuildEngine();
                var tracking = new TrackingTaskEnvironment { ProjectDirectory = tempDir };
                var task = new SdkTasks.Diagnostics.DiagnosticLogger
                {
                    BuildEngine = engine,
                    TaskEnvironment = tracking,
                    Message = "Tracked diagnostic output"
                };

                var originalOut = Console.Out;
                try
                {
                    using var sw = new StringWriter();
                    Console.SetOut(sw);

                    bool result = task.Execute();

                    Assert.True(result);
                    string consoleOutput = sw.ToString();
                    Assert.DoesNotContain("Tracked diagnostic output", consoleOutput);
                    Assert.Contains(engine.Messages, m => m.Message!.Contains("Tracked diagnostic output"));
                }
                finally
                {
                    Console.SetOut(originalOut);
                }
            }
            finally
            {
                TestHelper.CleanupTempDirectory(tempDir);
            }
        }
    }
}
