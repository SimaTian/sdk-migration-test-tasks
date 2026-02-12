using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class IncrementalBuildTrackerTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public IncrementalBuildTrackerTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Build.IncrementalBuildTracker();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Build.IncrementalBuildTracker),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldHaveInstanceIsolation()
        {
            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);

            var task1 = new SdkTasks.Build.IncrementalBuildTracker
            {
                BuildEngine = _engine,
                InputFile = "fileA.txt",
                TaskEnvironment = taskEnv
            };

            var task2 = new SdkTasks.Build.IncrementalBuildTracker
            {
                BuildEngine = _engine,
                InputFile = "fileB.txt",
                TaskEnvironment = taskEnv
            };

            task1.Execute();
            task2.Execute();

            Assert.Equal(1, task1.ExecutionNumber);
            Assert.Equal(1, task2.ExecutionNumber);
        }
    }
}
