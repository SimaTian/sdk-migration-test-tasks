using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class DeferredConfigLoaderTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public DeferredConfigLoaderTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Configuration.DeferredConfigLoader();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Configuration.DeferredConfigLoader),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldUseTaskEnvironment()
        {
            var nugetDir = Path.Combine(_projectDir, "nuget-packages");
            var cacheDir = Path.Combine(nugetDir, "cache");
            Directory.CreateDirectory(cacheDir);

            File.WriteAllText(Path.Combine(cacheDir, "dependency-config.json"),
                "TestDep=1.0.0");

            var sdkDir = Path.Combine(_projectDir, "dotnet-sdk");
            Directory.CreateDirectory(Path.Combine(sdkDir, "packs", "TestDep", "1.0.0"));

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            taskEnv.SetEnvironmentVariable("NUGET_PACKAGES", nugetDir);
            taskEnv.SetEnvironmentVariable("DOTNET_ROOT", sdkDir);

            var task = new SdkTasks.Configuration.DeferredConfigLoader
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                ConfigurationFile = "config.json",
                TargetFramework = "net8.0"
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.NotEmpty(task.ResolvedDependencies);
        }
    }
}
