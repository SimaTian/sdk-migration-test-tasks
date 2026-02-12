using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests
{
    public class InputFileProcessorTests : IDisposable
    {
        private readonly TaskTestContext _ctx;
        private string _tempDir => _ctx.ProjectDir;
        private MockBuildEngine _engine => _ctx.Engine;

        public InputFileProcessorTests() => _ctx = new TaskTestContext();

        public void Dispose() => _ctx.Dispose();

        [Fact]
        public void Execute_WithRelativePath_ShouldResolveToProjectDirectory()
        {
            File.WriteAllText(Path.Combine(_tempDir, "test.txt"), "// test");

            var task = new SdkTasks.Compilation.InputFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_tempDir),
                InputFiles = new ITaskItem[] { new TaskItem("test.txt") }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.NotEmpty(task.ProcessedFiles);
            string resolved = task.ProcessedFiles[0].ItemSpec;
            SharedTestHelpers.AssertPathUnderProjectDir(_tempDir, resolved);
        }

        [Fact]
        public void Execute_WithRelativePath_ShouldCallGetAbsolutePath()
        {
            File.WriteAllText(Path.Combine(_tempDir, "input.cs"), "// code");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_tempDir);
            var task = new SdkTasks.Compilation.InputFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                InputFiles = new ITaskItem[] { new TaskItem("input.cs") }
            };

            bool result = task.Execute();

            Assert.True(result);
            SharedTestHelpers.AssertUsesGetAbsolutePath(trackingEnv);
            Assert.Contains("input.cs", trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void Execute_WithAbsolutePath_ShouldNotCallGetAbsolutePath()
        {
            string absolutePath = Path.Combine(_tempDir, "absolute.cs");
            File.WriteAllText(absolutePath, "// abs");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_tempDir);
            var task = new SdkTasks.Compilation.InputFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                InputFiles = new ITaskItem[] { new TaskItem(absolutePath) }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(0, trackingEnv.GetAbsolutePathCallCount);
            Assert.Equal(absolutePath, task.ProcessedFiles[0].ItemSpec);
        }

        [Fact]
        public void Execute_WithMultipleFiles_ShouldResolveAllToProjectDirectory()
        {
            File.WriteAllText(Path.Combine(_tempDir, "a.cs"), "// a");
            File.WriteAllText(Path.Combine(_tempDir, "b.txt"), "// b");

            var task = new SdkTasks.Compilation.InputFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_tempDir),
                InputFiles = new ITaskItem[]
                {
                    new TaskItem("a.cs"),
                    new TaskItem("b.txt")
                }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(2, task.ProcessedFiles.Length);
            foreach (var file in task.ProcessedFiles)
            {
                Assert.StartsWith(_tempDir, file.ItemSpec, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void Execute_WithEmptyInputFiles_ShouldReturnTrueAndEmptyOutput()
        {
            var task = new SdkTasks.Compilation.InputFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_tempDir),
                InputFiles = Array.Empty<ITaskItem>()
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Empty(task.ProcessedFiles);
        }

        [Fact]
        public void Execute_WithNoExtension_ShouldSkipAndWarn()
        {
            var task = new SdkTasks.Compilation.InputFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_tempDir),
                InputFiles = new ITaskItem[] { new TaskItem("noextension") }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Empty(task.ProcessedFiles);
            Assert.Contains(_engine.Warnings, w => w.Message!.Contains("no extension"));
        }

        [Fact]
        public void Execute_ShouldSetOriginalPathAndExtensionMetadata()
        {
            File.WriteAllText(Path.Combine(_tempDir, "meta.cs"), "// m");

            var task = new SdkTasks.Compilation.InputFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_tempDir),
                InputFiles = new ITaskItem[] { new TaskItem("meta.cs") }
            };

            task.Execute();

            Assert.Single(task.ProcessedFiles);
            Assert.Equal("meta.cs", task.ProcessedFiles[0].GetMetadata("OriginalPath"));
            Assert.Equal(".cs", task.ProcessedFiles[0].GetMetadata("FileExtension"));
        }

        [Fact]
        public void Execute_ResolvedPathShouldNotMatchCwd()
        {
            File.WriteAllText(Path.Combine(_tempDir, "cwd-check.cs"), "// check");

            var task = new SdkTasks.Compilation.InputFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_tempDir),
                InputFiles = new ITaskItem[] { new TaskItem("cwd-check.cs") }
            };

            bool result = task.Execute();

            Assert.True(result);
            string resolved = task.ProcessedFiles[0].ItemSpec;
            string cwd = Directory.GetCurrentDirectory();
            SharedTestHelpers.AssertPathUnderProjectDir(_tempDir, resolved);
            if (!_tempDir.StartsWith(cwd, StringComparison.OrdinalIgnoreCase))
            {
                Assert.DoesNotContain(cwd, resolved);
            }
        }
    }
}
