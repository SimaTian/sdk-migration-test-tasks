using System;
using System.IO;
using Microsoft.Build.Framework;
using SdkTasks.Diagnostics;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests.Diagnostics
{
    public class OutputRedirectorMultiThreadingTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly string _cwdBefore;

        public OutputRedirectorMultiThreadingTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _cwdBefore = Directory.GetCurrentDirectory();
        }

        [Fact]
        public void Execute_ResolvesLogFileRelativeToProjectDirectory_NotCwd()
        {
            var relativePath = "output.log";
            var expectedAbsPath = Path.Combine(_projectDir, relativePath);

            var engine = new MockBuildEngine();
            var task = new OutputRedirector
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                LogFilePath = relativePath
            };

            var result = task.Execute();

            Assert.True(result, "Task should succeed");
            Assert.True(File.Exists(expectedAbsPath),
                $"Log file should be created at ProjectDirectory ({expectedAbsPath}), not process CWD");
        }

        [Fact]
        public void Execute_LogFileNotCreatedUnderCwd()
        {
            var relativePath = "cwd-check.log";
            var cwdPath = Path.Combine(_cwdBefore, relativePath);

            var engine = new MockBuildEngine();
            var task = new OutputRedirector
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                LogFilePath = relativePath
            };

            task.Execute();

            if (_projectDir != _cwdBefore)
            {
                Assert.False(File.Exists(cwdPath),
                    $"Log file should NOT be created under CWD ({cwdPath}); should be under ProjectDirectory");
            }
        }

        [Fact]
        public void Execute_CallsGetAbsolutePathOnTaskEnvironment()
        {
            var tracking = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var engine = new MockBuildEngine();
            var task = new OutputRedirector
            {
                BuildEngine = engine,
                TaskEnvironment = tracking,
                LogFilePath = "tracked.log"
            };

            task.Execute();

            SharedTestHelpers.AssertUsesGetAbsolutePath(tracking);
            Assert.Contains("tracked.log", tracking.GetAbsolutePathArgs);
        }

        [Fact]
        public void Execute_WritesContentToLogFile()
        {
            var engine = new MockBuildEngine();
            var task = new OutputRedirector
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                LogFilePath = "content.log"
            };

            task.Execute();

            var filePath = Path.Combine(_projectDir, "content.log");
            Assert.True(File.Exists(filePath));
            var content = File.ReadAllText(filePath);
            Assert.Contains("Redirected output to log file.", content);
        }

        [Fact]
        public void Execute_LogsMessageViaBuildEngine()
        {
            var engine = new MockBuildEngine();
            var task = new OutputRedirector
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                LogFilePath = "engine.log"
            };

            task.Execute();

            Assert.Contains(engine.Messages, m =>
                m.Message!.Contains("Redirected output to log file."));
        }

        [Fact]
        public void Execute_AbsoluteLogFilePath_UsedDirectly()
        {
            var absPath = Path.Combine(_projectDir, "absolute.log");
            var engine = new MockBuildEngine();
            var task = new OutputRedirector
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                LogFilePath = absPath
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.True(File.Exists(absPath), "Absolute path should be used directly");
        }

        [Fact]
        public void Execute_DoesNotModifyCwd()
        {
            var engine = new MockBuildEngine();
            var task = new OutputRedirector
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                LogFilePath = "nocwd.log"
            };

            task.Execute();

            Assert.Equal(_cwdBefore, Directory.GetCurrentDirectory());
        }

        [Fact]
        public void Execute_DoesNotUseConsoleOut()
        {
            var engine = new MockBuildEngine();
            var task = new OutputRedirector
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                LogFilePath = "console-check.log"
            };

            var originalOut = Console.Out;

            task.Execute();

            Assert.Same(originalOut, Console.Out);
        }

        public void Dispose()
        {
            TestHelper.CleanupTempDirectory(_projectDir);
        }
    }
}
