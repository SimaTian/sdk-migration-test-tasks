using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class IncrementalChangeDetectorTests : IDisposable
    {
        private readonly List<string> _tempDirs = new();

        private string CreateProjectDir()
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
        public void ShouldWatchOwnProjectDirectory()
        {
            var dir1 = CreateProjectDir();
            var dir2 = CreateProjectDir();

            var watchDir1 = Path.Combine(dir1, "watch");
            var watchDir2 = Path.Combine(dir2, "watch");
            Directory.CreateDirectory(watchDir1);
            Directory.CreateDirectory(watchDir2);

            var engine1 = new MockBuildEngine();
            var engine2 = new MockBuildEngine();

            var task1 = new SdkTasks.Analysis.IncrementalChangeDetector
            {
                BuildEngine = engine1,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir1),
                WatchDirectory = "watch",
                CollectionTimeoutMs = 100,
            };

            var task2 = new SdkTasks.Analysis.IncrementalChangeDetector
            {
                BuildEngine = engine2,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir2),
                WatchDirectory = "watch",
                CollectionTimeoutMs = 100,
            };

            Assert.True(task1.Execute());
            Assert.True(task2.Execute());

            task1.DisposeWatcher();
            task2.DisposeWatcher();

            var started1 = engine1.Messages.Any(m =>
                m.Message?.Contains("Started watching") == true &&
                m.Message?.Contains(watchDir1) == true);
            var started2 = engine2.Messages.Any(m =>
                m.Message?.Contains("Started watching") == true &&
                m.Message?.Contains(watchDir2) == true);

            Assert.True(started1, "Task1 should start watching its own directory");
            Assert.True(started2, "Task2 should start watching its own directory");
        }

        [Fact]
        public void ShouldCallGetAbsolutePathOnTaskEnvironment()
        {
            var projectDir = CreateProjectDir();
            Directory.CreateDirectory(Path.Combine(projectDir, "tracked-watch"));

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = projectDir };

            var task = new SdkTasks.Analysis.IncrementalChangeDetector
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = trackingEnv,
                WatchDirectory = "tracked-watch",
                CollectionTimeoutMs = 100,
            };

            Assert.True(task.Execute());
            task.DisposeWatcher();

            SharedTestHelpers.AssertGetAbsolutePathCalled(trackingEnv);
            Assert.Contains("tracked-watch", trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void ShouldResolveWatchDirectoryRelativeToProjectDirectory()
        {
            var projectDir = CreateProjectDir();
            var cwd = Directory.GetCurrentDirectory();
            Directory.CreateDirectory(Path.Combine(projectDir, "monitor"));

            var engine = new MockBuildEngine();
            var task = new SdkTasks.Analysis.IncrementalChangeDetector
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                WatchDirectory = "monitor",
                CollectionTimeoutMs = 100,
            };

            Assert.True(task.Execute());
            task.DisposeWatcher();

            // The watched directory should be under ProjectDirectory, not CWD
            Assert.Contains(engine.Messages,
                m => m.Message != null &&
                     m.Message.Contains("Started watching") &&
                     m.Message.Contains(projectDir));
        }

        [Fact]
        public void ShouldNotWatchRelativeToProcessCwd()
        {
            var projectDir = CreateProjectDir();
            var cwd = Directory.GetCurrentDirectory();
            Directory.CreateDirectory(Path.Combine(projectDir, "cwd-test"));

            var engine = new MockBuildEngine();
            var task = new SdkTasks.Analysis.IncrementalChangeDetector
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                WatchDirectory = "cwd-test",
                CollectionTimeoutMs = 100,
            };

            Assert.True(task.Execute());
            task.DisposeWatcher();

            // Verify the watched path is the project-scoped directory
            var watchMsg = engine.Messages.FirstOrDefault(m =>
                m.Message?.Contains("Started watching") == true);
            Assert.NotNull(watchMsg);
            Assert.Contains(Path.Combine(projectDir, "cwd-test"), watchMsg!.Message!,
                StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldCreateWatchDirectoryWhenNotExists()
        {
            var projectDir = CreateProjectDir();
            // Do NOT create the "auto-create" directory

            var engine = new MockBuildEngine();
            var task = new SdkTasks.Analysis.IncrementalChangeDetector
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                WatchDirectory = "auto-create",
                CollectionTimeoutMs = 100,
            };

            Assert.True(task.Execute());
            task.DisposeWatcher();

            // Should auto-create the directory under ProjectDirectory
            Assert.True(Directory.Exists(Path.Combine(projectDir, "auto-create")));
        }
    }
}
