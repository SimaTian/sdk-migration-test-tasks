using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class ConfigurationValidatorTests : IDisposable
    {
        private readonly List<string> _tempDirs = new();
        private readonly List<string> _envVarsToClean = new();

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
            foreach (var name in _envVarsToClean)
                Environment.SetEnvironmentVariable(name, null);
        }

        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Configuration.ConfigurationValidator();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Configuration.ConfigurationValidator),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldUseTaskScopedEnvVars()
        {
            var dir1 = CreateProjectDir();
            var dir2 = CreateProjectDir();
            var configKey = "TEST_CONFIG_" + Guid.NewGuid().ToString("N")[..8];
            _envVarsToClean.Add(configKey);

            var taskEnv1 = TaskEnvironmentHelper.CreateForTest(dir1);
            taskEnv1.SetEnvironmentVariable(configKey, "valueA");

            var task1 = new SdkTasks.Configuration.ConfigurationValidator
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv1,
                ConfigKey = configKey,
                FallbackValue = "fallback",
            };

            Assert.True(task1.Execute());

            var taskEnv2 = TaskEnvironmentHelper.CreateForTest(dir2);
            taskEnv2.SetEnvironmentVariable(configKey, "valueB");

            var task2 = new SdkTasks.Configuration.ConfigurationValidator
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv2,
                ConfigKey = configKey,
                FallbackValue = "fallback",
            };

            Assert.True(task2.Execute());

            Assert.Contains("valueA", task1.ResolvedConfig);
            Assert.Contains("valueB", task2.ResolvedConfig);
        }
    }
}
