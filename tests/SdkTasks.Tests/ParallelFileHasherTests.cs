using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class ParallelFileHasherTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public ParallelFileHasherTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldResolveToProjectDirectory()
        {
            string testFile = Path.Combine(_projectDir, "data.txt");
            File.WriteAllText(testFile, "hello world");

            var task = new SdkTasks.Build.ParallelFileHasher
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                SourceFiles = new ITaskItem[] { new TaskItem("data.txt") },
                ParallelProcessing = false
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.NotEmpty(task.ProcessedFiles);
            string fullPath = task.ProcessedFiles[0].GetMetadata("ResolvedFullPath");
            Assert.StartsWith(_projectDir, fullPath, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldResolveToProjectDirectoryInParallelMode()
        {
            File.WriteAllText(Path.Combine(_projectDir, "parallel1.txt"), "content1");
            File.WriteAllText(Path.Combine(_projectDir, "parallel2.txt"), "content2");

            var task = new SdkTasks.Build.ParallelFileHasher
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                SourceFiles = new ITaskItem[]
                {
                    new TaskItem("parallel1.txt"),
                    new TaskItem("parallel2.txt")
                },
                ParallelProcessing = true
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(2, task.ProcessedFiles.Length);
            foreach (var file in task.ProcessedFiles)
            {
                string fullPath = file.GetMetadata("ResolvedFullPath");
                Assert.StartsWith(_projectDir, fullPath, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void ShouldNotResolveRelativeToCwd()
        {
            // File only exists in projectDir, not CWD
            File.WriteAllText(Path.Combine(_projectDir, "not-cwd.txt"), "data");

            var task = new SdkTasks.Build.ParallelFileHasher
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                SourceFiles = new ITaskItem[] { new TaskItem("not-cwd.txt") },
                ParallelProcessing = false
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.NotEmpty(task.ProcessedFiles);
            string fullPath = task.ProcessedFiles[0].GetMetadata("ResolvedFullPath");
            Assert.StartsWith(_projectDir, fullPath, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldComputeFileHash()
        {
            File.WriteAllText(Path.Combine(_projectDir, "hash-test.txt"), "hash me");

            var task = new SdkTasks.Build.ParallelFileHasher
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                SourceFiles = new ITaskItem[] { new TaskItem("hash-test.txt") },
                ParallelProcessing = false
            };

            task.Execute();

            Assert.NotEmpty(task.ProcessedFiles);
            string hash = task.ProcessedFiles[0].GetMetadata("FileHash");
            Assert.NotEmpty(hash);
            // SHA256 hash is 64 hex characters
            Assert.Equal(64, hash.Length);
        }

        [Fact]
        public void ShouldSetFileSizeMetadata()
        {
            string content = "file size test content";
            File.WriteAllText(Path.Combine(_projectDir, "size-test.txt"), content);

            var task = new SdkTasks.Build.ParallelFileHasher
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                SourceFiles = new ITaskItem[] { new TaskItem("size-test.txt") },
                ParallelProcessing = false
            };

            task.Execute();

            Assert.NotEmpty(task.ProcessedFiles);
            string fileSize = task.ProcessedFiles[0].GetMetadata("FileSize");
            Assert.NotEmpty(fileSize);
            Assert.True(int.Parse(fileSize) > 0);
        }

        [Fact]
        public void ShouldHandleEmptySourceFiles()
        {
            var task = new SdkTasks.Build.ParallelFileHasher
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                SourceFiles = Array.Empty<ITaskItem>(),
                ParallelProcessing = false
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Empty(task.ProcessedFiles);
        }

        [Fact]
        public void ShouldUseProjectDirectoryNotEnvironmentCurrentDirectory()
        {
            var cwd = Environment.CurrentDirectory;
            File.WriteAllText(Path.Combine(_projectDir, "async-delegate.txt"), "test");

            var task = new SdkTasks.Build.ParallelFileHasher
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                SourceFiles = new ITaskItem[] { new TaskItem("async-delegate.txt") },
                ParallelProcessing = false
            };

            task.Execute();

            Assert.NotEmpty(task.ProcessedFiles);
            string fullPath = task.ProcessedFiles[0].GetMetadata("ResolvedFullPath");
            Assert.NotEqual(cwd, _projectDir);
            Assert.StartsWith(_projectDir, fullPath, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldCallTaskEnvironmentGetAbsolutePath()
        {
            File.WriteAllText(Path.Combine(_projectDir, "tracked.txt"), "track me");

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };

            var task = new SdkTasks.Build.ParallelFileHasher
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                SourceFiles = new ITaskItem[] { new TaskItem("tracked.txt") },
                ParallelProcessing = false
            };

            bool result = task.Execute();

            Assert.True(result);
            SharedTestHelpers.AssertGetAbsolutePathCalled(trackingEnv);
            Assert.Contains("tracked.txt", trackingEnv.GetAbsolutePathArgs);
        }
    }
}
