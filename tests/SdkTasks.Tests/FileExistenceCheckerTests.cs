using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Build;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class FileExistenceCheckerTests : IDisposable
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
        public void Execute_WithRelativePath_ShouldResolveRelativeToProjectDirectory()
        {
            var projectDir = CreateTempDir();
            var relativePath = "filecheck.txt";
            File.WriteAllText(Path.Combine(projectDir, relativePath), "test content");

            var task = new FileExistenceChecker
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                FilePath = relativePath
            };

            bool result = task.Execute();
            var engine = (MockBuildEngine)task.BuildEngine;

            Assert.True(result);
            Assert.Contains(engine.Messages, m => m.Message!.Contains("contains") && m.Message!.Contains("characters"));
        }

        [Fact]
        public void Execute_WithRelativePath_ShouldCallGetAbsolutePath()
        {
            var projectDir = CreateTempDir();
            var relativePath = "tracked.txt";
            File.WriteAllText(Path.Combine(projectDir, relativePath), "hello");

            var tracking = SharedTestHelpers.CreateTrackingEnvironment(projectDir);
            var task = new FileExistenceChecker
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = tracking,
                FilePath = relativePath
            };

            task.Execute();

            SharedTestHelpers.AssertUsesGetAbsolutePath(tracking);
            Assert.Contains(relativePath, tracking.GetAbsolutePathArgs);
        }

        [Fact]
        public void Execute_WithMissingFile_ShouldLogWarning()
        {
            var projectDir = CreateTempDir();

            var task = new FileExistenceChecker
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                FilePath = "nonexistent.txt"
            };

            bool result = task.Execute();
            var engine = (MockBuildEngine)task.BuildEngine;

            Assert.True(result);
            Assert.Contains(engine.Warnings, w => w.Message!.Contains("was not found"));
        }

        [Fact]
        public void Execute_WithEmptyFilePath_ShouldLogError()
        {
            var task = new FileExistenceChecker
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                FilePath = ""
            };

            bool result = task.Execute();
            var engine = (MockBuildEngine)task.BuildEngine;

            Assert.False(result);
            Assert.Contains(engine.Errors, e => e.Message!.Contains("FilePath is required"));
        }

        [Fact]
        public void Execute_ResolvesPathToProjectDir_NotProcessCwd()
        {
            var projectDir = CreateTempDir();
            var relativePath = "projfile.txt";
            File.WriteAllText(Path.Combine(projectDir, relativePath), "project content");

            var tracking = SharedTestHelpers.CreateTrackingEnvironment(projectDir);
            var task = new FileExistenceChecker
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = tracking,
                FilePath = relativePath
            };

            bool result = task.Execute();
            var engine = (MockBuildEngine)task.BuildEngine;

            Assert.True(result);
            Assert.Contains(engine.Messages, m => m.Message!.Contains("contains") && m.Message!.Contains("characters"));
            SharedTestHelpers.AssertUsesGetAbsolutePath(tracking);
        }
    }
}
