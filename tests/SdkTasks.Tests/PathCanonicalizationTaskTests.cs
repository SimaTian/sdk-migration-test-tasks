using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class PathCanonicalizationTaskTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public PathCanonicalizationTaskTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldUseGetCanonicalForm()
        {
            var relativePath = "subdir/../canon-test.txt";
            Directory.CreateDirectory(Path.Combine(_projectDir, "subdir"));
            File.WriteAllText(Path.Combine(_projectDir, "canon-test.txt"), "content");

            var taskEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Build.PathCanonicalizationTask
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                InputPath = relativePath
            };

            bool result = task.Execute();

            Assert.True(result);
            SharedTestHelpers.AssertGetCanonicalFormCalled(taskEnv);
        }

        [Fact]
        public void CanonicalPathShouldResolveRelativeToProjectDirectory()
        {
            var relativePath = "subdir/../canon-test.txt";
            Directory.CreateDirectory(Path.Combine(_projectDir, "subdir"));
            File.WriteAllText(Path.Combine(_projectDir, "canon-test.txt"), "content");

            var task = new SdkTasks.Build.PathCanonicalizationTask
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = relativePath
            };

            bool result = task.Execute();

            Assert.True(result);
            // The canonical path should contain projectDir, not CWD
            Assert.Contains(_engine.Messages, m => m.Message!.Contains(_projectDir));
        }

        [Fact]
        public void ShouldPassInputPathToGetCanonicalForm()
        {
            var relativePath = "deep/nested/../target.txt";
            Directory.CreateDirectory(Path.Combine(_projectDir, "deep", "nested"));
            File.WriteAllText(Path.Combine(_projectDir, "deep", "target.txt"), "data");

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Build.PathCanonicalizationTask
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                InputPath = relativePath
            };

            task.Execute();

            Assert.True(trackingEnv.GetCanonicalFormCallCount > 0);
            Assert.Contains(relativePath, trackingEnv.GetCanonicalFormArgs);
        }
    }
}
