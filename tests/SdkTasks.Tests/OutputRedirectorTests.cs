using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class OutputRedirectorTests : IDisposable
    {
        private readonly string _projectDir;

        public OutputRedirectorTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldNotChangeConsoleOut()
        {
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Diagnostics.OutputRedirector
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                LogFilePath = "somefile.log"
            };

            var originalOut = Console.Out;

            bool result = task.Execute();

            Assert.True(result);
            // Assert CORRECT behavior: Console.Out should be unchanged
            Assert.Same(originalOut, Console.Out);
            Assert.Contains(engine.Messages, m => m.Message!.Contains("Redirected output to log file."));
        }

        [Fact]
        public void ShouldUseTaskEnvironmentGetAbsolutePath()
        {
            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var engine = new MockBuildEngine();

            var task = new SdkTasks.Diagnostics.OutputRedirector
            {
                BuildEngine = engine,
                TaskEnvironment = trackingEnv,
                LogFilePath = "output.log"
            };

            task.Execute();

            SharedTestHelpers.AssertGetAbsolutePathCalled(trackingEnv);
            Assert.Contains("output.log", trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void ShouldWriteLogFileToProjectDirectory()
        {
            var engine = new MockBuildEngine();
            var logFileName = "redirect-test.log";

            var task = new SdkTasks.Diagnostics.OutputRedirector
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                LogFilePath = logFileName
            };

            bool result = task.Execute();

            // Assert CORRECT behavior: log file should be written to projectDir, not CWD
            Assert.True(result);
            var expectedPath = Path.Combine(_projectDir, logFileName);
            Assert.True(File.Exists(expectedPath),
                $"Log file should be written to project directory: {expectedPath}");
        }

        [Fact]
        public void ShouldResolveRelativeLogPathToProjectDirectory()
        {
            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var engine = new MockBuildEngine();
            var logFileName = "logs/build.log";
            Directory.CreateDirectory(Path.Combine(_projectDir, "logs"));

            var task = new SdkTasks.Diagnostics.OutputRedirector
            {
                BuildEngine = engine,
                TaskEnvironment = trackingEnv,
                LogFilePath = logFileName
            };

            task.Execute();

            // Assert CORRECT behavior: relative path resolved via TaskEnvironment
            Assert.True(trackingEnv.GetAbsolutePathCallCount > 0);
        }
    }
}
