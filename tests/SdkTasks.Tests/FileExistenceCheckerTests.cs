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
        public void Execute_WithExistingFile_ShouldResolveRelativeToProjectDirectory()
        {
            var projectDir = CreateTempDir();
            var relativePath = "filecheck.txt";
            File.WriteAllText(Path.Combine(projectDir, relativePath), "exists");

            var tracking = new TrackingTaskEnvironment { ProjectDirectory = projectDir };
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
            SharedTestHelpers.AssertGetAbsolutePathCalled(tracking);
            Assert.Contains(relativePath, tracking.GetAbsolutePathArgs);
        }

        [Fact]
        public void Execute_WithExistingFile_ShouldNotResolveRelativeToCwd()
        {
            var projectDir = CreateTempDir();
            var relativePath = "cwdcheck.txt";
            File.WriteAllText(Path.Combine(projectDir, relativePath), "content");

            // Do NOT create the file in CWD — only in projectDir
            var cwdPath = Path.Combine(Directory.GetCurrentDirectory(), relativePath);
            Assert.False(File.Exists(cwdPath), "Precondition: file must not exist in CWD");

            var tracking = new TrackingTaskEnvironment { ProjectDirectory = projectDir };
            var task = new FileExistenceChecker
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = tracking,
                FilePath = relativePath
            };

            bool result = task.Execute();
            var engine = (MockBuildEngine)task.BuildEngine;

            // Task should find file at projectDir, not CWD
            Assert.True(result);
            Assert.Contains(engine.Messages, m => m.Message!.Contains("contains") && m.Message!.Contains("characters"));
            Assert.Empty(engine.Warnings);
        }

        [Fact]
        public void Execute_WithMissingFile_ShouldLogWarning()
        {
            var projectDir = CreateTempDir();
            var relativePath = "nonexistent.txt";

            var tracking = new TrackingTaskEnvironment { ProjectDirectory = projectDir };
            var task = new FileExistenceChecker
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = tracking,
                FilePath = relativePath
            };

            bool result = task.Execute();
            var engine = (MockBuildEngine)task.BuildEngine;

            Assert.True(result);
            Assert.Contains(engine.Warnings, w => w.Message!.Contains("was not found"));
            SharedTestHelpers.AssertGetAbsolutePathCalled(tracking);
        }

        [Fact]
        public void Execute_WithEmptyFilePath_ShouldLogError()
        {
            var projectDir = CreateTempDir();

            var task = new FileExistenceChecker
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                FilePath = string.Empty
            };

            bool result = task.Execute();
            var engine = (MockBuildEngine)task.BuildEngine;

            Assert.False(result);
            Assert.Contains(engine.Errors, e => e.Message!.Contains("FilePath is required"));
        }

        [Fact]
        public void Execute_ShouldAutoInitializeProjectDirectoryFromBuildEngine()
        {
            var task = new FileExistenceChecker
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = new TaskEnvironment(),
                FilePath = "somefile.txt"
            };

            // TaskEnvironment.ProjectDirectory starts empty; Execute should auto-initialize from BuildEngine
            bool result = task.Execute();

            Assert.NotNull(task.TaskEnvironment.ProjectDirectory);
            Assert.NotEmpty(task.TaskEnvironment.ProjectDirectory);
        }
    }
}
