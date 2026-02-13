using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class DirectPathResolverTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public DirectPathResolverTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Build.DirectPathResolver();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Build.DirectPathResolver),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldResolveRelativeToProjectDirectory()
        {
            var fileName = "ignore-env-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.DirectPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                EnvVarName = "TEST_CONFIG",
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("Processing file"));
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

            var task = new SdkTasks.Build.DirectPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                EnvVarName = "TEST_VAR",
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains(absolutePath));
        }
    }
}
