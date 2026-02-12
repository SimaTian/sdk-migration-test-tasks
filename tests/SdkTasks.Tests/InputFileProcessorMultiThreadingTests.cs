using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Compilation;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests
{
    public class InputFileProcessorMultiThreadingTests : IDisposable
    {
        private readonly TaskTestContext _ctx;

        public InputFileProcessorMultiThreadingTests() => _ctx = new TaskTestContext();

        public void Dispose() => _ctx.Dispose();

        [Fact]
        public void ProcessedFiles_ResolveRelativePathsAgainstProjectDirectory_NotCWD()
        {
            var task = new InputFileProcessor
            {
                BuildEngine = _ctx.Engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_ctx.ProjectDir),
                InputFiles = new ITaskItem[] { new TaskItem("subdir/file.cs") }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Single(task.ProcessedFiles);
            string outputPath = task.ProcessedFiles[0].ItemSpec;
            SharedTestHelpers.AssertPathUnderProjectDir(_ctx.ProjectDir, outputPath);
            SharedTestHelpers.AssertNotResolvedToCwd(outputPath, _ctx.ProjectDir);
        }

        [Fact]
        public void Execute_EmptyInputFiles_ReturnsEmptyProcessedFiles()
        {
            var task = new InputFileProcessor
            {
                BuildEngine = _ctx.Engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_ctx.ProjectDir),
                InputFiles = Array.Empty<ITaskItem>()
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Empty(task.ProcessedFiles);
        }

        [Fact]
        public void Execute_FileWithNoExtension_IsSkippedWithWarning()
        {
            var task = new InputFileProcessor
            {
                BuildEngine = _ctx.Engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_ctx.ProjectDir),
                InputFiles = new ITaskItem[]
                {
                    new TaskItem("noextfile"),
                    new TaskItem("hasext.txt")
                }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Single(task.ProcessedFiles);
            Assert.Contains(_ctx.Engine.Warnings, w => w.Message!.Contains("noextfile"));
        }

        [Fact]
        public void ProcessedFiles_ContainOriginalPathAndFileExtensionMetadata()
        {
            var task = new InputFileProcessor
            {
                BuildEngine = _ctx.Engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_ctx.ProjectDir),
                InputFiles = new ITaskItem[] { new TaskItem("src/main.cs") }
            };

            task.Execute();

            Assert.Single(task.ProcessedFiles);
            var output = task.ProcessedFiles[0];
            Assert.Equal("src/main.cs", output.GetMetadata("OriginalPath"));
            Assert.Equal(".cs", output.GetMetadata("FileExtension"));
        }

        [Fact]
        public void Execute_AbsoluteInputPath_PassesThroughUnchanged()
        {
            string absoluteInput = Path.Combine(_ctx.ProjectDir, "already", "absolute.cs");
            var task = new InputFileProcessor
            {
                BuildEngine = _ctx.Engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_ctx.ProjectDir),
                InputFiles = new ITaskItem[] { new TaskItem(absoluteInput) }
            };

            task.Execute();

            Assert.Single(task.ProcessedFiles);
            Assert.Equal(absoluteInput, task.ProcessedFiles[0].ItemSpec);
        }

        [Fact]
        public void PublicApiSurface_IsPreserved()
        {
            var actual = typeof(InputFileProcessor)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name)
                .ToArray();

            Assert.Contains("TaskEnvironment", actual);
            Assert.Contains("InputFiles", actual);
            Assert.Contains("ProcessedFiles", actual);
        }

        [Fact]
        public void Execute_RelativePath_UsesGetAbsolutePath()
        {
            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_ctx.ProjectDir);
            var task = new InputFileProcessor
            {
                BuildEngine = _ctx.Engine,
                TaskEnvironment = trackingEnv,
                InputFiles = new ITaskItem[] { new TaskItem("relative.cs") }
            };

            bool result = task.Execute();

            Assert.True(result);
            SharedTestHelpers.AssertUsesGetAbsolutePath(trackingEnv);
            Assert.Contains("relative.cs", trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void Execute_AbsolutePath_DoesNotCallGetAbsolutePath()
        {
            string absolutePath = Path.Combine(_ctx.ProjectDir, "abs.cs");
            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_ctx.ProjectDir);
            var task = new InputFileProcessor
            {
                BuildEngine = _ctx.Engine,
                TaskEnvironment = trackingEnv,
                InputFiles = new ITaskItem[] { new TaskItem(absolutePath) }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(0, trackingEnv.GetAbsolutePathCallCount);
        }

        [Fact]
        public void Execute_MultipleRelativePaths_AllResolveUnderProjectDir()
        {
            var task = new InputFileProcessor
            {
                BuildEngine = _ctx.Engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_ctx.ProjectDir),
                InputFiles = new ITaskItem[]
                {
                    new TaskItem("a.cs"),
                    new TaskItem("sub/b.txt"),
                    new TaskItem("deep/path/c.xml")
                }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(3, task.ProcessedFiles.Length);
            foreach (var file in task.ProcessedFiles)
            {
                SharedTestHelpers.AssertPathUnderProjectDir(_ctx.ProjectDir, file.ItemSpec);
            }
        }

        [Fact]
        public void Execute_DifferentProjectDirs_ProduceDifferentResolvedPaths()
        {
            var dir2 = _ctx.CreateAdditionalProjectDir();

            var task1 = new InputFileProcessor
            {
                BuildEngine = _ctx.Engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_ctx.ProjectDir),
                InputFiles = new ITaskItem[] { new TaskItem("file.cs") }
            };
            task1.Execute();

            var task2 = new InputFileProcessor
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir2),
                InputFiles = new ITaskItem[] { new TaskItem("file.cs") }
            };
            task2.Execute();

            Assert.NotEqual(task1.ProcessedFiles[0].ItemSpec, task2.ProcessedFiles[0].ItemSpec);
            SharedTestHelpers.AssertPathUnderProjectDir(_ctx.ProjectDir, task1.ProcessedFiles[0].ItemSpec);
            SharedTestHelpers.AssertPathUnderProjectDir(dir2, task2.ProcessedFiles[0].ItemSpec);
        }
    }
}
