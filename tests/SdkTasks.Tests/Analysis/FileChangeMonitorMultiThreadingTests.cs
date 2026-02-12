using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Analysis;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests.Analysis
{
    public class FileChangeMonitorMultiThreadingTests : IDisposable
    {
        private readonly string _projectDir;

        public FileChangeMonitorMultiThreadingTests()
        {
            _projectDir = Path.Combine(Path.GetTempPath(), "fcm-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_projectDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_projectDir, true); } catch { }
        }

        [Fact]
        public void ItImplementsIMultiThreadableTask()
        {
            var task = new FileChangeMonitor();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void ItHasMSBuildMultiThreadableTaskAttribute()
        {
            Assert.NotEmpty(typeof(FileChangeMonitor)
                .GetCustomAttributes(typeof(MSBuildMultiThreadableTaskAttribute), false));
        }

        [Fact]
        public void ItResolvesWatchDirectoryRelativeToProjectDirectory()
        {
            string watchSubDir = Path.Combine(_projectDir, "watched");
            Directory.CreateDirectory(watchSubDir);
            File.WriteAllText(Path.Combine(watchSubDir, "test.txt"), "content");

            var task = new FileChangeMonitor();
            task.BuildEngine = new MockBuildEngine();
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir);
            task.WatchDirectory = "watched";
            task.FilePatterns = new[] { "*.txt" };

            bool result = task.Execute();

            Assert.True(result);
            Assert.NotEmpty(task.ChangedFiles);
            foreach (var item in task.ChangedFiles)
            {
                Assert.True(Path.IsPathRooted(item.ItemSpec),
                    "all output paths should be absolute");
                Assert.StartsWith(_projectDir, item.ItemSpec);
            }
        }

        [Fact]
        public void OutputItemsContainChangeTypeMetadata()
        {
            string watchSubDir = Path.Combine(_projectDir, "watchmeta");
            Directory.CreateDirectory(watchSubDir);
            File.WriteAllText(Path.Combine(watchSubDir, "data.csv"), "a,b,c");

            var task = new FileChangeMonitor();
            task.BuildEngine = new MockBuildEngine();
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir);
            task.WatchDirectory = "watchmeta";
            task.FilePatterns = new[] { "*.csv" };

            task.Execute();

            Assert.Single(task.ChangedFiles);
            var item = task.ChangedFiles[0];
            Assert.NotNull(item.GetMetadata("ChangeType"));
            Assert.NotEmpty(item.GetMetadata("ChangeType"));
            Assert.NotNull(item.GetMetadata("DetectedAt"));
            Assert.NotEmpty(item.GetMetadata("DetectedAt"));
        }

        [Fact]
        public void ItPreservesAllPublicProperties()
        {
            var task = new FileChangeMonitor();
            Assert.Equal(string.Empty, task.WatchDirectory);
            Assert.Equal(new[] { "*.*" }, task.FilePatterns);
            Assert.Equal(5000, task.TimeoutMs);
            Assert.Empty(task.ChangedFiles);
            Assert.NotNull(task.TaskEnvironment);
        }

        [Fact]
        public void ItHandlesNonExistentWatchDirectory()
        {
            var task = new FileChangeMonitor();
            task.BuildEngine = new MockBuildEngine();
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir);
            task.WatchDirectory = "nonexistent-subdir";

            bool result = task.Execute();

            Assert.True(result, "non-existent directory should not cause failure");
            Assert.Empty(task.ChangedFiles);
        }
    }
}
