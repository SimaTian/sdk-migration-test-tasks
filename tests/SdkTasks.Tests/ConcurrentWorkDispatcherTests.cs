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
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Build.ConcurrentWorkDispatcher();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Build.ConcurrentWorkDispatcher),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

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
    }
}
