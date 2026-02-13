using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class SafePathResolverTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public SafePathResolverTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Build.SafePathResolver();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Build.SafePathResolver),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldResolveRelativeToProjectDirectory()
        {
            var fileName = "null-check-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "data");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.SafePathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            var expectedResolved = Path.Combine(_projectDir, fileName);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains(expectedResolved));
        }

        [Fact]
        public void ShouldAutoInitializeProjectDirectoryFromBuildEngine()
        {
            var fileName = "auto-init-test.txt";
            var absolutePath = Path.Combine(_projectDir, fileName);
            File.WriteAllText(absolutePath, "test content");

            var projectFile = Path.Combine(_projectDir, "test.csproj");
            _engine.ProjectFileOfTaskNode = projectFile;

            // Empty TaskEnvironment (simulating default state)
            var taskEnv = new TaskEnvironment();

            var task = new SdkTasks.Build.SafePathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains(absolutePath));
        }
    }
}
