using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class SharedBuildStateManagerTests : IDisposable
    {
        private readonly List<string> _tempDirs = new();

        private string CreateProjectDir()
        {
            var dir = TestHelper.CreateNonCwdTempDirectory();
            _tempDirs.Add(dir);
            return dir;
        }

        public void Dispose()
        {
            foreach (var dir in _tempDirs)
                TestHelper.CleanupTempDirectory(dir);
        }

        [Fact]
        public void ShouldResolveToOwnProjectDirectory()
        {
            var dir1 = CreateProjectDir();
            var dir2 = CreateProjectDir();

            File.WriteAllText(Path.Combine(dir1, "app.config"), "config-from-dir1");
            File.WriteAllText(Path.Combine(dir2, "app.config"), "config-from-dir2");

            var engine = new MockBuildEngine();

            var task1 = new SdkTasks.Build.SharedBuildStateManager
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir1),
                ConfigFileName = "app.config",
            };

            var task2 = new SdkTasks.Build.SharedBuildStateManager
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir2),
                ConfigFileName = "app.config",
            };

            Assert.True(task1.Execute());
            Assert.True(task2.Execute());

            // Each task resolves config to its own ProjectDirectory
            Assert.StartsWith(dir1, task1.ConfigFilePath, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(dir2, task2.ConfigFilePath, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(task1.ConfigFilePath, task2.ConfigFilePath);
        }

        [Fact]
        public void ShouldCallGetAbsolutePathOnTaskEnvironment()
        {
            var dir = CreateProjectDir();
            File.WriteAllText(Path.Combine(dir, "settings.json"), "{}");

            var tracking = SharedTestHelpers.CreateTrackingEnvironment(dir);

            var task = new SdkTasks.Build.SharedBuildStateManager
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = tracking,
                ConfigFileName = "settings.json",
            };

            Assert.True(task.Execute());

            // Verify task uses TaskEnvironment.GetAbsolutePath instead of Path.GetFullPath
            SharedTestHelpers.AssertUsesGetAbsolutePath(tracking);
            Assert.Contains("settings.json", tracking.GetAbsolutePathArgs);
        }

        [Fact]
        public void ShouldResolveRelativeToProjectDirectory_NotCwd()
        {
            var projectDir = CreateProjectDir();
            var configName = "build.cfg";
            File.WriteAllText(Path.Combine(projectDir, configName), "build-config");

            var task = new SdkTasks.Build.SharedBuildStateManager
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                ConfigFileName = configName,
            };

            Assert.True(task.Execute());

            var expectedPath = Path.Combine(projectDir, configName);

            // Path must resolve under ProjectDirectory, not process CWD
            Assert.Equal(expectedPath, task.ConfigFilePath, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(Directory.GetCurrentDirectory(), task.ConfigFilePath,
                StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldReturnCachedPathOnSecondInvocation()
        {
            var dir = CreateProjectDir();
            File.WriteAllText(Path.Combine(dir, "cache.config"), "cached");

            var engine = new MockBuildEngine();

            var task1 = new SdkTasks.Build.SharedBuildStateManager
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir),
                ConfigFileName = "cache.config",
            };

            var task2 = new SdkTasks.Build.SharedBuildStateManager
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir),
                ConfigFileName = "cache.config",
            };

            Assert.True(task1.Execute());
            Assert.True(task2.Execute());

            // Second invocation should reuse cached path
            Assert.Equal(task1.ConfigFilePath, task2.ConfigFilePath, StringComparer.OrdinalIgnoreCase);
            Assert.True(task2.ConfigLoaded);
            Assert.Contains(engine.Messages,
                m => m.Message!.Contains("Cache hit", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ShouldFailWithEmptyConfigFileName()
        {
            var dir = CreateProjectDir();

            var engine = new MockBuildEngine();
            var task = new SdkTasks.Build.SharedBuildStateManager
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir),
                ConfigFileName = "  ",
            };

            Assert.False(task.Execute());
            Assert.Contains(engine.Errors,
                e => e.Message!.Contains("ConfigFileName", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ShouldSetConfigLoadedFalse_WhenFileDoesNotExist()
        {
            var dir = CreateProjectDir();

            var task = new SdkTasks.Build.SharedBuildStateManager
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir),
                ConfigFileName = "nonexistent.config",
            };

            Assert.True(task.Execute());
            Assert.False(task.ConfigLoaded);
            Assert.StartsWith(dir, task.ConfigFilePath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
