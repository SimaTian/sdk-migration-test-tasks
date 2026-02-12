using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class DirectoryContextSwitcherTests : IDisposable
    {
        private readonly string _projectDir;

        public DirectoryContextSwitcherTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Build.DirectoryContextSwitcher();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Build.DirectoryContextSwitcher),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldNotModifyGlobalCwd()
        {
            var originalCwd = Environment.CurrentDirectory;

            var task = new SdkTasks.Build.DirectoryContextSwitcher
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                NewDirectory = _projectDir
            };

            task.Execute();

            Assert.Equal(originalCwd, Environment.CurrentDirectory);

            // Restore CWD in case task changed it
            Environment.CurrentDirectory = originalCwd;
        }
    }
}
