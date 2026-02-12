using System;
using System.IO;
using System.Linq;
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

        [Fact]
        public void Execute_CallsGetEnvironmentVariable_ForNugetAndDotnetRoot()
        {
            var nugetDir = Path.Combine(_projectDir, "nuget-packages");
            var cacheDir = Path.Combine(nugetDir, "cache");
            Directory.CreateDirectory(cacheDir);
            File.WriteAllText(Path.Combine(cacheDir, "dependency-config.json"),
                "TestDep=1.0.0");

            var sdkDir = Path.Combine(_projectDir, "dotnet-sdk");
            Directory.CreateDirectory(Path.Combine(sdkDir, "packs", "TestDep", "1.0.0"));

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
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

            // Task should read env vars through TaskEnvironment, not System.Environment
            Assert.True(trackingEnv.GetEnvironmentVariableCallCount >= 2,
                $"Expected at least 2 GetEnvironmentVariable calls, got {trackingEnv.GetEnvironmentVariableCallCount}");
        }

        [Fact]
        public void Execute_CallsGetAbsolutePath_ForPathResolution()
        {
            var nugetDir = Path.Combine(_projectDir, "nuget-packages");
            var cacheDir = Path.Combine(nugetDir, "cache");
            Directory.CreateDirectory(cacheDir);
            File.WriteAllText(Path.Combine(cacheDir, "dependency-config.json"),
                "TestDep=1.0.0");

            var sdkDir = Path.Combine(_projectDir, "dotnet-sdk");
            Directory.CreateDirectory(Path.Combine(sdkDir, "packs", "TestDep", "1.0.0"));

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
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

            // Task should resolve paths through TaskEnvironment.GetAbsolutePath
            Assert.True(trackingEnv.GetAbsolutePathCallCount >= 1,
                $"Expected at least 1 GetAbsolutePath call, got {trackingEnv.GetAbsolutePathCallCount}");
        }

        [Fact]
        public void Execute_ResolvesConfigPath_RelativeToProjectDirectory()
        {
            // ConfigurationFile is passed as a relative path; the task should
            // resolve it via TaskEnvironment.GetAbsolutePath (project-relative),
            // NOT via the process CWD.
            var nugetDir = Path.Combine(_projectDir, "nuget-packages");
            var cacheDir = Path.Combine(nugetDir, "cache");
            Directory.CreateDirectory(cacheDir);
            File.WriteAllText(Path.Combine(cacheDir, "dependency-config.json"),
                "TestDep=1.0.0");

            var sdkDir = Path.Combine(_projectDir, "dotnet-sdk");
            Directory.CreateDirectory(Path.Combine(sdkDir, "packs", "TestDep", "1.0.0"));

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
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

            // Verify the ConfigurationFile relative path was resolved via GetAbsolutePath
            Assert.Contains("config.json", trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void Execute_ResolvesDependencyPaths_RelativeToProjectDirectory()
        {
            // Create nuget cache with a dependency that can only be resolved
            // under a "packages" subfolder of ProjectDirectory
            var nugetDir = Path.Combine(_projectDir, "nuget-packages");
            var cacheDir = Path.Combine(nugetDir, "cache");
            Directory.CreateDirectory(cacheDir);
            File.WriteAllText(Path.Combine(cacheDir, "dependency-config.json"),
                "MyLib=2.0.0");

            // Create the local packages dir under ProjectDirectory
            var localPkgDir = Path.Combine(_projectDir, "packages", "MyLib", "2.0.0", "lib", "net8.0");
            Directory.CreateDirectory(localPkgDir);

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            trackingEnv.SetEnvironmentVariable("NUGET_PACKAGES", nugetDir);

            var task = new SdkTasks.Configuration.DeferredConfigLoader
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                ConfigurationFile = "config.json",
                TargetFramework = "net8.0"
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.NotEmpty(task.ResolvedDependencies);

            // The resolved dependency path should be under the project directory
            string resolvedPath = task.ResolvedDependencies[0].ItemSpec;
            SharedTestHelpers.AssertPathUnderProjectDir(_projectDir, resolvedPath);
        }

        [Fact]
        public void Execute_UsesTaskEnvironmentEnvVars_NotProcessEnvironment()
        {
            // Set NUGET_PACKAGES and DOTNET_ROOT ONLY in TaskEnvironment (not process env).
            // If the task reads from System.Environment, it won't find these paths.
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

            // The task should find and resolve the dependency using TaskEnvironment env vars
            Assert.True(result, $"Execute failed. Errors: {string.Join("; ", _engine.Errors.Select(e => e.Message))}");
            Assert.NotEmpty(task.ResolvedDependencies);
        }

        [Fact]
        public void Execute_EmptyConfigurationFile_ReturnsError()
        {
            var task = new SdkTasks.Configuration.DeferredConfigLoader
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                ConfigurationFile = string.Empty
            };

            bool result = task.Execute();

            Assert.False(result);
            Assert.NotEmpty(_engine.Errors);
        }

        [Fact]
        public void Execute_NoDependenciesFound_ReturnsEmptyArray()
        {
            // Provide a nuget cache with an empty config file (no dependencies)
            var nugetDir = Path.Combine(_projectDir, "nuget-packages");
            var cacheDir = Path.Combine(nugetDir, "cache");
            Directory.CreateDirectory(cacheDir);
            File.WriteAllText(Path.Combine(cacheDir, "dependency-config.json"),
                "# empty config");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            taskEnv.SetEnvironmentVariable("NUGET_PACKAGES", nugetDir);

            var task = new SdkTasks.Configuration.DeferredConfigLoader
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                ConfigurationFile = "config.json",
                TargetFramework = "net8.0"
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Empty(task.ResolvedDependencies);
        }
    }
}
