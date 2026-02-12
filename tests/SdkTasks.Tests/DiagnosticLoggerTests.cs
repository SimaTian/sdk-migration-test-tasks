using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class DiagnosticLoggerTests : IDisposable
    {
        private readonly List<string> _tempDirs = new();

        private string CreateTempDir()
        {
            var dir = TestHelper.CreateNonCwdTempDirectory();
            _tempDirs.Add(dir);
            return dir;
        }

        public void Dispose()
        {
            foreach (var dir in _tempDirs)
                TestHelper.CleanupTempDirectory(dir);
        }

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
        public void ShouldExecuteWithNonCwdProjectDirectory()
        {
            var projectDir = CreateTempDir();
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Diagnostics.DiagnosticLogger
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                Message = "Logging from non-CWD project"
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(engine.Messages, m => m.Message!.Contains("Logging from non-CWD project"));
        }

        [Fact]
        public void ShouldAcceptTrackingTaskEnvironment()
        {
            var projectDir = CreateTempDir();
            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(projectDir);
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Diagnostics.DiagnosticLogger
            {
                BuildEngine = engine,
                TaskEnvironment = trackingEnv,
                Message = "Tracked message"
            };

            bool result = task.Execute();

            // Task should execute correctly with TrackingTaskEnvironment
            Assert.True(result);
            Assert.Contains(engine.Messages, m => m.Message!.Contains("Tracked message"));
        }

        [Fact]
        public void ShouldLogEmptyMessageWhenMessageIsNull()
        {
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Diagnostics.DiagnosticLogger
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                Message = null
            };

            bool result = task.Execute();

            Assert.True(result);
        }
    }
}
