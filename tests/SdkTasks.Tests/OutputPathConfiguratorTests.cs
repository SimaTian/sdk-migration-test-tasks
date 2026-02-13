using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class OutputPathConfiguratorTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public OutputPathConfiguratorTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Build.OutputPathConfigurator();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Build.OutputPathConfigurator),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldResolveToProjectDirectory()
        {
            var task = new SdkTasks.Build.OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputDirectory = "bin"
            };

            task.Execute();

            Assert.Contains(_projectDir, task.ResolvedOutputDirectory, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ResolveDirectory_UsesTaskEnvironment_GetAbsolutePath()
        {
            var taskEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Build.OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                OutputDirectory = "bin"
            };

            task.Execute();

            // Verify GetAbsolutePath was called
            Assert.True(taskEnv.GetAbsolutePathCallCount > 0, "Should call GetAbsolutePath");
            Assert.Contains("bin", taskEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void Execute_ProducesCorrectPaths_WhenCwdIsDifferent()
        {
            var outputDir = "out";
            var task = new SdkTasks.Build.OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputDirectory = outputDir
            };

            task.Execute();

            var expected = Path.Combine(_projectDir, outputDir) + Path.DirectorySeparatorChar;
            Assert.Equal(expected, task.ResolvedOutputDirectory);
        }

        [Fact]
        public void Execute_InitializesProjectDirectory_FromBuildEngine()
        {
            // Do NOT set ProjectDirectory in TaskEnvironment
            var env = new TrackingTaskEnvironment();
            var task = new SdkTasks.Build.OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = env,
                OutputDirectory = "bin"
            };

            // Mock engine returns absolute path
            var projectFile = Path.Combine(_projectDir, "test.csproj");
            _engine.ProjectFileOfTaskNode = projectFile;

            task.Execute();

            Assert.Equal(_projectDir, env.ProjectDirectory);
        }
    }
}
