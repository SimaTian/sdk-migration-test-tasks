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

            Assert.StartsWith(dir1, task1.ConfigFilePath, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(dir2, task2.ConfigFilePath, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(task1.ConfigFilePath, task2.ConfigFilePath);
        }

        [Fact]
        public void ShouldCallGetAbsolutePathOnTaskEnvironment()
        {
            var projectDir = CreateProjectDir();
            File.WriteAllText(Path.Combine(projectDir, "tracked.config"), "tracked-content");

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = projectDir };

            var task = new SdkTasks.Build.SharedBuildStateManager
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = trackingEnv,
                ConfigFileName = "tracked.config",
            };

            Assert.True(task.Execute());

            SharedTestHelpers.AssertGetAbsolutePathCalled(trackingEnv);
            Assert.Contains("tracked.config", trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void ShouldNotResolveConfigRelativeToProcessCwd()
        {
            var projectDir = CreateProjectDir();
            var cwd = Directory.GetCurrentDirectory();
            File.WriteAllText(Path.Combine(projectDir, "my.config"), "data");

            var task = new SdkTasks.Build.SharedBuildStateManager
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                ConfigFileName = "my.config",
            };

            Assert.True(task.Execute());

            Assert.StartsWith(projectDir, task.ConfigFilePath, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(cwd, task.ConfigFilePath, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldLoadConfigContentWhenFileExists()
        {
            var projectDir = CreateProjectDir();
            File.WriteAllText(Path.Combine(projectDir, "settings.config"), "setting=value");

            var task = new SdkTasks.Build.SharedBuildStateManager
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                ConfigFileName = "settings.config",
            };

            Assert.True(task.Execute());
            Assert.True(task.ConfigLoaded);
        }

        [Fact]
        public void ShouldReuseCachedStateOnSecondInvocationWithSameEngine()
        {
            var projectDir = CreateProjectDir();
            File.WriteAllText(Path.Combine(projectDir, "cache-test.config"), "cached-data");

            var engine = new MockBuildEngine();

            var task1 = new SdkTasks.Build.SharedBuildStateManager
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                ConfigFileName = "cache-test.config",
            };

            var task2 = new SdkTasks.Build.SharedBuildStateManager
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                ConfigFileName = "cache-test.config",
            };

            Assert.True(task1.Execute());
            Assert.True(task2.Execute());

            // Both should resolve to the same path
            Assert.Equal(task1.ConfigFilePath, task2.ConfigFilePath);

            // Second invocation should get a cache hit via IBuildEngine4
            Assert.Contains(engine.Messages,
                m => m.Message != null && m.Message.Contains("Cache hit"));
        }
    }
}
