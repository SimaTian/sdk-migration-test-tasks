using Xunit;
using System.Reflection;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class IncrementalChangeDetectorTests : IDisposable
    {
        private readonly TaskTestContext _ctx;

        public IncrementalChangeDetectorTests() => _ctx = new TaskTestContext();

        private string CreateProjectDir() => _ctx.CreateAdditionalProjectDir();

        public void Dispose() => _ctx.Dispose();

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
            task1.DisposeWatcher();
            Assert.True(task2.Execute());
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
        public void Execute_ShouldCallGetAbsolutePathForWatchDirectory()
        {
            var dir = CreateProjectDir();
            var watchDir = Path.Combine(dir, "tracked");
            Directory.CreateDirectory(watchDir);

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(dir);
            var task = new SdkTasks.Analysis.IncrementalChangeDetector
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = trackingEnv,
                WatchDirectory = "tracked",
                CollectionTimeoutMs = 100,
            };

            Assert.True(task.Execute());
            task.DisposeWatcher();

            SharedTestHelpers.AssertUsesGetAbsolutePath(trackingEnv);
            Assert.Contains("tracked", trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void Execute_WithRelativeWatchDir_ShouldResolveToProjectDirectory()
        {
            var dir = CreateProjectDir();
            var watchDir = Path.Combine(dir, "src");
            Directory.CreateDirectory(watchDir);

            var engine = new MockBuildEngine();
            var task = new SdkTasks.Analysis.IncrementalChangeDetector
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir),
                WatchDirectory = "src",
                CollectionTimeoutMs = 100,
            };

            Assert.True(task.Execute());
            task.DisposeWatcher();

            Assert.Contains(engine.Messages, m =>
                m.Message != null && m.Message.Contains(watchDir));
        }

        [Fact]
        public void Execute_ResolvedPathsShouldNotMatchCwd()
        {
            var dir = CreateProjectDir();
            var watchDir = Path.Combine(dir, "cwdcheck");
            Directory.CreateDirectory(watchDir);

            var engine = new MockBuildEngine();
            var task = new SdkTasks.Analysis.IncrementalChangeDetector
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir),
                WatchDirectory = "cwdcheck",
                CollectionTimeoutMs = 100,
            };

            Assert.True(task.Execute());
            task.DisposeWatcher();

            // Verify the watcher resolved to the project directory, not process CWD
            string cwd = Directory.GetCurrentDirectory();
            var watchMessage = engine.Messages.FirstOrDefault(m =>
                m.Message?.Contains("Started watching") == true);
            Assert.NotNull(watchMessage);
            Assert.Contains(dir, watchMessage.Message!, StringComparison.OrdinalIgnoreCase);
            if (!dir.StartsWith(cwd, StringComparison.OrdinalIgnoreCase))
            {
                Assert.DoesNotContain(cwd, watchMessage.Message!);
            }
        }

        [Fact]
        public void Execute_TwoTasks_ShouldEachResolveToOwnProjectDir()
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
            task1.DisposeWatcher();
            Assert.True(task2.Execute());
            task2.DisposeWatcher();

            // Verify each task watched its own project directory
            Assert.Contains(engine1.Messages, m => m.Message!.Contains(watchDir1));
            Assert.Contains(engine2.Messages, m => m.Message!.Contains(watchDir2));
        }

        [Fact]
        public void Execute_WithEmptyWatchDirectory_ShouldLogError()
        {
            var dir = CreateProjectDir();
            var engine = new MockBuildEngine();

            var task = new SdkTasks.Analysis.IncrementalChangeDetector
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir),
                WatchDirectory = "   ",
                CollectionTimeoutMs = 100,
            };

            bool result = task.Execute();

            Assert.False(result);
            Assert.Contains(engine.Errors, e =>
                e.Message != null && e.Message.Contains("WatchDirectory"));
        }

        [Fact]
        public void Execute_WithNonExistentDir_ShouldCreateAndWatch()
        {
            var dir = CreateProjectDir();
            var watchDir = Path.Combine(dir, "newdir");

            var engine = new MockBuildEngine();
            var task = new SdkTasks.Analysis.IncrementalChangeDetector
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir),
                WatchDirectory = "newdir",
                CollectionTimeoutMs = 100,
            };

            Assert.True(task.Execute());
            task.DisposeWatcher();

            Assert.True(Directory.Exists(watchDir),
                "Task should create the watch directory if it doesn't exist");
            Assert.Contains(engine.Warnings, w =>
                w.Message != null && w.Message.Contains("does not exist"));
        }

        [Fact]
        public void Execute_TrackingEnv_TwoTasks_ShouldBothCallGetAbsolutePath()
        {
            var dir1 = CreateProjectDir();
            var dir2 = CreateProjectDir();

            Directory.CreateDirectory(Path.Combine(dir1, "src"));
            Directory.CreateDirectory(Path.Combine(dir2, "src"));

            var trackingEnv1 = SharedTestHelpers.CreateTrackingEnvironment(dir1);
            var trackingEnv2 = SharedTestHelpers.CreateTrackingEnvironment(dir2);

            var task1 = new SdkTasks.Analysis.IncrementalChangeDetector
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = trackingEnv1,
                WatchDirectory = "src",
                CollectionTimeoutMs = 100,
            };

            var task2 = new SdkTasks.Analysis.IncrementalChangeDetector
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = trackingEnv2,
                WatchDirectory = "src",
                CollectionTimeoutMs = 100,
            };

            Assert.True(task1.Execute());
            task1.DisposeWatcher();
            Assert.True(task2.Execute());
            task2.DisposeWatcher();

            SharedTestHelpers.AssertUsesGetAbsolutePath(trackingEnv1);
            SharedTestHelpers.AssertUsesGetAbsolutePath(trackingEnv2);
            Assert.Contains("src", trackingEnv1.GetAbsolutePathArgs);
            Assert.Contains("src", trackingEnv2.GetAbsolutePathArgs);
        }
    }
}
