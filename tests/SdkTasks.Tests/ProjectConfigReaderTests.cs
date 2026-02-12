using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class ProjectConfigReaderTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public ProjectConfigReaderTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Compilation.ProjectConfigReader();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Compilation.ProjectConfigReader),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldResolveRelativeToProjectDirectory()
        {
            var relativePath = "data.xml";
            File.WriteAllText(Path.Combine(_projectDir, relativePath), "<root><item/></root>");

            var task = new SdkTasks.Compilation.ProjectConfigReader
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                XmlPath = relativePath
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("Loaded XML with"));
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("Saved updated XML"));
        }
    }
}
