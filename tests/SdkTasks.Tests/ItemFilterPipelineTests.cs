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
        public void ShouldUseTaskEnvironmentGetAbsolutePath()
        {
            var item = new TaskItem("ext-ref.dll");
            item.SetMetadata("Category", "ExternalReference");

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Compilation.ItemFilterPipeline
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                InputItems = new ITaskItem[] { item }
            };

            task.Execute();

            Assert.NotEmpty(task.FilteredItems);
            SharedTestHelpers.AssertGetAbsolutePathCalled(trackingEnv, 2);
        }

        [Fact]
        public void ShouldResolvePathsRelativeToProjectDirectory()
        {
            var item = new TaskItem("ext-ref.dll");
            item.SetMetadata("Category", "ExternalReference");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Compilation.ItemFilterPipeline
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                InputItems = new ITaskItem[] { item }
            };

            task.Execute();

            Assert.NotEmpty(task.FilteredItems);
            var resolvedItem = task.FilteredItems[0];
            Assert.StartsWith(_projectDir, resolvedItem.ItemSpec, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldUseGetCanonicalFormInPipeline()
        {
            var item = new TaskItem("ext-ref.dll");
            item.SetMetadata("Category", "ExternalReference");

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Compilation.ItemFilterPipeline
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                InputItems = new ITaskItem[] { item }
            };

            task.Execute();

            SharedTestHelpers.AssertGetCanonicalFormCalled(trackingEnv);
        }

        [Fact]
        public void ShouldFilterExcludedItems()
        {
            var included = new TaskItem("keep.dll");
            included.SetMetadata("Category", "ExternalReference");

            var excluded = new TaskItem("skip.dll");
            excluded.SetMetadata("Category", "ExternalReference");
            excluded.SetMetadata("Exclude", "true");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Compilation.ItemFilterPipeline
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                InputItems = new ITaskItem[] { included, excluded }
            };

            task.Execute();

            Assert.Single(task.FilteredItems);
            Assert.Contains("keep.dll", task.FilteredItems[0].ItemSpec, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ResolvedPathsShouldNotContainProcessCwd()
        {
            var item = new TaskItem("ext-ref.dll");
            item.SetMetadata("Category", "ExternalReference");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Compilation.ItemFilterPipeline
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                InputItems = new ITaskItem[] { item }
            };

            task.Execute();

            string cwd = Directory.GetCurrentDirectory();
            Assert.NotEmpty(task.FilteredItems);
            foreach (var filteredItem in task.FilteredItems)
            {
                if (!_projectDir.StartsWith(cwd, StringComparison.OrdinalIgnoreCase))
                    Assert.DoesNotContain(cwd, filteredItem.ItemSpec, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
