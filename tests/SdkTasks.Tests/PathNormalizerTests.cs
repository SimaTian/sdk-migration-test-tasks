using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class PathNormalizerTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public PathNormalizerTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Build.PathNormalizer),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldImplementIMultiThreadableTask()
        {
            var task = new SdkTasks.Build.PathNormalizer();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void ShouldResolveRelativeToProjectDirectory()
        {
            var relativePath = "testfile.txt";
            File.WriteAllText(Path.Combine(_projectDir, relativePath), "hello");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.PathNormalizer
            {
                BuildEngine = _engine,
                InputPath = relativePath,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("File found at"));
        }
    }
}
