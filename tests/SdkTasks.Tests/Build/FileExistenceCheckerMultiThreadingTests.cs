using System.Reflection;
using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Build;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests.Build
{
    public class FileExistenceCheckerMultiThreadingTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public FileExistenceCheckerMultiThreadingTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void FindsFileViaTaskEnvironmentNotCwd()
        {
            var relPath = Path.Combine("subdir", "testfile.txt");
            var absPath = Path.Combine(_projectDir, relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absPath)!);
            File.WriteAllText(absPath, "hello world");

            var task = new FileExistenceChecker
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                FilePath = relPath
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m =>
                m.Message!.Contains("11 characters"));
        }

        [Fact]
        public void LogsWarningWhenFileNotFound()
        {
            var task = new FileExistenceChecker
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                FilePath = "nonexistent.txt"
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Warnings, w =>
                w.Message!.Contains("nonexistent.txt") && w.Message!.Contains("not found"));
        }

        [Fact]
        public void LogsErrorWhenFilePathEmpty()
        {
            var task = new FileExistenceChecker
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                FilePath = ""
            };

            var result = task.Execute();

            Assert.False(result);
            Assert.Contains(_engine.Errors, e => e.Message!.Contains("FilePath is required"));
        }

        [Fact]
        public void LogMessageUsesOriginalRelativePath()
        {
            var relPath = "myfile.txt";
            File.WriteAllText(Path.Combine(_projectDir, relPath), "content");

            var task = new FileExistenceChecker
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                FilePath = relPath
            };

            task.Execute();

            Assert.Contains(_engine.Messages, m =>
                m.Message!.Contains($"File '{relPath}'"));
        }

        [Fact]
        public void CallsGetAbsolutePathForPathResolution()
        {
            var relPath = "tracked.txt";
            File.WriteAllText(Path.Combine(_projectDir, relPath), "data");

            var tracking = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new FileExistenceChecker
            {
                BuildEngine = _engine,
                TaskEnvironment = tracking,
                FilePath = relPath
            };

            task.Execute();

            SharedTestHelpers.AssertUsesGetAbsolutePath(tracking);
            Assert.Contains(relPath, tracking.GetAbsolutePathArgs);
        }

        [Fact]
        public void PreservesAllPublicProperties()
        {
            var expectedProperties = new[] { "TaskEnvironment", "FilePath" };

            var actualProperties = typeof(FileExistenceChecker)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name)
                .ToArray();

            foreach (var expected in expectedProperties)
            {
                Assert.Contains(expected, actualProperties);
            }
        }
    }
}
