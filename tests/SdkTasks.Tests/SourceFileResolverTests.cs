using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Compilation;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class SourceFileResolverTests : IDisposable
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

        // =====================================================================
        // Interface / attribute verification
        // =====================================================================

        // =====================================================================
        // Path resolution: relative paths resolve via ProjectDirectory
        // =====================================================================

        [Fact]
        public void Execute_RelativePath_ResolvesRelativeToProjectDirectory()
        {
            var projectDir = CreateTempDir();
            var relativePath = "sourcefile.txt";
            File.WriteAllText(Path.Combine(projectDir, relativePath), "data");

            var task = new SourceFileResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                InputPath = relativePath
            };

            bool result = task.Execute();
            var engine = (MockBuildEngine)task.BuildEngine;

            Assert.True(result);
            Assert.Contains(engine.Messages, m => m.Message!.Contains("File size:"));
        }

        [Fact]
        public void Execute_RelativePath_UsesGetAbsolutePath()
        {
            var projectDir = CreateTempDir();
            var relativePath = "tracked.txt";
            File.WriteAllText(Path.Combine(projectDir, relativePath), "content");

            var tracking = SharedTestHelpers.CreateTrackingEnvironment(projectDir);
            var task = new SourceFileResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = tracking,
                InputPath = relativePath
            };

            bool result = task.Execute();

            Assert.True(result);
            SharedTestHelpers.AssertUsesGetAbsolutePath(tracking);
            Assert.Contains(relativePath, tracking.GetAbsolutePathArgs);
        }

        [Fact]
        public void Execute_RelativePath_ResolvedPathContainsProjectDirectory()
        {
            var projectDir = CreateTempDir();
            var relativePath = "notincwd.txt";
            File.WriteAllText(Path.Combine(projectDir, relativePath), "hello");

            var task = new SourceFileResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                InputPath = relativePath
            };

            bool result = task.Execute();
            var engine = (MockBuildEngine)task.BuildEngine;

            Assert.True(result);
            Assert.Contains(engine.Messages, m =>
                m.Message!.Contains("Resolved") && m.Message!.Contains(projectDir));
        }

        // =====================================================================
        // Absolute path handling
        // =====================================================================

        [Fact]
        public void Execute_AbsolutePath_StillCallsGetAbsolutePath()
        {
            var projectDir = CreateTempDir();
            var absolutePath = Path.Combine(projectDir, "absolute.txt");
            File.WriteAllText(absolutePath, "abs");

            var tracking = SharedTestHelpers.CreateTrackingEnvironment(projectDir);
            var task = new SourceFileResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = tracking,
                InputPath = absolutePath
            };

            bool result = task.Execute();
            var engine = (MockBuildEngine)task.BuildEngine;

            Assert.True(result);
            SharedTestHelpers.AssertUsesGetAbsolutePath(tracking);
            Assert.Contains(engine.Messages, m => m.Message!.Contains("File size:"));
        }

        // =====================================================================
        // Error / warning cases
        // =====================================================================

        [Fact]
        public void Execute_MissingFile_LogsWarning()
        {
            var projectDir = CreateTempDir();

            var task = new SourceFileResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                InputPath = "nonexistent.txt"
            };

            bool result = task.Execute();
            var engine = (MockBuildEngine)task.BuildEngine;

            Assert.True(result);
            Assert.Contains(engine.Warnings, w => w.Message!.Contains("does not exist"));
        }

        [Fact]
        public void Execute_EmptyInputPath_LogsError()
        {
            var task = new SourceFileResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(CreateTempDir()),
                InputPath = string.Empty
            };

            bool result = task.Execute();
            var engine = (MockBuildEngine)task.BuildEngine;

            Assert.False(result);
            Assert.Contains(engine.Errors, e => e.Message!.Contains("InputPath is required"));
        }

        // =====================================================================
        // ProjectDirectory auto-initialization from BuildEngine
        // =====================================================================

        [Fact]
        public void Execute_EmptyProjectDirectory_AutoInitializesFromBuildEngine()
        {
            var task = new SourceFileResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = new TaskEnvironment(),
                InputPath = "somefile.txt"
            };

            task.Execute();

            Assert.NotEqual(string.Empty, task.TaskEnvironment.ProjectDirectory);
        }
    }
}
