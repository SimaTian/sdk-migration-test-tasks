using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class DependencyPathResolverTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public DependencyPathResolverTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Analysis.DependencyPathResolver();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Analysis.DependencyPathResolver),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldResolveRelativeToProjectDirectory()
        {
            var fileName = "indirect-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "hello");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Analysis.DependencyPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(Path.Combine(_projectDir, fileName), task.ResolvedPath);
        }

        [Fact]
        public void ShouldAutoInitializeProjectDirectoryFromBuildEngine()
        {
            var fileName = "auto-init-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            // Create TaskEnvironment with empty ProjectDirectory
            var taskEnv = new TaskEnvironment();
            
            // Set BuildEngine with absolute project file path
            var engine = new MockBuildEngine
            {
                ProjectFileOfTaskNode = Path.Combine(_projectDir, "test.csproj")
            };
            
            var task = new SdkTasks.Analysis.DependencyPathResolver
            {
                BuildEngine = engine,
                InputPath = fileName,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.NotNull(task.ResolvedPath);
            Assert.Equal(Path.Combine(_projectDir, fileName), task.ResolvedPath);
        }
    }
}
