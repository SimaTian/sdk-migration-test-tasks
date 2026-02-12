using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class DependencyPathResolverTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public DependencyPathResolverTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldResolveRelativeToProjectDirectory()
        {
            var fileName = "indirect-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "hello");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Analysis.DependencyPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(Path.Combine(_projectDir, fileName), task.ResolvedPath);
        }

        [Fact]
        public void ShouldCallTaskEnvironmentGetAbsolutePath()
        {
            var fileName = "tracking-dep.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "hello");

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Analysis.DependencyPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = trackingEnv
            };

            task.Execute();

            SharedTestHelpers.AssertGetAbsolutePathCalled(trackingEnv);
        }

        [Fact]
        public void ShouldNotResolveRelativeToCwd()
        {
            var fileName = "dep-not-in-cwd.txt";
            // File only exists in projectDir, not CWD
            File.WriteAllText(Path.Combine(_projectDir, fileName), "dependency data");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Analysis.DependencyPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = taskEnv
            };

            task.Execute();

            // ResolvedPath should point to projectDir, not CWD
            Assert.NotNull(task.ResolvedPath);
            Assert.StartsWith(_projectDir, task.ResolvedPath!, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldResolveSubdirectoryPathToProjectDirectory()
        {
            var subPath = Path.Combine("deps", "lib.dll");
            var fullPath = Path.Combine(_projectDir, subPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, "fake-dll");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Analysis.DependencyPathResolver
            {
                BuildEngine = _engine,
                InputPath = subPath,
                TaskEnvironment = taskEnv
            };

            task.Execute();

            Assert.Equal(fullPath, task.ResolvedPath);
        }

        [Fact]
        public void ShouldPassAbsolutePathThrough()
        {
            var absolutePath = Path.Combine(_projectDir, "absolute-dep.txt");
            File.WriteAllText(absolutePath, "data");

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Analysis.DependencyPathResolver
            {
                BuildEngine = _engine,
                InputPath = absolutePath,
                TaskEnvironment = trackingEnv
            };

            task.Execute();

            Assert.Equal(absolutePath, task.ResolvedPath);
        }
    }
}
