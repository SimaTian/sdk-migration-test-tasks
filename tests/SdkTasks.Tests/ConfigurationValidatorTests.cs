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

        [Fact]
        public void ShouldCallTaskEnvironmentGetEnvironmentVariable()
        {
            var dir1 = CreateProjectDir();
            var configKey = "TEST_TRACKING_" + Guid.NewGuid().ToString("N")[..8];
            _envVarsToClean.Add(configKey);

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = dir1 };
            trackingEnv.SetEnvironmentVariable(configKey, "trackedValue");

            var task = new SdkTasks.Configuration.ConfigurationValidator
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = trackingEnv,
                ConfigKey = configKey,
                FallbackValue = "fallback",
            };

            task.Execute();

            SharedTestHelpers.AssertGetEnvironmentVariableCalled(trackingEnv, 2);
        }

        [Fact]
        public void ShouldUseFallbackWhenEnvVarNotSet()
        {
            var dir1 = CreateProjectDir();
            var configKey = "NONEXISTENT_CONFIG_" + Guid.NewGuid().ToString("N")[..8];
            _envVarsToClean.Add(configKey);

            var taskEnv = TaskEnvironmentHelper.CreateForTest(dir1);

            var task = new SdkTasks.Configuration.ConfigurationValidator
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                ConfigKey = configKey,
                FallbackValue = "myFallback",
            };

            Assert.True(task.Execute());
            Assert.Contains("myFallback", task.ResolvedConfig);
        }

        [Fact]
        public void ShouldNotReadFromProcessEnvironment()
        {
            var dir1 = CreateProjectDir();
            var configKey = "TEST_PROCESS_" + Guid.NewGuid().ToString("N")[..8];
            _envVarsToClean.Add(configKey);

            // Set in process environment only
            Environment.SetEnvironmentVariable(configKey, "PROCESS_VALUE");

            // TaskEnvironment has a different value
            var taskEnv = TaskEnvironmentHelper.CreateForTest(dir1);
            taskEnv.SetEnvironmentVariable(configKey, "TASK_VALUE");

            var task = new SdkTasks.Configuration.ConfigurationValidator
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                ConfigKey = configKey,
                FallbackValue = "fallback",
            };

            Assert.True(task.Execute());
            Assert.Contains("TASK_VALUE", task.ResolvedConfig);
        }

        [Fact]
        public void ShouldResolveConfigPathRelativeToProjectDirectory()
        {
            var dir1 = CreateProjectDir();
            var configKey = "TEST_PATH_" + Guid.NewGuid().ToString("N")[..8];
            _envVarsToClean.Add(configKey);

            // Create a directory matching the config value so ResolveConfigPath finds it
            string configDir = Path.Combine(dir1, "myconfig");
            Directory.CreateDirectory(configDir);

            var engine = new MockBuildEngine();
            var taskEnv = TaskEnvironmentHelper.CreateForTest(dir1);
            taskEnv.SetEnvironmentVariable(configKey, "myconfig");

            var task = new SdkTasks.Configuration.ConfigurationValidator
            {
                BuildEngine = engine,
                TaskEnvironment = taskEnv,
                ConfigKey = configKey,
                FallbackValue = "fallback",
            };

            Assert.True(task.Execute());
            Assert.Contains("myconfig", task.ResolvedConfig);

            // Verify the log shows the path was resolved relative to ProjectDirectory, not CWD
            Assert.Contains(engine.Messages,
                m => m.Message != null && m.Message.Contains(dir1) && m.Message.Contains("myconfig"));
        }

        [Fact]
        public void TwoTasks_ShouldResolveConfigPathToOwnProjectDirectory()
        {
            var dir1 = CreateProjectDir();
            var dir2 = CreateProjectDir();
            var configKey = "TEST_DUAL_PATH_" + Guid.NewGuid().ToString("N")[..8];
            _envVarsToClean.Add(configKey);

            // Create matching directories in both project dirs
            Directory.CreateDirectory(Path.Combine(dir1, "cfg"));
            Directory.CreateDirectory(Path.Combine(dir2, "cfg"));

            var engine1 = new MockBuildEngine();
            var taskEnv1 = TaskEnvironmentHelper.CreateForTest(dir1);
            taskEnv1.SetEnvironmentVariable(configKey, "cfg");

            var task1 = new SdkTasks.Configuration.ConfigurationValidator
            {
                BuildEngine = engine1,
                TaskEnvironment = taskEnv1,
                ConfigKey = configKey,
                FallbackValue = "fallback",
            };

            var engine2 = new MockBuildEngine();
            var taskEnv2 = TaskEnvironmentHelper.CreateForTest(dir2);
            taskEnv2.SetEnvironmentVariable(configKey, "cfg");

            var task2 = new SdkTasks.Configuration.ConfigurationValidator
            {
                BuildEngine = engine2,
                TaskEnvironment = taskEnv2,
                ConfigKey = configKey,
                FallbackValue = "fallback",
            };

            Assert.True(task1.Execute());
            Assert.True(task2.Execute());

            // Each task should resolve the config path relative to its own ProjectDirectory
            Assert.Contains(engine1.Messages,
                m => m.Message != null && m.Message.Contains(dir1));
            Assert.Contains(engine2.Messages,
                m => m.Message != null && m.Message.Contains(dir2));
        }
    }
}
