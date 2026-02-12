using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class OutputDirectoryValidatorTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public OutputDirectoryValidatorTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Build.OutputDirectoryValidator();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Build.OutputDirectoryValidator),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldResolveRelativeToProjectDirectory()
        {
            var relativePath = "mysubdir";
            Directory.CreateDirectory(Path.Combine(_projectDir, relativePath));
            File.WriteAllText(Path.Combine(_projectDir, relativePath, "dummy.txt"), "x");

            var task = new SdkTasks.Build.OutputDirectoryValidator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                DirectoryPath = relativePath
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("exists with") && m.Message!.Contains("file(s)"));
        }
    }
}
