using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Build;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests
{
    public class ConcurrentWorkDispatcherMultiThreadingTests : IDisposable
    {
        private readonly TaskTestContext _ctx;

        public ConcurrentWorkDispatcherMultiThreadingTests() => _ctx = new TaskTestContext();

        public void Dispose() => _ctx.Dispose();

        [Fact]
        public void ProcessWorkItem_ResolvesRelativeConfigPath_ViaProjectDirectory()
        {
            var configRelative = Path.Combine("configs", "settings.json");
            var configAbsolute = Path.Combine(_ctx.ProjectDir, configRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(configAbsolute)!);
            File.WriteAllText(configAbsolute, "{}");

            var item = new TaskItem("work1");
            item.SetMetadata("ConfigPath", configRelative);
            item.SetMetadata("Category", "Build");

            var task = new ConcurrentWorkDispatcher
            {
                BuildEngine = _ctx.Engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_ctx.ProjectDir),
                WorkItems = new ITaskItem[] { item }
            };

            var result = task.Execute();

            Assert.True(result,
                $"Execute failed. Errors: {string.Join("; ", _ctx.Engine.Errors.Select(e => e.Message))}");
            Assert.Single(task.CompletedItems);
            var resolvedPath = task.CompletedItems[0].GetMetadata("ResolvedPath");
            SharedTestHelpers.AssertPathUnderProjectDir(_ctx.ProjectDir, resolvedPath);
            SharedTestHelpers.AssertNotResolvedToCwd(resolvedPath, _ctx.ProjectDir);
            Assert.Equal("True", task.CompletedItems[0].GetMetadata("Exists"));
        }

        [Fact]
        public void ProcessWorkItem_ConfigPathCategory_UsesDotnetRootFromTaskEnvironment()
        {
            var fakeDotnetRoot = _ctx.CreateAdditionalProjectDir();
            Directory.CreateDirectory(Path.Combine(fakeDotnetRoot, "sdk"));

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_ctx.ProjectDir);
            taskEnv.SetEnvironmentVariable("DOTNET_ROOT", fakeDotnetRoot);

            var item = new TaskItem("8.0.100");
            item.SetMetadata("Category", "ConfigPath");

            var task = new ConcurrentWorkDispatcher
            {
                BuildEngine = _ctx.Engine,
                TaskEnvironment = taskEnv,
                WorkItems = new ITaskItem[] { item }
            };

            var result = task.Execute();

            Assert.True(result,
                $"Execute failed. Errors: {string.Join("; ", _ctx.Engine.Errors.Select(e => e.Message))}");
            Assert.Single(task.CompletedItems);
            var resolvedPath = task.CompletedItems[0].GetMetadata("ResolvedPath");
            Assert.Equal(Path.Combine(fakeDotnetRoot, "sdk", "8.0.100"), resolvedPath);
        }

        [Fact]
        public void ProcessWorkItem_NoConfigPath_ResolvesIdentityViaProjectDirectory()
        {
            var relativePath = Path.Combine("output", "result.txt");
            var absolutePath = Path.Combine(_ctx.ProjectDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            File.WriteAllText(absolutePath, "data");

            var item = new TaskItem(relativePath);
            item.SetMetadata("Category", "Other");

            var task = new ConcurrentWorkDispatcher
            {
                BuildEngine = _ctx.Engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_ctx.ProjectDir),
                WorkItems = new ITaskItem[] { item }
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.Single(task.CompletedItems);
            var resolved = task.CompletedItems[0].GetMetadata("ResolvedPath");
            SharedTestHelpers.AssertPathUnderProjectDir(_ctx.ProjectDir, resolved);
            Assert.Equal("True", task.CompletedItems[0].GetMetadata("Exists"));
        }

        [Fact]
        public void Execute_EmptyWorkItems_ReturnsTrueWithNoOutput()
        {
            var task = new ConcurrentWorkDispatcher
            {
                BuildEngine = _ctx.Engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_ctx.ProjectDir),
                WorkItems = Array.Empty<ITaskItem>()
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.Empty(task.CompletedItems);
        }

        [Fact]
        public void Execute_MultipleWorkItems_AllResolveViaProjectDirectory()
        {
            for (int i = 0; i < 5; i++)
                File.WriteAllText(Path.Combine(_ctx.ProjectDir, $"item{i}.txt"), $"content{i}");

            var items = Enumerable.Range(0, 5)
                .Select(i =>
                {
                    var t = new TaskItem($"item{i}.txt");
                    t.SetMetadata("Category", "Build");
                    return (ITaskItem)t;
                })
                .ToArray();

            var task = new ConcurrentWorkDispatcher
            {
                BuildEngine = _ctx.Engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_ctx.ProjectDir),
                WorkItems = items
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.Equal(5, task.CompletedItems.Length);
            foreach (var completed in task.CompletedItems)
            {
                SharedTestHelpers.AssertPathUnderProjectDir(_ctx.ProjectDir, completed.GetMetadata("ResolvedPath"));
                Assert.Equal("True", completed.GetMetadata("Exists"));
            }
        }

        [Fact]
        public void Execute_ConfigPathCategory_UsesGetEnvironmentVariable()
        {
            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_ctx.ProjectDir);
            var sdkDir = Path.Combine(_ctx.ProjectDir, "sdk", "testtool");
            Directory.CreateDirectory(sdkDir);
            trackingEnv.SetEnvironmentVariable("DOTNET_ROOT", _ctx.ProjectDir);

            var item = new TaskItem("testtool");
            item.SetMetadata("Category", "ConfigPath");

            var task = new ConcurrentWorkDispatcher
            {
                BuildEngine = _ctx.Engine,
                TaskEnvironment = trackingEnv,
                WorkItems = new ITaskItem[] { item }
            };

            task.Execute();

            SharedTestHelpers.AssertUsesGetEnvironmentVariable(trackingEnv);
        }

        [Fact]
        public void Execute_RelativeConfigPath_UsesGetAbsolutePath()
        {
            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_ctx.ProjectDir);

            var item = new TaskItem("work1");
            item.SetMetadata("ConfigPath", "relative/config.json");
            item.SetMetadata("Category", "Build");

            var task = new ConcurrentWorkDispatcher
            {
                BuildEngine = _ctx.Engine,
                TaskEnvironment = trackingEnv,
                WorkItems = new ITaskItem[] { item }
            };

            task.Execute();

            SharedTestHelpers.AssertUsesGetAbsolutePath(trackingEnv);
            Assert.Contains("relative/config.json", trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void Execute_DifferentProjectDirs_ProduceDifferentResolvedPaths()
        {
            var dir2 = _ctx.CreateAdditionalProjectDir();

            var task1 = new ConcurrentWorkDispatcher
            {
                BuildEngine = _ctx.Engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_ctx.ProjectDir),
                WorkItems = new ITaskItem[] { new TaskItem("file.txt") }
            };
            task1.Execute();

            var task2 = new ConcurrentWorkDispatcher
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir2),
                WorkItems = new ITaskItem[] { new TaskItem("file.txt") }
            };
            task2.Execute();

            var path1 = task1.CompletedItems[0].GetMetadata("ResolvedPath");
            var path2 = task2.CompletedItems[0].GetMetadata("ResolvedPath");
            Assert.NotEqual(path1, path2);
            SharedTestHelpers.AssertPathUnderProjectDir(_ctx.ProjectDir, path1);
            SharedTestHelpers.AssertPathUnderProjectDir(dir2, path2);
        }
    }
}
