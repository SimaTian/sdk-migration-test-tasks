using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class ItemFilterPipelineTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public ItemFilterPipelineTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Compilation.ItemFilterPipeline();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Compilation.ItemFilterPipeline),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldUseTaskEnvironmentGetAbsolutePath()
        {
            var item = new TaskItem("ext-ref.dll");
            item.SetMetadata("Category", "ExternalReference");

            var taskEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Compilation.ItemFilterPipeline
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                InputItems = new ITaskItem[] { item }
            };

            task.Execute();

            Assert.NotEmpty(task.FilteredItems);
            Assert.True(taskEnv.GetAbsolutePathCallCount >= 2,
                $"Task should call GetAbsolutePath for ExternalReference resolution (called {taskEnv.GetAbsolutePathCallCount} times, expected >= 2)");
        }
    }
}
