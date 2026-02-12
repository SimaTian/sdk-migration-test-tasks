using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class InputFileProcessorTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public InputFileProcessorTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldResolveToProjectDirectory()
        {
            File.WriteAllText(Path.Combine(_projectDir, "test.txt"), "// test");

            var task = new SdkTasks.Compilation.InputFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputFiles = new ITaskItem[] { new TaskItem("test.txt") }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.NotEmpty(task.ProcessedFiles);
            string resolved = task.ProcessedFiles[0].ItemSpec;
            Assert.StartsWith(_projectDir, resolved, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldCallTaskEnvironmentGetAbsolutePathThroughCallChain()
        {
            File.WriteAllText(Path.Combine(_projectDir, "deep-chain.txt"), "data");

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Compilation.InputFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                InputFiles = new ITaskItem[] { new TaskItem("deep-chain.txt") }
            };

            task.Execute();

            SharedTestHelpers.AssertGetAbsolutePathCalled(trackingEnv);
        }

        [Fact]
        public void ShouldResolveMultipleFilesToProjectDirectory()
        {
            File.WriteAllText(Path.Combine(_projectDir, "file1.cs"), "// 1");
            File.WriteAllText(Path.Combine(_projectDir, "file2.cs"), "// 2");

            var task = new SdkTasks.Compilation.InputFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputFiles = new ITaskItem[]
                {
                    new TaskItem("file1.cs"),
                    new TaskItem("file2.cs")
                }
            };

            task.Execute();

            Assert.Equal(2, task.ProcessedFiles.Length);
            foreach (var file in task.ProcessedFiles)
            {
                Assert.StartsWith(_projectDir, file.ItemSpec, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void ShouldNotResolveRelativeToCwd()
        {
            File.WriteAllText(Path.Combine(_projectDir, "not-in-cwd.txt"), "data");

            var task = new SdkTasks.Compilation.InputFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputFiles = new ITaskItem[] { new TaskItem("not-in-cwd.txt") }
            };

            task.Execute();

            Assert.NotEmpty(task.ProcessedFiles);
            string resolved = task.ProcessedFiles[0].ItemSpec;
            // Resolved path must be under projectDir, not CWD
            Assert.StartsWith(_projectDir, resolved, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Environment.CurrentDirectory, resolved);
        }

        [Fact]
        public void ShouldHandleEmptyInputFiles()
        {
            var task = new SdkTasks.Compilation.InputFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputFiles = Array.Empty<ITaskItem>()
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Empty(task.ProcessedFiles);
        }

        [Fact]
        public void ShouldPreserveOriginalPathMetadata()
        {
            File.WriteAllText(Path.Combine(_projectDir, "meta.cs"), "// meta");

            var task = new SdkTasks.Compilation.InputFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputFiles = new ITaskItem[] { new TaskItem("meta.cs") }
            };

            task.Execute();

            Assert.NotEmpty(task.ProcessedFiles);
            string originalPath = task.ProcessedFiles[0].GetMetadata("OriginalPath");
            Assert.Equal("meta.cs", originalPath);
        }
    }
}
