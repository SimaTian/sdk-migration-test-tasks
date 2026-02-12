using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class OutputRedirectorTests : IDisposable
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
        public void ShouldNotChangeConsoleOut()
        {
            var projectDir = CreateTempDir();
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Diagnostics.OutputRedirector
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                LogFilePath = "somefile.log"
            };

            var originalOut = Console.Out;

            bool result = task.Execute();

            Assert.True(result);
            // Console.Out must remain unchanged (no Console.SetOut calls)
            Assert.Same(originalOut, Console.Out);
            // Output should go to build engine
            Assert.Contains(engine.Messages, m => m.Message!.Contains("Redirected output to log file."));
        }

        [Fact]
        public void ShouldUseGetAbsolutePath_VerifiedByTrackingEnvironment()
        {
            var projectDir = CreateTempDir();
            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(projectDir);
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Diagnostics.OutputRedirector
            {
                BuildEngine = engine,
                TaskEnvironment = trackingEnv,
                LogFilePath = "redirect.log"
            };

            bool result = task.Execute();

            Assert.True(result);
            // Task must call TaskEnvironment.GetAbsolutePath to resolve the log file path
            SharedTestHelpers.AssertUsesGetAbsolutePath(trackingEnv);
            Assert.Contains("redirect.log", trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void ShouldResolveLogFileRelativeToProjectDirectory_NotCwd()
        {
            var projectDir = CreateTempDir();
            var relativePath = "output.log";
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Diagnostics.OutputRedirector
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                LogFilePath = relativePath
            };

            bool result = task.Execute();

            Assert.True(result);
            // The log file must be written to the ProjectDirectory, not process CWD
            var expectedPath = Path.Combine(projectDir, relativePath);
            Assert.True(File.Exists(expectedPath),
                $"Log file should be created at ProjectDirectory ({expectedPath}), not process CWD");
        }

        [Fact]
        public void ShouldLogMessageViaBuildEngine()
        {
            var projectDir = CreateTempDir();
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Diagnostics.OutputRedirector
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                LogFilePath = "engine-log.log"
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(engine.Messages, m => m.Message!.Contains("Redirected output to log file."));
        }
    }
}
