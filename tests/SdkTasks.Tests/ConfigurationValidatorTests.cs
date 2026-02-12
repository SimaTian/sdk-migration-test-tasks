using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class ConfigurationValidatorTests : IDisposable
    {
        private readonly TaskTestContext _ctx;
        private readonly List<string> _envVarsToClean = new();

        public ConfigurationValidatorTests() => _ctx = new TaskTestContext();

        private string CreateProjectDir() => _ctx.CreateAdditionalProjectDir();

        public void Dispose()
        {
            _ctx.Dispose();
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
        public void ShouldCallGetEnvironmentVariable_ViaTaskEnvironment()
        {
            var dir = CreateProjectDir();
            var configKey = "TEST_CONFIG_" + Guid.NewGuid().ToString("N")[..8];
            _envVarsToClean.Add(configKey);

            var tracking = SharedTestHelpers.CreateTrackingEnvironment(dir);
            tracking.SetEnvironmentVariable(configKey, "trackedValue");

            var task = new SdkTasks.Configuration.ConfigurationValidator
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = tracking,
                ConfigKey = configKey,
                FallbackValue = "fallback",
            };

            Assert.True(task.Execute());

            // Task must read env vars through TaskEnvironment, not System.Environment
            SharedTestHelpers.AssertMinimumGetEnvironmentVariableCalls(tracking, 1);
            Assert.Contains("trackedValue", task.ResolvedConfig);
        }

        [Fact]
        public void ShouldResolveConfigPath_RelativeToProjectDirectory()
        {
            var dir = CreateProjectDir();
            var configKey = "TEST_CONFIG_" + Guid.NewGuid().ToString("N")[..8];
            _envVarsToClean.Add(configKey);

            // Create a subdirectory so ResolveConfigPath finds an existing directory
            var subDir = Path.Combine(dir, "myconfig");
            Directory.CreateDirectory(subDir);

            var taskEnv = TaskEnvironmentHelper.CreateForTest(dir);
            taskEnv.SetEnvironmentVariable(configKey, "myconfig");

            var engine = new MockBuildEngine();
            var task = new SdkTasks.Configuration.ConfigurationValidator
            {
                BuildEngine = engine,
                TaskEnvironment = taskEnv,
                ConfigKey = configKey,
                FallbackValue = "fallback",
            };

            Assert.True(task.Execute());

            // The resolved config should reference the project directory, not the process CWD
            var hasDirReference = engine.Messages.Exists(m =>
                m.Message != null && m.Message.Contains(dir, StringComparison.OrdinalIgnoreCase));
            Assert.True(hasDirReference,
                "Config path should resolve relative to ProjectDirectory, not process CWD.");
        }

        [Fact]
        public void ShouldResolveToOwnProjectDir_WhenMultipleTasksRunConcurrently()
        {
            var dir1 = CreateProjectDir();
            var dir2 = CreateProjectDir();
            var configKey = "TEST_CONFIG_" + Guid.NewGuid().ToString("N")[..8];
            _envVarsToClean.Add(configKey);

            // Create matching subdirectories in both project dirs
            Directory.CreateDirectory(Path.Combine(dir1, "cfg"));
            Directory.CreateDirectory(Path.Combine(dir2, "cfg"));

            var taskEnv1 = TaskEnvironmentHelper.CreateForTest(dir1);
            taskEnv1.SetEnvironmentVariable(configKey, "cfg");

            var taskEnv2 = TaskEnvironmentHelper.CreateForTest(dir2);
            taskEnv2.SetEnvironmentVariable(configKey, "cfg");

            var engine1 = new MockBuildEngine();
            var engine2 = new MockBuildEngine();

            var task1 = new SdkTasks.Configuration.ConfigurationValidator
            {
                BuildEngine = engine1,
                TaskEnvironment = taskEnv1,
                ConfigKey = configKey,
                FallbackValue = "fallback",
            };

            var task2 = new SdkTasks.Configuration.ConfigurationValidator
            {
                BuildEngine = engine2,
                TaskEnvironment = taskEnv2,
                ConfigKey = configKey,
                FallbackValue = "fallback",
            };

            Assert.True(task1.Execute());
            Assert.True(task2.Execute());

            // Each task should resolve paths against its own ProjectDirectory
            var task1ReferencesDir1 = engine1.Messages.Exists(m =>
                m.Message != null && m.Message.Contains(dir1, StringComparison.OrdinalIgnoreCase));
            var task2ReferencesDir2 = engine2.Messages.Exists(m =>
                m.Message != null && m.Message.Contains(dir2, StringComparison.OrdinalIgnoreCase));

            Assert.True(task1ReferencesDir1,
                "Task1 should resolve config path relative to its own ProjectDirectory.");
            Assert.True(task2ReferencesDir2,
                "Task2 should resolve config path relative to its own ProjectDirectory.");
        }

        [Fact]
        public void ShouldUseFallback_WhenEnvVarNotSet()
        {
            var dir = CreateProjectDir();
            var configKey = "TEST_CONFIG_MISSING_" + Guid.NewGuid().ToString("N")[..8];
            _envVarsToClean.Add(configKey);

            var taskEnv = TaskEnvironmentHelper.CreateForTest(dir);
            // Deliberately do NOT set the env var

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
        public void ShouldNotResolvePathsRelativeToProcessCwd()
        {
            var dir = CreateProjectDir();
            var configKey = "TEST_CONFIG_" + Guid.NewGuid().ToString("N")[..8];
            _envVarsToClean.Add(configKey);

            // Create a subdirectory only in the project dir, not in process CWD
            Directory.CreateDirectory(Path.Combine(dir, "unique_subdir"));

            var tracking = SharedTestHelpers.CreateTrackingEnvironment(dir);
            tracking.SetEnvironmentVariable(configKey, "unique_subdir");

            var engine = new MockBuildEngine();
            var task = new SdkTasks.Configuration.ConfigurationValidator
            {
                BuildEngine = engine,
                TaskEnvironment = tracking,
                ConfigKey = configKey,
                FallbackValue = "fallback",
            };

            Assert.True(task.Execute());

            // Verify the resolved path references ProjectDirectory, not process CWD
            var processCwd = Environment.CurrentDirectory;
            var referencesProjectDir = engine.Messages.Exists(m =>
                m.Message != null && m.Message.Contains(dir, StringComparison.OrdinalIgnoreCase));
            Assert.True(referencesProjectDir,
                $"Task should resolve paths relative to ProjectDirectory '{dir}', not CWD '{processCwd}'.");
        }
    }
}
