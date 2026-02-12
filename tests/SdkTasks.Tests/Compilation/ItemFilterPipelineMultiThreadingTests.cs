using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Compilation;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests.Compilation
{
    public class ItemFilterPipelineMultiThreadingTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public ItemFilterPipelineMultiThreadingTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ItImplementsIMultiThreadableTask()
        {
            var task = new ItemFilterPipeline();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void ItHasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(ItemFilterPipeline),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ItResolvesRelativePathsViaTaskEnvironment()
        {
            var item = new TaskItem("subdir/file1.cs");
            item.SetMetadata("Category", "Default");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new ItemFilterPipeline
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                FilterPattern = string.Empty,
                IncludeMetadata = false,
                InputItems = new ITaskItem[] { item }
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.Single(task.FilteredItems);

            string expectedAbsolute = Path.GetFullPath(Path.Combine(_projectDir, "subdir", "file1.cs"));
            string actualPath = Path.GetFullPath(task.FilteredItems[0].ItemSpec);
            Assert.Equal(expectedAbsolute, actualPath);
        }

        [Fact]
        public void ItResolvesOutputDirectoryViaTaskEnvironment()
        {
            var trackingEnv = SharedTestHelpers.CreateTrackingEnv(_projectDir);
            var item = new TaskItem("src/file.cs");
            item.SetMetadata("Category", "Default");

            var task = new ItemFilterPipeline
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                OutputDirectory = "bin/output",
                InputItems = new ITaskItem[] { item }
            };

            var result = task.Execute();

            Assert.True(result);
            // GetAbsolutePath should be called for OutputDirectory + for each item path
            SharedTestHelpers.AssertGetAbsolutePathCalled(trackingEnv, 2);
            Assert.Contains("bin/output", trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void ItNormalizesPathsViaTaskEnvironmentCanonicalForm()
        {
            var trackingEnv = SharedTestHelpers.CreateTrackingEnv(_projectDir);
            var item = new TaskItem("subdir/../subdir/file.cs");
            item.SetMetadata("Category", "Default");

            var task = new ItemFilterPipeline
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                FilterPattern = string.Empty,
                InputItems = new ITaskItem[] { item }
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.Single(task.FilteredItems);
            SharedTestHelpers.AssertGetCanonicalFormCalled(trackingEnv);
        }

        [Fact]
        public void ResolvedPathsShouldBeUnderProjectDirectory()
        {
            var item = new TaskItem("lib/component.dll");
            item.SetMetadata("Category", "ExternalReference");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new ItemFilterPipeline
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                InputItems = new ITaskItem[] { item }
            };

            var result = task.Execute();

            Assert.True(result);
            SharedTestHelpers.AssertAllItemsUnderProjectDir(_projectDir, task.FilteredItems);
        }

        [Fact]
        public void ResolvedPathsShouldNotFallUnderProcessCwd()
        {
            var item = new TaskItem("ref.dll");
            item.SetMetadata("Category", "ExternalReference");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new ItemFilterPipeline
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                InputItems = new ITaskItem[] { item }
            };

            task.Execute();

            Assert.NotEmpty(task.FilteredItems);
            foreach (var filtered in task.FilteredItems)
            {
                SharedTestHelpers.AssertPathNotUnderCwd(_projectDir, filtered.ItemSpec);
            }
        }
    }
}
