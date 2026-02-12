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
        public void ShouldUseTaskEnvironmentForDependencyResolution()
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

        [Fact]
        public void ShouldCallTaskEnvironmentGetEnvironmentVariable()
        {
            var nugetDir = Path.Combine(_projectDir, "nuget-packages");
            var cacheDir = Path.Combine(nugetDir, "cache");
            Directory.CreateDirectory(cacheDir);
            File.WriteAllText(Path.Combine(cacheDir, "dependency-config.json"),
                "TestDep=1.0.0");

            var sdkDir = Path.Combine(_projectDir, "dotnet-sdk");
            Directory.CreateDirectory(Path.Combine(sdkDir, "packs", "TestDep", "1.0.0"));

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            trackingEnv.SetEnvironmentVariable("NUGET_PACKAGES", nugetDir);
            trackingEnv.SetEnvironmentVariable("DOTNET_ROOT", sdkDir);

            var task = new SdkTasks.Configuration.DeferredConfigLoader
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                ConfigurationFile = "config.json",
                TargetFramework = "net8.0"
            };

            task.Execute();

            SharedTestHelpers.AssertGetEnvironmentVariableCalled(trackingEnv);
        }

        [Fact]
        public void ShouldCallTaskEnvironmentGetAbsolutePath()
        {
            var nugetDir = Path.Combine(_projectDir, "nuget-packages");
            var cacheDir = Path.Combine(nugetDir, "cache");
            Directory.CreateDirectory(cacheDir);
            File.WriteAllText(Path.Combine(cacheDir, "dependency-config.json"),
                "TestDep=1.0.0");

            var sdkDir = Path.Combine(_projectDir, "dotnet-sdk");
            Directory.CreateDirectory(Path.Combine(sdkDir, "packs", "TestDep", "1.0.0"));

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            trackingEnv.SetEnvironmentVariable("NUGET_PACKAGES", nugetDir);
            trackingEnv.SetEnvironmentVariable("DOTNET_ROOT", sdkDir);

            var task = new SdkTasks.Configuration.DeferredConfigLoader
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                ConfigurationFile = "config.json",
                TargetFramework = "net8.0"
            };

            task.Execute();

            SharedTestHelpers.AssertGetAbsolutePathCalled(trackingEnv);
        }

        [Fact]
        public void ShouldFallbackToUserProfileForGlobalPackages()
        {
            var nugetDir = Path.Combine(_projectDir, ".nuget", "packages");
            var cacheDir = Path.Combine(nugetDir, "..", "..", "nuget-packages", "cache");
            Directory.CreateDirectory(cacheDir);
            // Use USERPROFILE fallback path for the cache
            var userProfileCacheDir = Path.Combine(_projectDir, ".nuget", "packages", "cache");
            Directory.CreateDirectory(Path.GetDirectoryName(userProfileCacheDir)!);

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            // Set NUGET_PACKAGES to null so it falls back to USERPROFILE
            taskEnv.SetEnvironmentVariable("NUGET_PACKAGES", null);
            taskEnv.SetEnvironmentVariable("USERPROFILE", _projectDir);
            taskEnv.SetEnvironmentVariable("HOME", _projectDir);

            var globalCacheDir = Path.Combine(_projectDir, ".nuget", "packages", "cache");
            Directory.CreateDirectory(globalCacheDir);
            File.WriteAllText(Path.Combine(globalCacheDir, "dependency-config.json"),
                "FallbackDep=2.0.0");

            var sdkDir = Path.Combine(_projectDir, "dotnet-sdk");
            Directory.CreateDirectory(Path.Combine(sdkDir, "packs", "FallbackDep", "2.0.0"));
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
