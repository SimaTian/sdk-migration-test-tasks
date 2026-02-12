using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class ConcurrentWorkDispatcherTests : IDisposable
    {
        private readonly TaskTestContext _ctx;
        private string _projectDir => _ctx.ProjectDir;
        private MockBuildEngine _engine => _ctx.Engine;

        public ConcurrentWorkDispatcherTests() => _ctx = new TaskTestContext();
        public void Dispose() => _ctx.Dispose();

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
        public void ShouldCallGetEnvironmentVariableOnTaskEnvironment()
        {
            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);

            string sdkDir = Path.Combine(_projectDir, "sdk", "envtool");
            Directory.CreateDirectory(sdkDir);

            trackingEnv.SetEnvironmentVariable("DOTNET_ROOT", _projectDir);

            var workItem = new TaskItem("envtool");
            workItem.SetMetadata("Category", "ConfigPath");

            var task = new SdkTasks.Build.ConcurrentWorkDispatcher
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                WorkItems = new ITaskItem[] { workItem }
            };

            task.Execute();

            SharedTestHelpers.AssertUsesGetEnvironmentVariable(trackingEnv);
            Assert.NotEmpty(task.CompletedItems);
            string resolvedPath = task.CompletedItems[0].GetMetadata("ResolvedPath");
            Assert.Contains(_projectDir, resolvedPath, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldResolveRelativeConfigPathAgainstProjectDir_NotCwd()
        {
            // Create a config file ONLY in _projectDir (not in process CWD)
            string configDir = Path.Combine(_projectDir, "configs");
            Directory.CreateDirectory(configDir);
            File.WriteAllText(Path.Combine(configDir, "app.json"), "{}");

            var workItem = new TaskItem("work1");
            workItem.SetMetadata("ConfigPath", Path.Combine("configs", "app.json"));

            var task = new SdkTasks.Build.ConcurrentWorkDispatcher
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                WorkItems = new ITaskItem[] { workItem }
            };

            bool result = task.Execute();

            Assert.True(result,
                $"Execute failed. Errors: {string.Join("; ", _engine.Errors.Select(e => e.Message))}");
            Assert.NotEmpty(task.CompletedItems);
            string resolvedPath = task.CompletedItems[0].GetMetadata("ResolvedPath");
            SharedTestHelpers.AssertNotResolvedToCwd(resolvedPath, _projectDir);
        }

        [Fact]
        public void ShouldResolveDefaultIdentityAgainstProjectDir()
        {
            // Work item with no ConfigPath and non-ConfigPath category falls back to
            // Path.Combine(projectDir, identity)
            File.WriteAllText(Path.Combine(_projectDir, "myfile.txt"), "data");

            var workItem = new TaskItem("myfile.txt");
            workItem.SetMetadata("Category", "General");

            var task = new SdkTasks.Build.ConcurrentWorkDispatcher
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                WorkItems = new ITaskItem[] { workItem }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.NotEmpty(task.CompletedItems);
            string resolvedPath = task.CompletedItems[0].GetMetadata("ResolvedPath");
            SharedTestHelpers.AssertPathUnderProjectDir(_projectDir, resolvedPath);
            Assert.Equal("True", task.CompletedItems[0].GetMetadata("Exists"));
        }

        [Fact]
        public void ShouldProcessMultipleWorkItemsAndResolveAllToProjectDir()
        {
            File.WriteAllText(Path.Combine(_projectDir, "item1.txt"), "one");
            File.WriteAllText(Path.Combine(_projectDir, "item2.txt"), "two");
            File.WriteAllText(Path.Combine(_projectDir, "item3.txt"), "three");

            var task = new SdkTasks.Build.ConcurrentWorkDispatcher
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                WorkItems = new ITaskItem[]
                {
                    new TaskItem("item1.txt"),
                    new TaskItem("item2.txt"),
                    new TaskItem("item3.txt")
                }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(3, task.CompletedItems.Length);
            foreach (var item in task.CompletedItems)
            {
                string resolvedPath = item.GetMetadata("ResolvedPath");
                SharedTestHelpers.AssertPathUnderProjectDir(_projectDir, resolvedPath);
            }
        }

        [Fact]
        public void ShouldReturnEmptyResultsForNoWorkItems()
        {
            var task = new SdkTasks.Build.ConcurrentWorkDispatcher
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                WorkItems = Array.Empty<ITaskItem>()
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Empty(task.CompletedItems);
        }

        [Fact]
        public void ShouldResolveSubdirectoryItemsToProjectDir()
        {
            string subDir = Path.Combine(_projectDir, "sub", "deep");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(subDir, "nested.dat"), "nested");

            var workItem = new TaskItem(Path.Combine("sub", "deep", "nested.dat"));

            var task = new SdkTasks.Build.ConcurrentWorkDispatcher
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                WorkItems = new ITaskItem[] { workItem }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.NotEmpty(task.CompletedItems);
            string resolvedPath = task.CompletedItems[0].GetMetadata("ResolvedPath");
            SharedTestHelpers.AssertPathUnderProjectDir(_projectDir, resolvedPath);
        }

        [Fact]
        public void ShouldUseTaskEnvironmentEnvVars_NotProcessEnvironment()
        {
            // Set DOTNET_ROOT ONLY in TaskEnvironment (not process environment)
            // to prove the task reads from TaskEnvironment, not Environment
            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);

            string sdkDir = Path.Combine(_projectDir, "sdk", "mytool");
            Directory.CreateDirectory(sdkDir);

            trackingEnv.SetEnvironmentVariable("DOTNET_ROOT", _projectDir);

            var workItem = new TaskItem("mytool");
            workItem.SetMetadata("Category", "ConfigPath");

            var task = new SdkTasks.Build.ConcurrentWorkDispatcher
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                WorkItems = new ITaskItem[] { workItem }
            };

            bool result = task.Execute();

            Assert.True(result,
                $"Execute failed. Errors: {string.Join("; ", _engine.Errors.Select(e => e.Message))}. " +
                $"Warnings: {string.Join("; ", _engine.Warnings.Select(e => e.Message))}");
            Assert.NotEmpty(task.CompletedItems);
            SharedTestHelpers.AssertUsesGetEnvironmentVariable(trackingEnv);
            string resolvedPath = task.CompletedItems[0].GetMetadata("ResolvedPath");
            Assert.Contains(_projectDir, resolvedPath, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldPreserveAbsoluteConfigPathAsIs()
        {
            // Absolute ConfigPath should be used directly, not combined with projectDir
            string absPath = Path.Combine(_projectDir, "absolute", "config.xml");
            Directory.CreateDirectory(Path.GetDirectoryName(absPath)!);
            File.WriteAllText(absPath, "<config/>");

            var workItem = new TaskItem("work-abs");
            workItem.SetMetadata("ConfigPath", absPath);

            var task = new SdkTasks.Build.ConcurrentWorkDispatcher
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                WorkItems = new ITaskItem[] { workItem }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.NotEmpty(task.CompletedItems);
            string resolvedPath = task.CompletedItems[0].GetMetadata("ResolvedPath");
            Assert.Equal(absPath, resolvedPath);
        }
    }
}
