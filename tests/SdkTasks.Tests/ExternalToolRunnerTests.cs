using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class ExternalToolRunnerTests : IDisposable
    {
        private readonly string _projectDir;

        public ExternalToolRunnerTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Tools.ExternalToolRunner),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldImplementIMultiThreadableTask()
        {
            var task = new SdkTasks.Tools.ExternalToolRunner();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void ShouldSetWorkingDirectoryToProjectDir()
        {
            var engine = new MockBuildEngine();

            var task = new SdkTasks.Tools.ExternalToolRunner
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                Command = "cmd.exe",
                Arguments = "/c cd"
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(engine.Messages!, m => m.Message!.Contains(_projectDir));
        }
    }
}
