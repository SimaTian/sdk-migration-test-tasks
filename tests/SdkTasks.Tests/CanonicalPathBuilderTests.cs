using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class CanonicalPathBuilderTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public CanonicalPathBuilderTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldUseTaskEnvironmentGetCanonicalForm()
        {
            var fileName = "double-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "data");

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Build.CanonicalPathBuilder
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = trackingEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            SharedTestHelpers.AssertGetCanonicalFormCalled(trackingEnv);
        }

        [Fact]
        public void ShouldResolveCanonicalPathRelativeToProjectDirectory()
        {
            var fileName = "canon-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "data");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.CanonicalPathBuilder
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = taskEnv
            };

            task.Execute();

            Assert.NotNull(task.CanonicalPath);
            Assert.StartsWith(_projectDir, task.CanonicalPath!);
        }

        [Fact]
        public void ShouldCanonicalizePathWithParentDirectoryReferences()
        {
            var relativePath = "subdir\\..\\canon-test.txt";
            Directory.CreateDirectory(Path.Combine(_projectDir, "subdir"));
            File.WriteAllText(Path.Combine(_projectDir, "canon-test.txt"), "content");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.CanonicalPathBuilder
            {
                BuildEngine = _engine,
                InputPath = relativePath,
                TaskEnvironment = taskEnv
            };

            task.Execute();

            Assert.NotNull(task.CanonicalPath);
            Assert.StartsWith(_projectDir, task.CanonicalPath!);
            // Canonical form should not contain ".."
            Assert.DoesNotContain("..", task.CanonicalPath!);
        }

        [Fact]
        public void ShouldNotUsePathGetFullPathDirectly()
        {
            var fileName = "no-getfullpath.txt";

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Build.CanonicalPathBuilder
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = trackingEnv
            };

            task.Execute();

            SharedTestHelpers.AssertGetCanonicalFormCalled(trackingEnv);
            Assert.Contains(fileName, trackingEnv.GetCanonicalFormArgs);
        }

        [Fact]
        public void ShouldNotResolveRelativeToCwd()
        {
            var fileName = "cwd-canon.txt";

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.CanonicalPathBuilder
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = taskEnv
            };

            task.Execute();

            Assert.NotNull(task.CanonicalPath);
            // The canonical path should be under projectDir, not CWD
            Assert.StartsWith(_projectDir, task.CanonicalPath!);
        }
    }
}
