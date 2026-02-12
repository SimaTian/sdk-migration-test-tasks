using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class OutputDirectoryValidatorTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public OutputDirectoryValidatorTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldResolveRelativeToProjectDirectory()
        {
            var relativePath = "mysubdir";
            Directory.CreateDirectory(Path.Combine(_projectDir, relativePath));
            File.WriteAllText(Path.Combine(_projectDir, relativePath, "dummy.txt"), "x");

            var task = new SdkTasks.Build.OutputDirectoryValidator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                DirectoryPath = relativePath
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("exists with") && m.Message!.Contains("file(s)"));
        }

        [Fact]
        public void ShouldCallGetAbsolutePathOnTaskEnvironment()
        {
            var relativePath = "tracked-dir";
            Directory.CreateDirectory(Path.Combine(_projectDir, relativePath));

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Build.OutputDirectoryValidator
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                DirectoryPath = relativePath
            };

            task.Execute();

            SharedTestHelpers.AssertGetAbsolutePathCalled(trackingEnv);
            Assert.Contains(relativePath, trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void ShouldCreateDirectoryInProjectDir_NotCwd()
        {
            var relativePath = "newdir-" + Guid.NewGuid().ToString("N")[..8];

            var task = new SdkTasks.Build.OutputDirectoryValidator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                DirectoryPath = relativePath
            };

            task.Execute();

            var projectPath = Path.Combine(_projectDir, relativePath);
            var cwdPath = Path.Combine(Directory.GetCurrentDirectory(), relativePath);

            Assert.True(Directory.Exists(projectPath), "Directory should be created in projectDir");
            Assert.False(Directory.Exists(cwdPath),
                "Directory should NOT be created under process CWD");

            // Clean up if CWD dir was created by mistake
            if (Directory.Exists(cwdPath))
                Directory.Delete(cwdPath, true);
        }

        [Fact]
        public void ShouldLogCreatingDirectoryMessage()
        {
            var relativePath = "logdir-" + Guid.NewGuid().ToString("N")[..8];

            var task = new SdkTasks.Build.OutputDirectoryValidator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                DirectoryPath = relativePath
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("Creating directory"));
        }

        [Fact]
        public void ShouldReturnFalseForEmptyDirectoryPath()
        {
            var task = new SdkTasks.Build.OutputDirectoryValidator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                DirectoryPath = string.Empty
            };

            bool result = task.Execute();

            Assert.False(result);
            Assert.Contains(_engine.Errors, e => e.Message!.Contains("DirectoryPath is required"));
        }

        [Fact]
        public void ShouldResolveAbsolutePathRelativeToProjectDir_NotCwd()
        {
            var relativePath = "mysubdir";
            Directory.CreateDirectory(Path.Combine(_projectDir, relativePath));
            File.WriteAllText(Path.Combine(_projectDir, relativePath, "dummy.txt"), "x");

            var tracking = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Build.OutputDirectoryValidator
            {
                BuildEngine = _engine,
                TaskEnvironment = tracking,
                DirectoryPath = relativePath
            };

            bool result = task.Execute();

            Assert.True(result);
            // Verify GetAbsolutePath was called with the relative path
            Assert.Contains(relativePath, tracking.GetAbsolutePathArgs);
            // The task found the directory in projectDir (not CWD) and reported files
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("exists with") && m.Message!.Contains("file(s)"));
        }
    }
}
