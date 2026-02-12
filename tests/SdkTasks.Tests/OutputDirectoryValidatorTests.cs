using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class OutputDirectoryValidatorTests : IDisposable
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
        public void Execute_EmptyDirectoryPath_ReturnsErrorAndFalse()
        {
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Build.OutputDirectoryValidator
            {
                BuildEngine = engine,
                DirectoryPath = string.Empty
            };

            bool result = task.Execute();

            Assert.False(result);
            Assert.Contains(engine.Errors, e => e.Message!.Contains("DirectoryPath is required"));
        }

        [Fact]
        public void Execute_ExistingDirectory_ShouldResolveRelativeToProjectDirectory()
        {
            var projectDir = CreateTempDir();
            var relativePath = "mysubdir";
            Directory.CreateDirectory(Path.Combine(projectDir, relativePath));
            File.WriteAllText(Path.Combine(projectDir, relativePath, "dummy.txt"), "x");

            var engine = new MockBuildEngine();
            var task = new SdkTasks.Build.OutputDirectoryValidator
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                DirectoryPath = relativePath
            };

            bool result = task.Execute();

            // Assert CORRECT behavior: task should find the existing directory and report file count
            Assert.True(result);
            Assert.Contains(engine.Messages, m => m.Message!.Contains("exists with") && m.Message!.Contains("file(s)"));
        }

        [Fact]
        public void Execute_ExistingDirectory_UsesGetAbsolutePathFromTaskEnvironment()
        {
            var projectDir = CreateTempDir();
            var relativePath = "tracksubdir";
            Directory.CreateDirectory(Path.Combine(projectDir, relativePath));

            var tracking = SharedTestHelpers.CreateTrackingEnvironment(projectDir);
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Build.OutputDirectoryValidator
            {
                BuildEngine = engine,
                TaskEnvironment = tracking,
                DirectoryPath = relativePath
            };

            task.Execute();

            // Verify task calls TaskEnvironment.GetAbsolutePath instead of forbidden APIs
            SharedTestHelpers.AssertUsesGetAbsolutePath(tracking);
            Assert.Contains(relativePath, tracking.GetAbsolutePathArgs);
        }

        [Fact]
        public void Execute_NonExistingDirectory_CreatesDirectoryUnderProjectDir()
        {
            var projectDir = CreateTempDir();
            var relativePath = "newoutput";

            var engine = new MockBuildEngine();
            var task = new SdkTasks.Build.OutputDirectoryValidator
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                DirectoryPath = relativePath
            };

            bool result = task.Execute();

            // Assert CORRECT behavior: directory created under projectDir, not CWD
            Assert.True(result);
            Assert.True(Directory.Exists(Path.Combine(projectDir, relativePath)),
                "Directory should be created under ProjectDirectory");
            Assert.Contains(engine.Messages, m => m.Message!.Contains("Creating directory"));
        }

        [Fact]
        public void Execute_NonExistingDirectory_DoesNotCreateInCwd()
        {
            var projectDir = CreateTempDir();
            var relativePath = "cwd-guard-" + Guid.NewGuid().ToString("N")[..8];

            var engine = new MockBuildEngine();
            var task = new SdkTasks.Build.OutputDirectoryValidator
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                DirectoryPath = relativePath
            };

            task.Execute();

            var cwdPath = Path.Combine(Directory.GetCurrentDirectory(), relativePath);
            Assert.False(Directory.Exists(cwdPath),
                "Task must not create directory in process CWD");

            // Clean up in case the task incorrectly wrote to CWD
            if (Directory.Exists(cwdPath))
                Directory.Delete(cwdPath, true);
        }

        [Fact]
        public void Execute_ExistingDirectoryWithMultipleFiles_ReportsCorrectFileCount()
        {
            var projectDir = CreateTempDir();
            var relativePath = "multifile";
            var fullDir = Path.Combine(projectDir, relativePath);
            Directory.CreateDirectory(fullDir);
            File.WriteAllText(Path.Combine(fullDir, "a.txt"), "1");
            File.WriteAllText(Path.Combine(fullDir, "b.txt"), "2");
            File.WriteAllText(Path.Combine(fullDir, "c.txt"), "3");

            var engine = new MockBuildEngine();
            var task = new SdkTasks.Build.OutputDirectoryValidator
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                DirectoryPath = relativePath
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(engine.Messages, m => m.Message!.Contains("3 file(s)"));
        }

        [Fact]
        public void Execute_AbsolutePath_StillCallsGetAbsolutePath()
        {
            var projectDir = CreateTempDir();
            var absDir = Path.Combine(projectDir, "abstest");
            Directory.CreateDirectory(absDir);

            var tracking = SharedTestHelpers.CreateTrackingEnvironment(projectDir);
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Build.OutputDirectoryValidator
            {
                BuildEngine = engine,
                TaskEnvironment = tracking,
                DirectoryPath = absDir
            };

            task.Execute();

            // Even with absolute paths, task should route through TaskEnvironment
            SharedTestHelpers.AssertUsesGetAbsolutePath(tracking);
        }
    }
}
