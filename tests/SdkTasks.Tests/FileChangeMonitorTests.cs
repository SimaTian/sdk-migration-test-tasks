using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests
{
    public class FileChangeMonitorTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public FileChangeMonitorTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void Execute_WithRelativeWatchDir_ShouldResolveToProjectDirectory()
        {
            string watchDir = Path.Combine(_projectDir, "watch");
            Directory.CreateDirectory(watchDir);
            File.WriteAllText(Path.Combine(watchDir, "file1.txt"), "test");

            var task = new SdkTasks.Analysis.FileChangeMonitor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                WatchDirectory = "watch",
                FilePatterns = new[] { "*.txt" }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.NotEmpty(task.ChangedFiles);
            string resolved = task.ChangedFiles[0].ItemSpec;
            SharedTestHelpers.AssertPathUnderProjectDir(_projectDir, resolved);
        }

        [Fact]
        public void Execute_ShouldCallGetAbsolutePathForWatchDirectory()
        {
            string watchDir = Path.Combine(_projectDir, "tracked");
            Directory.CreateDirectory(watchDir);
            File.WriteAllText(Path.Combine(watchDir, "a.log"), "data");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new SdkTasks.Analysis.FileChangeMonitor
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                WatchDirectory = "tracked",
                FilePatterns = new[] { "*.log" }
            };

            bool result = task.Execute();

            Assert.True(result);
            SharedTestHelpers.AssertUsesGetAbsolutePath(trackingEnv);
            Assert.Contains("tracked", trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void Execute_EventHandler_ShouldCallGetAbsolutePathForChangedFiles()
        {
            string watchDir = Path.Combine(_projectDir, "events");
            Directory.CreateDirectory(watchDir);
            File.WriteAllText(Path.Combine(watchDir, "changed.txt"), "content");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new SdkTasks.Analysis.FileChangeMonitor
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                WatchDirectory = "events",
                FilePatterns = new[] { "*.txt" }
            };

            bool result = task.Execute();

            Assert.True(result);
            // At least 2 calls: one for WatchDirectory, one for the changed file in the event handler
            Assert.True(trackingEnv.GetAbsolutePathCallCount >= 2,
                $"Expected at least 2 GetAbsolutePath calls (WatchDirectory + event handler), got {trackingEnv.GetAbsolutePathCallCount}");
        }

        [Fact]
        public void Execute_WithMultipleFiles_ShouldResolveAllToProjectDirectory()
        {
            string watchDir = Path.Combine(_projectDir, "multi");
            Directory.CreateDirectory(watchDir);
            File.WriteAllText(Path.Combine(watchDir, "file1.txt"), "one");
            File.WriteAllText(Path.Combine(watchDir, "file2.txt"), "two");
            File.WriteAllText(Path.Combine(watchDir, "file3.txt"), "three");

            var task = new SdkTasks.Analysis.FileChangeMonitor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                WatchDirectory = "multi",
                FilePatterns = new[] { "*.txt" }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(3, task.ChangedFiles.Length);
            foreach (var item in task.ChangedFiles)
            {
                Assert.StartsWith(_projectDir, item.ItemSpec, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void Execute_ResolvedPathsShouldNotMatchCwd()
        {
            string watchDir = Path.Combine(_projectDir, "cwdcheck");
            Directory.CreateDirectory(watchDir);
            File.WriteAllText(Path.Combine(watchDir, "test.dat"), "data");

            var task = new SdkTasks.Analysis.FileChangeMonitor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                WatchDirectory = "cwdcheck",
                FilePatterns = new[] { "*.dat" }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.NotEmpty(task.ChangedFiles);
            string resolved = task.ChangedFiles[0].ItemSpec;
            string cwd = Directory.GetCurrentDirectory();
            SharedTestHelpers.AssertPathUnderProjectDir(_projectDir, resolved);
            if (!_projectDir.StartsWith(cwd, StringComparison.OrdinalIgnoreCase))
            {
                Assert.DoesNotContain(cwd, resolved);
            }
        }

        [Fact]
        public void Execute_ShouldSetChangeTypeMetadata()
        {
            string watchDir = Path.Combine(_projectDir, "meta");
            Directory.CreateDirectory(watchDir);
            File.WriteAllText(Path.Combine(watchDir, "item.txt"), "metadata test");

            var task = new SdkTasks.Analysis.FileChangeMonitor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                WatchDirectory = "meta",
                FilePatterns = new[] { "*.txt" }
            };

            task.Execute();

            Assert.NotEmpty(task.ChangedFiles);
            string changeType = task.ChangedFiles[0].GetMetadata("ChangeType");
            Assert.False(string.IsNullOrEmpty(changeType), "ChangedFiles items should have ChangeType metadata");
        }

        [Fact]
        public void Execute_WithEmptyWatchDir_ShouldReturnNoChanges()
        {
            string watchDir = Path.Combine(_projectDir, "empty");
            Directory.CreateDirectory(watchDir);

            var task = new SdkTasks.Analysis.FileChangeMonitor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                WatchDirectory = "empty",
                FilePatterns = new[] { "*.txt" }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Empty(task.ChangedFiles);
        }

        [Fact]
        public void Execute_WithMultiplePatterns_ShouldDetectMatchingFiles()
        {
            string watchDir = Path.Combine(_projectDir, "patterns");
            Directory.CreateDirectory(watchDir);
            File.WriteAllText(Path.Combine(watchDir, "code.cs"), "// cs");
            File.WriteAllText(Path.Combine(watchDir, "data.xml"), "<root/>");
            File.WriteAllText(Path.Combine(watchDir, "notes.txt"), "text");

            var task = new SdkTasks.Analysis.FileChangeMonitor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                WatchDirectory = "patterns",
                FilePatterns = new[] { "*.cs", "*.xml" }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(2, task.ChangedFiles.Length);
            foreach (var item in task.ChangedFiles)
            {
                Assert.StartsWith(_projectDir, item.ItemSpec, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
