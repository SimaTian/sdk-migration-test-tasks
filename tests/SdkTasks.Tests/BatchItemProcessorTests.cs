using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class BatchItemProcessorTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public BatchItemProcessorTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Compilation.BatchItemProcessor();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Compilation.BatchItemProcessor),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldResolveRelativeToProjectDirectory()
        {
            var relativePaths = new[] { "file1.txt", "subdir\\file2.txt" };

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Compilation.BatchItemProcessor
            {
                BuildEngine = _engine,
                RelativePaths = relativePaths,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(Path.Combine(_projectDir, "file1.txt"), task.AbsolutePaths[0]);
            Assert.Equal(Path.Combine(_projectDir, "subdir\\file2.txt"), task.AbsolutePaths[1]);
        }

        [Fact]
        public void AutoInitializesProjectDirectoryFromBuildEngine()
        {
            // Arrange
            string projectFile = Path.Combine(_projectDir, "test.proj");
            _engine.ProjectFileOfTaskNode = projectFile;

            // Create TaskEnvironment with empty ProjectDirectory
            var taskEnv = new TaskEnvironment(); 
            // Note: ProjectDirectory is empty by default

            var task = new SdkTasks.Compilation.BatchItemProcessor
            {
                BuildEngine = _engine,
                RelativePaths = new[] { "file.txt" },
                TaskEnvironment = taskEnv
            };

            // Act
            task.Execute();

            // Assert
            Assert.Equal(_projectDir, taskEnv.ProjectDirectory);
            Assert.Equal(Path.Combine(_projectDir, "file.txt"), task.AbsolutePaths[0]);
        }

        [Fact]
        public void HandlesNullTaskEnvironment()
        {
            // This test verifies that the task can run even if TaskEnvironment is not injected (e.g. single-threaded mode)
            // It should auto-initialize TaskEnvironment
            
            string projectFile = Path.Combine(_projectDir, "test.proj");
            _engine.ProjectFileOfTaskNode = projectFile;

            var task = new SdkTasks.Compilation.BatchItemProcessor
            {
                BuildEngine = _engine,
                RelativePaths = new[] { "file.txt" },
                TaskEnvironment = null! // Simulate missing injection
            };

            // Act
            bool result = task.Execute();

            // Assert
            Assert.True(result);
            Assert.NotNull(task.TaskEnvironment);
            Assert.Equal(_projectDir, task.TaskEnvironment.ProjectDirectory);
             Assert.Equal(Path.Combine(_projectDir, "file.txt"), task.AbsolutePaths[0]);
        }
    }
}
