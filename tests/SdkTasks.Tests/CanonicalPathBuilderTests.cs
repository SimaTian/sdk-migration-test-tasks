using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class CanonicalPathBuilderTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public CanonicalPathBuilderTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Build.CanonicalPathBuilder();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Build.CanonicalPathBuilder),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldUseTaskEnvironmentGetCanonicalForm()
        {
            var fileName = "double-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "data");

            var taskEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Build.CanonicalPathBuilder
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.True(taskEnv.GetCanonicalFormCallCount > 0,
                "Task should use TaskEnvironment.GetCanonicalForm() instead of Path.GetFullPath()");
        }
    }
}
