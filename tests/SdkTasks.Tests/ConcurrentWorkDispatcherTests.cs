using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class ConcurrentWorkDispatcherTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public ConcurrentWorkDispatcherTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldUseTaskEnvironmentForEnvVars()
        {
            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);

            string sdkDir = Path.Combine(_projectDir, "sdk", "tool1");
            Directory.CreateDirectory(sdkDir);

            taskEnv.SetEnvironmentVariable("DOTNET_ROOT", _projectDir);

            var workItem = new TaskItem("tool1");
            workItem.SetMetadata("Category", "ConfigPath");

            var task = new SdkTasks.Build.ConcurrentWorkDispatcher
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                WorkItems = new ITaskItem[] { workItem }
            };

            task.Execute();

            Assert.NotEmpty(task.CompletedItems);
            string resolvedPath = task.CompletedItems[0].GetMetadata("ResolvedPath");
            Assert.Contains(_projectDir, resolvedPath, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldUseTaskEnvironmentGetEnvironmentVariable()
        {
            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };

            string sdkDir = Path.Combine(_projectDir, "sdk", "tool1");
            Directory.CreateDirectory(sdkDir);
            trackingEnv.SetEnvironmentVariable("DOTNET_ROOT", _projectDir);

            var workItem = new TaskItem("tool1");
            workItem.SetMetadata("Category", "ConfigPath");

            var task = new SdkTasks.Build.ConcurrentWorkDispatcher
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                WorkItems = new ITaskItem[] { workItem }
            };

            task.Execute();

            SharedTestHelpers.AssertGetEnvironmentVariableCalled(trackingEnv);
        }

        [Fact]
        public void ShouldResolveWorkItemPathsRelativeToProjectDirectory()
        {
            var workItem = new TaskItem("subdir/output.txt");
            workItem.SetMetadata("Category", "General");

            var task = new SdkTasks.Build.ConcurrentWorkDispatcher
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                WorkItems = new ITaskItem[] { workItem }
            };

            task.Execute();

            Assert.NotEmpty(task.CompletedItems);
            string resolvedPath = task.CompletedItems[0].GetMetadata("ResolvedPath");
            Assert.Contains(_projectDir, resolvedPath, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldProcessMultipleWorkItemsSuccessfully()
        {
            var items = new ITaskItem[]
            {
                new TaskItem("item1"),
                new TaskItem("item2"),
                new TaskItem("item3")
            };

            var task = new SdkTasks.Build.ConcurrentWorkDispatcher
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                WorkItems = items
            };

            task.Execute();

            Assert.Equal(3, task.CompletedItems.Length);
            foreach (var completed in task.CompletedItems)
            {
                string resolvedPath = completed.GetMetadata("ResolvedPath");
                Assert.Contains(_projectDir, resolvedPath, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
