using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class WorkingDirectoryResolverTests : IDisposable
    {
        private readonly string _projectDir;

        public WorkingDirectoryResolverTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Build.WorkingDirectoryResolver();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Build.WorkingDirectoryResolver),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldReadProjectDirectory()
        {
            var task = new SdkTasks.Build.WorkingDirectoryResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
            };

            task.Execute();

            Assert.Equal(_projectDir, task.CurrentDir);
        }
    }
}
