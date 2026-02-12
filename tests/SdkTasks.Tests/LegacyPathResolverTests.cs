using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class LegacyPathResolverTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public LegacyPathResolverTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Compatibility.LegacyPathResolver),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldImplementIMultiThreadableTask()
        {
            var task = new SdkTasks.Compatibility.LegacyPathResolver();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void ShouldResolveRelativeToProjectDirectory()
        {
            var fileName = "test-input.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var task = new SdkTasks.Compatibility.LegacyPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                EnvVarName = "TEST_VAR"
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.DoesNotContain(_engine.Messages, m => m.Message!.Contains("File not found"));
        }
    }
}
