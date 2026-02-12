using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;
using SdkTasks.Analysis;

namespace SdkTasks.Tests
{
    public class DependencyPathResolverTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public DependencyPathResolverTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldResolveRelativeToProjectDirectory()
        {
            var fileName = "indirect-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "hello");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new DependencyPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            // Assert CORRECT behavior: resolved path should be relative to projectDir
            Assert.True(result);
            Assert.Equal(Path.Combine(_projectDir, fileName), task.ResolvedPath);
        }

        [Fact]
        public void ShouldNotResolveToProcessCwd()
        {
            var fileName = "cwd-check.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "data");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new DependencyPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = taskEnv
            };

            task.Execute();

            // The resolved path must NOT be under the process CWD
            var cwdResolved = Path.Combine(Directory.GetCurrentDirectory(), fileName);
            Assert.NotEqual(cwdResolved, task.ResolvedPath);
            Assert.StartsWith(_projectDir, task.ResolvedPath!);
        }

        [Fact]
        public void ShouldCallTaskEnvironmentGetAbsolutePath()
        {
            var fileName = "tracking-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var tracking = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new DependencyPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = tracking
            };

            task.Execute();

            // Verify task uses TaskEnvironment.GetAbsolutePath instead of direct Path.GetFullPath
            SharedTestHelpers.AssertUsesGetAbsolutePath(tracking);
            Assert.Contains(fileName, tracking.GetAbsolutePathArgs);
        }

        [Fact]
        public void AbsolutePath_ShouldReturnUnchanged()
        {
            var absolutePath = Path.Combine(_projectDir, "absolute-test.txt");
            File.WriteAllText(absolutePath, "absolute");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new DependencyPathResolver
            {
                BuildEngine = _engine,
                InputPath = absolutePath,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(absolutePath, task.ResolvedPath);
        }

        [Fact]
        public void FileNotFound_ShouldLogWarningAndStillSucceed()
        {
            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new DependencyPathResolver
            {
                BuildEngine = _engine,
                InputPath = "nonexistent.txt",
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            // Task returns true even when file is not found, but logs a warning
            Assert.True(result);
            Assert.Contains(_engine.Warnings, w => w.Message!.Contains("File not found"));
        }

        [Fact]
        public void ExistingFile_ShouldLogResolvedMessageWithNoWarnings()
        {
            var fileName = "log-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "test");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new DependencyPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = taskEnv
            };

            task.Execute();

            Assert.Contains(_engine.Messages, m => m.Message!.Contains("Resolved"));
            Assert.Empty(_engine.Warnings);
        }
    }
}
