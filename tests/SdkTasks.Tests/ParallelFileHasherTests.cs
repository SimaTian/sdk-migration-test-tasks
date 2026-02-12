using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class ParallelFileHasherTests : IDisposable
    {
        private readonly TaskTestContext _ctx;
        private string _projectDir => _ctx.ProjectDir;
        private MockBuildEngine _engine => _ctx.Engine;

        public ParallelFileHasherTests() => _ctx = new TaskTestContext();
        public void Dispose() => _ctx.Dispose();

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

            Assert.True(result,
                $"Execute failed. Errors: {string.Join("; ", _engine.Errors.Select(e => e.Message))}. " +
                $"Warnings: {string.Join("; ", _engine.Warnings.Select(e => e.Message))}");
            Assert.NotEmpty(task.ProcessedFiles);
            string fullPath = task.ProcessedFiles[0].GetMetadata("ResolvedFullPath");
            SharedTestHelpers.AssertPathUnderProjectDir(_projectDir, fullPath);
        }

        [Fact]
        public void ShouldCallGetAbsolutePathOnTaskEnvironment()
        {
            File.WriteAllText(Path.Combine(_projectDir, "tracked.txt"), "content");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new SdkTasks.Build.ParallelFileHasher
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                SourceFiles = new ITaskItem[] { new TaskItem("tracked.txt") },
                ParallelProcessing = false
            };

            bool result = task.Execute();

            Assert.True(result);
            SharedTestHelpers.AssertUsesGetAbsolutePath(trackingEnv);
            Assert.Contains("tracked.txt", trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void ShouldResolveRelativePathsAgainstProjectDir_NotCwd()
        {
            // Create file ONLY in _projectDir (not in process CWD)
            File.WriteAllText(Path.Combine(_projectDir, "only-here.txt"), "data");

            var task = new SdkTasks.Build.ParallelFileHasher
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                SourceFiles = new ITaskItem[] { new TaskItem("only-here.txt") },
                ParallelProcessing = false
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.NotEmpty(task.ProcessedFiles);
            string resolvedFullPath = task.ProcessedFiles[0].GetMetadata("ResolvedFullPath");
            SharedTestHelpers.AssertNotResolvedToCwd(resolvedFullPath, _projectDir);
        }

        [Fact]
        public void ShouldProcessMultipleFilesAndResolveAllToProjectDir()
        {
            File.WriteAllText(Path.Combine(_projectDir, "file1.txt"), "one");
            File.WriteAllText(Path.Combine(_projectDir, "file2.txt"), "two");
            File.WriteAllText(Path.Combine(_projectDir, "file3.txt"), "three");

            var task = new SdkTasks.Build.ParallelFileHasher
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                SourceFiles = new ITaskItem[]
                {
                    new TaskItem("file1.txt"),
                    new TaskItem("file2.txt"),
                    new TaskItem("file3.txt")
                },
                ParallelProcessing = false
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(3, task.ProcessedFiles.Length);
            foreach (var item in task.ProcessedFiles)
            {
                string fullPath = item.GetMetadata("ResolvedFullPath");
                SharedTestHelpers.AssertPathUnderProjectDir(_projectDir, fullPath);
                Assert.False(string.IsNullOrEmpty(item.GetMetadata("FileHash")),
                    "Each processed file should have a FileHash metadata");
            }
        }

        [Fact]
        public void ShouldCallGetAbsolutePathForEachFile_ParallelMode()
        {
            File.WriteAllText(Path.Combine(_projectDir, "a.txt"), "aaa");
            File.WriteAllText(Path.Combine(_projectDir, "b.txt"), "bbb");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new SdkTasks.Build.ParallelFileHasher
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                SourceFiles = new ITaskItem[]
                {
                    new TaskItem("a.txt"),
                    new TaskItem("b.txt")
                },
                ParallelProcessing = true
            };

            bool result = task.Execute();

            Assert.True(result);
            SharedTestHelpers.AssertMinimumGetAbsolutePathCalls(trackingEnv, 2);
        }

        [Fact]
        public void ShouldResolveSubdirectoryFilesToProjectDir()
        {
            string subDir = Path.Combine(_projectDir, "subdir");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(subDir, "nested.txt"), "nested content");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new SdkTasks.Build.ParallelFileHasher
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                SourceFiles = new ITaskItem[] { new TaskItem(Path.Combine("subdir", "nested.txt")) },
                ParallelProcessing = false
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.NotEmpty(task.ProcessedFiles);
            string fullPath = task.ProcessedFiles[0].GetMetadata("ResolvedFullPath");
            SharedTestHelpers.AssertPathUnderProjectDir(_projectDir, fullPath);
            SharedTestHelpers.AssertUsesGetAbsolutePath(trackingEnv);
        }

        [Fact]
        public void ShouldReturnEmptyResultsForNoSourceFiles()
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
        public void ShouldPopulateAllMetadataOnOutputItems()
        {
            var subDir = Path.Combine(_projectDir, "src");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(subDir, "file1.txt"), "hello world");

            var task = new SdkTasks.Build.ParallelFileHasher
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                SourceFiles = new ITaskItem[] { new TaskItem(Path.Combine("src", "file1.txt")) }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Single(task.ProcessedFiles);

            var outputItem = task.ProcessedFiles[0];
            var resolvedFullPath = outputItem.GetMetadata("ResolvedFullPath");
            Assert.StartsWith(_projectDir, resolvedFullPath);
            Assert.False(Path.IsPathRooted(outputItem.ItemSpec),
                "ItemSpec should be a relative path under ProjectDirectory");
            Assert.False(string.IsNullOrEmpty(outputItem.GetMetadata("FileHash")));
            Assert.False(string.IsNullOrEmpty(outputItem.GetMetadata("FileSize")));
            Assert.False(string.IsNullOrEmpty(outputItem.GetMetadata("LastWriteTime")));
        }

        [Fact]
        public void ShouldNotContainCwdPathInLogMessages()
        {
            File.WriteAllText(Path.Combine(_projectDir, "data.bin"), "content");

            var task = new SdkTasks.Build.ParallelFileHasher
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                SourceFiles = new ITaskItem[] { new TaskItem("data.bin") }
            };

            task.Execute();

            var logText = string.Join(" ", _engine.Messages.Select(m => m.Message));
            var cwd = Directory.GetCurrentDirectory();
            if (!cwd.Equals(_projectDir, StringComparison.OrdinalIgnoreCase))
            {
                Assert.DoesNotContain(cwd, logText);
            }
        }

        [Fact]
        public void ShouldExcludeMissingFilesFromOutput()
        {
            var task = new SdkTasks.Build.ParallelFileHasher
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                SourceFiles = new ITaskItem[] { new TaskItem("nonexistent.txt") }
            };

            bool result = task.Execute();

            Assert.Empty(task.ProcessedFiles);
        }

        [Fact]
        public void ShouldProduceSameResultsInParallelAndSequentialModes()
        {
            for (int i = 0; i < 5; i++)
                File.WriteAllText(Path.Combine(_projectDir, $"file{i}.txt"), $"content-{i}");

            var sourceFiles = Enumerable.Range(0, 5)
                .Select(i => (ITaskItem)new TaskItem($"file{i}.txt"))
                .ToArray();

            var parallelTask = new SdkTasks.Build.ParallelFileHasher
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                SourceFiles = sourceFiles,
                ParallelProcessing = true
            };
            parallelTask.Execute();

            var sequentialTask = new SdkTasks.Build.ParallelFileHasher
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                SourceFiles = sourceFiles,
                ParallelProcessing = false
            };
            sequentialTask.Execute();

            Assert.Equal(parallelTask.ProcessedFiles.Length, sequentialTask.ProcessedFiles.Length);

            var parallelHashes = parallelTask.ProcessedFiles
                .Select(f => f.GetMetadata("FileHash")).OrderBy(h => h).ToArray();
            var sequentialHashes = sequentialTask.ProcessedFiles
                .Select(f => f.GetMetadata("FileHash")).OrderBy(h => h).ToArray();
            Assert.Equal(sequentialHashes, parallelHashes);
        }
    }
}
