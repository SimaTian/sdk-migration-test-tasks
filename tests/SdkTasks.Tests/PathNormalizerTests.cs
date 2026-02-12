using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class PathNormalizerTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public PathNormalizerTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldResolveRelativeToProjectDirectory()
        {
            var relativePath = "testfile.txt";
            File.WriteAllText(Path.Combine(_projectDir, relativePath), "hello");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.PathNormalizer
            {
                BuildEngine = _engine,
                InputPath = relativePath,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("File found at"));
        }

        [Fact]
        public void ShouldCallGetAbsolutePathOnTaskEnvironment()
        {
            var relativePath = "tracked-norm.txt";
            File.WriteAllText(Path.Combine(_projectDir, relativePath), "tracked");

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Build.PathNormalizer
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                InputPath = relativePath
            };

            task.Execute();

            SharedTestHelpers.AssertGetAbsolutePathCalled(trackingEnv);
            Assert.Contains(relativePath, trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void ResolvedPathShouldContainProjectDirectory()
        {
            var relativePath = "resolve-check.txt";
            File.WriteAllText(Path.Combine(_projectDir, relativePath), "content");

            var task = new SdkTasks.Build.PathNormalizer
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = relativePath
            };

            task.Execute();

            // The resolved path logged should contain the projectDir, not the CWD
            Assert.Contains(_engine.Messages, m => m.Message!.Contains(_projectDir));
        }

        [Fact]
        public void EmptyInputPath_ShouldLogError()
        {
            var task = new SdkTasks.Build.PathNormalizer
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
            };

            bool result = task.Execute();

            Assert.False(result);
            Assert.Contains(_engine.Errors, e => e.Message!.Contains("InputPath is required"));
        }

        [Fact]
        public void NonexistentFile_ShouldReportNotFound()
        {
            var task = new SdkTasks.Build.PathNormalizer
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = "nonexistent.txt"
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("File not found at"));
        }

        [Fact]
        public void ShouldNotResolveRelativeToCwd()
        {
            var relativePath = "cwd-test-" + Guid.NewGuid().ToString("N")[..8] + ".txt";

            // Create file only in projectDir, NOT in CWD
            File.WriteAllText(Path.Combine(_projectDir, relativePath), "project-only");

            var task = new SdkTasks.Build.PathNormalizer
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = relativePath
            };

            bool result = task.Execute();

            Assert.True(result);
            // The resolved path must point to the project directory, not CWD
            Assert.Contains(_engine.Messages, m =>
                m.Message!.Contains("Resolved path:") && m.Message!.Contains(_projectDir));
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("File found at"));
        }
    }
}
