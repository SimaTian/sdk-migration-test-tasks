using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class SafePathResolverTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public SafePathResolverTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldResolveRelativeToProjectDirectory()
        {
            var fileName = "null-check-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "data");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.SafePathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            // Assert CORRECT behavior: task succeeds and resolves path to projectDir
            Assert.True(result);
            var expectedResolved = Path.Combine(_projectDir, fileName);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains(expectedResolved));
        }

        [Fact]
        public void ShouldUseTaskEnvironmentGetAbsolutePath()
        {
            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var fileName = "tracked-file.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "data");

            var task = new SdkTasks.Build.SafePathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = trackingEnv
            };

            task.Execute();

            SharedTestHelpers.AssertGetAbsolutePathCalled(trackingEnv);
            Assert.Contains(fileName, trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void ShouldNotResolveRelativeToCwd()
        {
            var engine = new MockBuildEngine();
            var fileName = "cwd-check.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "data");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.SafePathResolver
            {
                BuildEngine = engine,
                InputPath = fileName,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            // Assert CORRECT behavior: resolved path should contain projectDir, not CWD
            Assert.True(result);
            var expectedResolved = Path.Combine(_projectDir, fileName);
            Assert.Contains(engine.Messages, m => m.Message!.Contains(expectedResolved));
            // Verify the file does not exist when resolved relative to CWD
            Assert.DoesNotContain(engine.Messages, m => m.Message!.Contains("does not exist"));
        }

        [Fact]
        public void ShouldReportFileExistsWhenFoundInProjectDir()
        {
            var engine = new MockBuildEngine();
            var fileName = "found-file.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "file content");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.SafePathResolver
            {
                BuildEngine = engine,
                InputPath = fileName,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            // Assert CORRECT behavior: task reports file size
            Assert.True(result);
            Assert.Contains(engine.Messages, m => m.Message!.Contains("bytes"));
        }

        [Fact]
        public void ShouldReportFileDoesNotExistForMissingFile()
        {
            var engine = new MockBuildEngine();
            var fileName = "nonexistent-file.txt";

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.SafePathResolver
            {
                BuildEngine = engine,
                InputPath = fileName,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            // Assert CORRECT behavior: task reports file does not exist (still succeeds)
            Assert.True(result);
            Assert.Contains(engine.Messages, m => m.Message!.Contains("does not exist"));
        }
    }
}
