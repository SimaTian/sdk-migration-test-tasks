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
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Build.ParallelFileHasher();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Build.ParallelFileHasher),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

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
    }
}
