using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;

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
        public void ShouldResolveChangedFilesToProjectDirectory()
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

            task.Execute();

            Assert.NotEmpty(task.ChangedFiles);
            string resolved = task.ChangedFiles[0].ItemSpec;
            Assert.StartsWith(_projectDir, resolved, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldUseTaskEnvironmentGetAbsolutePath()
        {
            string watchDir = Path.Combine(_projectDir, "watch");
            Directory.CreateDirectory(watchDir);
            File.WriteAllText(Path.Combine(watchDir, "file1.txt"), "test");

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Analysis.FileChangeMonitor
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                WatchDirectory = "watch",
                FilePatterns = new[] { "*.txt" }
            };

            task.Execute();

            SharedTestHelpers.AssertGetAbsolutePathCalled(trackingEnv);
            Assert.Contains(trackingEnv.GetAbsolutePathArgs, arg =>
                arg.Contains("watch") || arg.Contains("file1.txt"));
        }

        [Fact]
        public void ShouldDetectMultipleChangesInWatchDirectory()
        {
            string watchDir = Path.Combine(_projectDir, "watch");
            Directory.CreateDirectory(watchDir);
            File.WriteAllText(Path.Combine(watchDir, "a.txt"), "aaa");
            File.WriteAllText(Path.Combine(watchDir, "b.txt"), "bbb");

            var task = new SdkTasks.Analysis.FileChangeMonitor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                WatchDirectory = "watch",
                FilePatterns = new[] { "*.txt" }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(2, task.ChangedFiles.Length);
            foreach (var item in task.ChangedFiles)
            {
                Assert.StartsWith(_projectDir, item.ItemSpec, StringComparison.OrdinalIgnoreCase);
                Assert.False(string.IsNullOrEmpty(item.GetMetadata("ChangeType")));
            }
        }

        [Fact]
        public void ResolvedPathsShouldNotContainProcessCwd()
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

            task.Execute();

            string cwd = Directory.GetCurrentDirectory();
            Assert.NotEmpty(task.ChangedFiles);
            foreach (var item in task.ChangedFiles)
            {
                Assert.DoesNotContain(cwd, item.ItemSpec, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
