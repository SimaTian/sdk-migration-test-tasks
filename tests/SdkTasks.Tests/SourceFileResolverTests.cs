using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class SourceFileResolverTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public SourceFileResolverTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Compilation.SourceFileResolver();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Compilation.SourceFileResolver),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldResolveRelativeToProjectDirectory()
        {
            var relativePath = "ignoretask.txt";
            File.WriteAllText(Path.Combine(_projectDir, relativePath), "data");

            var task = new SdkTasks.Compilation.SourceFileResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = relativePath
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("File size:"));
        }
    }
}
