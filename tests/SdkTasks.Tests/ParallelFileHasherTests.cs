using System;
using System.IO;
using System.Linq;
using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class ParallelFileHasherTests : IDisposable
    {
        private readonly string _projectDir;

        public ParallelFileHasherTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldMatchSingleThreadedOutputsWhenTaskEnvironmentIsInitialized()
        {
            string testFile = Path.Combine(_projectDir, "data.txt");
            File.WriteAllText(testFile, "hello world");

            var baselineEngine = new MockBuildEngine();
            var baselineTask = new SdkTasks.Build.ParallelFileHasher
            {
                BuildEngine = baselineEngine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                SourceFiles = new ITaskItem[] { new TaskItem("data.txt") },
                ParallelProcessing = false
            };

            bool baselineResult = baselineTask.Execute();

            Assert.True(baselineResult);
            Assert.Single(baselineTask.ProcessedFiles);
            var baselineItem = baselineTask.ProcessedFiles[0];
            var baselineMessages = baselineEngine.Messages.Select(message => message.Message).ToArray();

            var multithreadEngine = new MockBuildEngine
            {
                ProjectFileOfTaskNode = Path.Combine(_projectDir, "test.csproj")
            };
            var multithreadTask = new SdkTasks.Build.ParallelFileHasher
            {
                BuildEngine = multithreadEngine,
                TaskEnvironment = new TaskEnvironment(),
                SourceFiles = new ITaskItem[] { new TaskItem("data.txt") },
                ParallelProcessing = false
            };

            bool multithreadResult = multithreadTask.Execute();

            Assert.True(multithreadResult);
            Assert.Single(multithreadTask.ProcessedFiles);
            var multithreadItem = multithreadTask.ProcessedFiles[0];

            Assert.Equal(baselineItem.ItemSpec, multithreadItem.ItemSpec);
            Assert.Equal(baselineItem.GetMetadata("ResolvedFullPath"), multithreadItem.GetMetadata("ResolvedFullPath"));
            Assert.Equal(baselineItem.GetMetadata("FileHash"), multithreadItem.GetMetadata("FileHash"));
            Assert.Equal(baselineItem.GetMetadata("FileSize"), multithreadItem.GetMetadata("FileSize"));
            Assert.Equal(baselineItem.GetMetadata("LastWriteTime"), multithreadItem.GetMetadata("LastWriteTime"));
            Assert.Equal(baselineMessages, multithreadEngine.Messages.Select(message => message.Message));
        }
    }
}
