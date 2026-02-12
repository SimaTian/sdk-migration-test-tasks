using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class PathCanonicalizationTaskTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public PathCanonicalizationTaskTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Build.PathCanonicalizationTask();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Build.PathCanonicalizationTask),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldUseGetCanonicalForm()
        {
            var relativePath = "subdir/../canon-test.txt";
            Directory.CreateDirectory(Path.Combine(_projectDir, "subdir"));
            File.WriteAllText(Path.Combine(_projectDir, "canon-test.txt"), "content");

            var taskEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Build.PathCanonicalizationTask
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                InputPath = relativePath
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.True(taskEnv.GetCanonicalFormCallCount > 0,
                "Task should use TaskEnvironment.GetCanonicalForm() instead of Path.GetFullPath()");
        }
    }
}
