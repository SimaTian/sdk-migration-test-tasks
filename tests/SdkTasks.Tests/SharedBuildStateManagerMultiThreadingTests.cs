using Microsoft.Build.Framework;
using SdkTasks.Build;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests
{
    public class SharedBuildStateManagerMultiThreadingTests : IDisposable
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
        public void ItImplementsIMultiThreadableTask()
        {
            var task = new SharedBuildStateManager();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void ItHasMSBuildMultiThreadableTaskAttribute()
        {
            Assert.NotNull(
                Attribute.GetCustomAttribute(typeof(SharedBuildStateManager),
                    typeof(MSBuildMultiThreadableTaskAttribute)));
        }

        [Fact]
        public void ItResolvesConfigFileRelativeToProjectDirectory()
        {
            var projectDir = CreateProjectDir();
            string configFileName = "myconfig.json";
            string expectedAbsPath = Path.Combine(projectDir, configFileName);
            File.WriteAllText(expectedAbsPath, "{\"key\":\"value\"}");

            var task = new SharedBuildStateManager
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                ConfigFileName = configFileName,
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(expectedAbsPath, task.ConfigFilePath);
            Assert.True(task.ConfigLoaded, "config file exists at the project-relative path");
        }

        [Fact]
        public void ItReturnsCachedPathOnSecondInvocation()
        {
            var projectDir = CreateProjectDir();
            string configFileName = "cached.json";
            File.WriteAllText(Path.Combine(projectDir, configFileName), "content");

            var engine = new MockBuildEngine();

            var task1 = new SharedBuildStateManager
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                ConfigFileName = configFileName,
            };
            Assert.True(task1.Execute());
            string firstPath = task1.ConfigFilePath;

            var task2 = new SharedBuildStateManager
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                ConfigFileName = configFileName,
            };
            Assert.True(task2.Execute());

            Assert.Equal(firstPath, task2.ConfigFilePath);
            Assert.True(task2.ConfigLoaded);
        }

        [Fact]
        public void ItSetsConfigLoadedFalseWhenFileNotFound()
        {
            var projectDir = CreateProjectDir();

            var task = new SharedBuildStateManager
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                ConfigFileName = "nonexistent.json",
            };

            bool result = task.Execute();

            Assert.True(result, "task should succeed even when file not found");
            Assert.False(task.ConfigLoaded, "file does not exist");
            Assert.Equal(Path.Combine(projectDir, "nonexistent.json"), task.ConfigFilePath);
        }
    }
}
