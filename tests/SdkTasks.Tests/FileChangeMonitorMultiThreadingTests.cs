using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Analysis;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests
{
    public class FileChangeMonitorMultiThreadingTests : IDisposable
    {
        private readonly TaskTestContext _ctx;

        public FileChangeMonitorMultiThreadingTests() => _ctx = new TaskTestContext();

        public void Dispose() => _ctx.Dispose();

        [Fact]
        public void ChangedFiles_ShouldContainAbsolutePathsUnderProjectDirectory()
        {
            var watchSubDir = Path.Combine(_ctx.ProjectDir, "watch");
            Directory.CreateDirectory(watchSubDir);
            File.WriteAllText(Path.Combine(watchSubDir, "test.txt"), "content");

            var task = new FileChangeMonitor
            {
                BuildEngine = _ctx.Engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_ctx.ProjectDir),
                WatchDirectory = "watch",
                FilePatterns = new[] { "*.txt" }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.NotEmpty(task.ChangedFiles);
            foreach (var item in task.ChangedFiles)
            {
                SharedTestHelpers.AssertPathUnderProjectDir(_ctx.ProjectDir, item.ItemSpec);
            }
        }

        [Fact]
        public void Execute_WithRelativeWatchDir_FindsFilesUnderProjectDirNotCwd()
        {
            var watchDir = Path.Combine(_ctx.ProjectDir, "monitored");
            Directory.CreateDirectory(watchDir);
            File.WriteAllText(Path.Combine(watchDir, "file1.cs"), "code");
            File.WriteAllText(Path.Combine(watchDir, "file2.cs"), "code");

            var task = new FileChangeMonitor
            {
                BuildEngine = _ctx.Engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_ctx.ProjectDir),
                WatchDirectory = "monitored",
                FilePatterns = new[] { "*.cs" }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(2, task.ChangedFiles.Length);
        }

        [Fact]
        public void ChangedFiles_ShouldHaveChangeTypeAndDetectedAtMetadata()
        {
            var watchDir = Path.Combine(_ctx.ProjectDir, "src");
            Directory.CreateDirectory(watchDir);
            File.WriteAllText(Path.Combine(watchDir, "app.cs"), "code");

            var task = new FileChangeMonitor
            {
                BuildEngine = _ctx.Engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_ctx.ProjectDir),
                WatchDirectory = "src",
                FilePatterns = new[] { "*.cs" }
            };

            task.Execute();

            Assert.Single(task.ChangedFiles);
            var item = task.ChangedFiles[0];
            Assert.Equal("Modified", item.GetMetadata("ChangeType"));
            Assert.False(string.IsNullOrEmpty(item.GetMetadata("DetectedAt")),
                "ChangedFiles items should have DetectedAt metadata");
        }

        [Fact]
        public void LogMessages_ShouldReferenceProjectDirPaths()
        {
            var watchDir = Path.Combine(_ctx.ProjectDir, "logs");
            Directory.CreateDirectory(watchDir);
            File.WriteAllText(Path.Combine(watchDir, "app.log"), "data");

            var task = new FileChangeMonitor
            {
                BuildEngine = _ctx.Engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_ctx.ProjectDir),
                WatchDirectory = "logs",
                FilePatterns = new[] { "*.log" }
            };

            task.Execute();

            var messages = _ctx.Engine.Messages.Select(m => m.Message).ToList();
            Assert.Contains(messages, m => m!.Contains(_ctx.ProjectDir));
        }

        [Fact]
        public void OutputProperties_ShouldResolveUnderProjectDirectory()
        {
            var watchDir = Path.Combine(_ctx.ProjectDir, "data");
            Directory.CreateDirectory(watchDir);
            File.WriteAllText(Path.Combine(watchDir, "file.dat"), "x");

            var task = new FileChangeMonitor
            {
                BuildEngine = _ctx.Engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_ctx.ProjectDir),
                WatchDirectory = "data",
                FilePatterns = new[] { "*.*" }
            };

            task.Execute();

            // Validate all [Output] ITaskItem[] properties have paths under ProjectDirectory
            var outputProps = typeof(FileChangeMonitor)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<OutputAttribute>() != null
                         && p.PropertyType == typeof(ITaskItem[]));

            foreach (var prop in outputProps)
            {
                var items = (ITaskItem[]?)prop.GetValue(task);
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        Assert.True(
                            item.ItemSpec.StartsWith(_ctx.ProjectDir, StringComparison.OrdinalIgnoreCase),
                            $"[Output] property '{prop.Name}' item '{item.ItemSpec}' should be under ProjectDirectory '{_ctx.ProjectDir}'");
                    }
                }
            }

            // Also validate string [Output] properties
            SharedTestHelpers.AssertAllStringOutputsUnderProjectDir(task, _ctx.ProjectDir);
        }

        [Fact]
        public void Execute_DifferentProjectDirs_ProduceDifferentResolvedPaths()
        {
            var dir2 = _ctx.CreateAdditionalProjectDir();

            var watchDir1 = Path.Combine(_ctx.ProjectDir, "shared");
            Directory.CreateDirectory(watchDir1);
            File.WriteAllText(Path.Combine(watchDir1, "file.txt"), "a");

            var watchDir2 = Path.Combine(dir2, "shared");
            Directory.CreateDirectory(watchDir2);
            File.WriteAllText(Path.Combine(watchDir2, "file.txt"), "b");

            var task1 = new FileChangeMonitor
            {
                BuildEngine = _ctx.Engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_ctx.ProjectDir),
                WatchDirectory = "shared",
                FilePatterns = new[] { "*.txt" }
            };
            task1.Execute();

            var task2 = new FileChangeMonitor
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir2),
                WatchDirectory = "shared",
                FilePatterns = new[] { "*.txt" }
            };
            task2.Execute();

            Assert.NotEqual(task1.ChangedFiles[0].ItemSpec, task2.ChangedFiles[0].ItemSpec);
            SharedTestHelpers.AssertPathUnderProjectDir(_ctx.ProjectDir, task1.ChangedFiles[0].ItemSpec);
            SharedTestHelpers.AssertPathUnderProjectDir(dir2, task2.ChangedFiles[0].ItemSpec);
        }

        [Fact]
        public void Execute_UsesGetAbsolutePathForWatchDirectoryAndFiles()
        {
            var watchDir = Path.Combine(_ctx.ProjectDir, "tracked");
            Directory.CreateDirectory(watchDir);
            File.WriteAllText(Path.Combine(watchDir, "a.cs"), "code");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_ctx.ProjectDir);
            var task = new FileChangeMonitor
            {
                BuildEngine = _ctx.Engine,
                TaskEnvironment = trackingEnv,
                WatchDirectory = "tracked",
                FilePatterns = new[] { "*.cs" }
            };

            bool result = task.Execute();

            Assert.True(result);
            SharedTestHelpers.AssertUsesGetAbsolutePath(trackingEnv);
            // At least 2: one for WatchDirectory, one for the file change event
            SharedTestHelpers.AssertMinimumGetAbsolutePathCalls(trackingEnv, 2);
            Assert.Contains("tracked", trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void ChangedFiles_PathsShouldNotResolveRelativeToCwd()
        {
            var watchDir = Path.Combine(_ctx.ProjectDir, "cwdtest");
            Directory.CreateDirectory(watchDir);
            File.WriteAllText(Path.Combine(watchDir, "test.dat"), "data");

            var task = new FileChangeMonitor
            {
                BuildEngine = _ctx.Engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_ctx.ProjectDir),
                WatchDirectory = "cwdtest",
                FilePatterns = new[] { "*.dat" }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.NotEmpty(task.ChangedFiles);
            foreach (var item in task.ChangedFiles)
            {
                SharedTestHelpers.AssertNotResolvedToCwd(item.ItemSpec, _ctx.ProjectDir);
            }
        }
    }
}
