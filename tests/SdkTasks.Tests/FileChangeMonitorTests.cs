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
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Analysis.FileChangeMonitor();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Analysis.FileChangeMonitor),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldResolveToProjectDirectory()
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
    }
}
