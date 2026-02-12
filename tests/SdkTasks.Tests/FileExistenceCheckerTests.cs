using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Build;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class FileExistenceCheckerTests : IDisposable
    {
        private readonly TaskTestContext _ctx;

        public FileExistenceCheckerTests() => _ctx = new TaskTestContext();

        private string CreateTempDir() => _ctx.CreateAdditionalProjectDir();

        public void Dispose() => _ctx.Dispose();

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

            // File only exists under projectDir, NOT in process CWD
            var cwdPath = Path.Combine(Directory.GetCurrentDirectory(), relativePath);
            Assert.False(File.Exists(cwdPath), "Precondition: file must not exist in CWD");

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

        [Fact]
        public void Execute_ResolvedPathShouldBeUnderProjectDirectory()
        {
            var projectDir = CreateTempDir();
            var relativePath = "subdir/nested.txt";
            var nestedDir = Path.Combine(projectDir, "subdir");
            Directory.CreateDirectory(nestedDir);
            File.WriteAllText(Path.Combine(nestedDir, "nested.txt"), "nested content");

            var tracking = SharedTestHelpers.CreateTrackingEnvironment(projectDir);
            var task = new FileExistenceChecker
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = tracking,
                FilePath = relativePath
            };

            bool result = task.Execute();

            Assert.True(result);
            SharedTestHelpers.AssertUsesGetAbsolutePath(tracking);

            var resolvedPath = tracking.GetAbsolutePath(relativePath);
            SharedTestHelpers.AssertPathUnderProjectDir(projectDir, resolvedPath);
        }
    }
}
