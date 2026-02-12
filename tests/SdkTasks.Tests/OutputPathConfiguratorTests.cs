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
    }
}
