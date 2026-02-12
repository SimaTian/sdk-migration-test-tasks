using Xunit;
using System.Reflection;
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
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Analysis.IncrementalChangeDetector();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Analysis.IncrementalChangeDetector),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
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

            var started1 = engine1.Messages.Any(m =>
                m.Message?.Contains("Started watching") == true &&
                m.Message?.Contains(watchDir1) == true);
            var started2 = engine2.Messages.Any(m =>
                m.Message?.Contains("Started watching") == true &&
                m.Message?.Contains(watchDir2) == true);

            Assert.True(started1, "Task1 should start watching its own directory");
            Assert.True(started2, "Task2 should start watching its own directory");
        }
    }
}
