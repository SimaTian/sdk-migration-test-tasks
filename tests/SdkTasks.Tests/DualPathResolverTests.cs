using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class DualPathResolverTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public DualPathResolverTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Build.DualPathResolver();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Build.DualPathResolver),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldResolveBothPathsToProjectDirectory()
        {
            var primaryFile = "primary.txt";
            var secondaryFile = "secondary.txt";
            File.WriteAllText(Path.Combine(_projectDir, primaryFile), "same");
            File.WriteAllText(Path.Combine(_projectDir, secondaryFile), "same");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.DualPathResolver
            {
                BuildEngine = _engine,
                PrimaryPath = primaryFile,
                SecondaryPath = secondaryFile,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.True(task.FilesMatch);
        }
    }
}
