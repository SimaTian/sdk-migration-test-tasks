using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class BuildEnvironmentConfiguratorTests : IDisposable
    {
        private readonly List<string> _envVarsToClean = new();

        public void Dispose()
        {
            foreach (var name in _envVarsToClean)
                Environment.SetEnvironmentVariable(name, null);
        }

        private string UniqueVarName(string prefix = "MSBUILD_SET_TEST_")
        {
            var name = prefix + Guid.NewGuid().ToString("N")[..8];
            _envVarsToClean.Add(name);
            return name;
        }

        [Fact]
        public void Execute_ShouldNotModifyGlobalEnvironment()
        {
            var varName = UniqueVarName();
            var taskEnv = TaskEnvironmentHelper.CreateForTest();

            var task = new SdkTasks.Configuration.BuildEnvironmentConfigurator
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                VariableName = varName,
                VariableValue = "TASK_SCOPED_VALUE"
            };

            var result = task.Execute();

            Assert.True(result);
            // Global environment must NOT be modified
            Assert.Null(Environment.GetEnvironmentVariable(varName));
        }

        [Fact]
        public void Execute_ShouldStoreValueInTaskEnvironment()
        {
            var varName = UniqueVarName();
            var taskEnv = TaskEnvironmentHelper.CreateForTest();

            var task = new SdkTasks.Configuration.BuildEnvironmentConfigurator
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                VariableName = varName,
                VariableValue = "STORED_VALUE"
            };

            task.Execute();

            // Value should be readable back via TaskEnvironment
            Assert.Equal("STORED_VALUE", taskEnv.GetEnvironmentVariable(varName));
        }

        // =====================================================================
        // TrackingTaskEnvironment: verify task routes through TaskEnvironment
        // =====================================================================

        [Fact]
        public void Execute_WithTrackingEnv_ShouldStoreWithoutGlobalSideEffects()
        {
            var varName = UniqueVarName();
            var trackingEnv = new TrackingTaskEnvironment
            {
                ProjectDirectory = TestHelper.CreateNonCwdTempDirectory()
            };

            try
            {
                var task = new SdkTasks.Configuration.BuildEnvironmentConfigurator
                {
                    BuildEngine = new MockBuildEngine(),
                    TaskEnvironment = trackingEnv,
                    VariableName = varName,
                    VariableValue = "TRACKED_VALUE"
                };

                task.Execute();

                // Value stored in TaskEnvironment, readable via GetEnvironmentVariable
                Assert.Equal("TRACKED_VALUE", trackingEnv.GetEnvironmentVariable(varName));
                // Global env must remain unmodified
                Assert.Null(Environment.GetEnvironmentVariable(varName));
            }
            finally
            {
                TestHelper.CleanupTempDirectory(trackingEnv.ProjectDirectory);
            }
        }

        [Fact]
        public void Execute_WithTrackingEnv_ReadBackUsesTaskEnvironmentGetEnvironmentVariable()
        {
            var varName = UniqueVarName();
            var trackingEnv = new TrackingTaskEnvironment
            {
                ProjectDirectory = TestHelper.CreateNonCwdTempDirectory()
            };

            try
            {
                var task = new SdkTasks.Configuration.BuildEnvironmentConfigurator
                {
                    BuildEngine = new MockBuildEngine(),
                    TaskEnvironment = trackingEnv,
                    VariableName = varName,
                    VariableValue = "ENV_VALUE"
                };

                task.Execute();

                // Reading back the value through TrackingTaskEnvironment increments the counter
                var readBack = trackingEnv.GetEnvironmentVariable(varName);
                Assert.Equal("ENV_VALUE", readBack);
                Assert.True(trackingEnv.GetEnvironmentVariableCallCount >= 1,
                    "Expected GetEnvironmentVariable to be callable on the same TaskEnvironment instance");
            }
            finally
            {
                TestHelper.CleanupTempDirectory(trackingEnv.ProjectDirectory);
            }
        }

        // =====================================================================
        // Isolation: separate TaskEnvironment instances don't share state
        // =====================================================================

        [Fact]
        public void Execute_SeparateTaskEnvironments_ShouldBeIsolated()
        {
            var varName = UniqueVarName();
            var taskEnv1 = TaskEnvironmentHelper.CreateForTest();
            var taskEnv2 = TaskEnvironmentHelper.CreateForTest();

            var task1 = new SdkTasks.Configuration.BuildEnvironmentConfigurator
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv1,
                VariableName = varName,
                VariableValue = "VALUE_FROM_TASK1"
            };

            var task2 = new SdkTasks.Configuration.BuildEnvironmentConfigurator
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv2,
                VariableName = varName,
                VariableValue = "VALUE_FROM_TASK2"
            };

            task1.Execute();
            task2.Execute();

            // Each TaskEnvironment should have its own value
            Assert.Equal("VALUE_FROM_TASK1", taskEnv1.GetEnvironmentVariable(varName));
            Assert.Equal("VALUE_FROM_TASK2", taskEnv2.GetEnvironmentVariable(varName));
            // Global env must remain unmodified
            Assert.Null(Environment.GetEnvironmentVariable(varName));
        }

        [Fact]
        public void Execute_ShouldNotAffectPreExistingGlobalEnvVar()
        {
            var varName = UniqueVarName();
            Environment.SetEnvironmentVariable(varName, "ORIGINAL_GLOBAL");

            var taskEnv = TaskEnvironmentHelper.CreateForTest();

            var task = new SdkTasks.Configuration.BuildEnvironmentConfigurator
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                VariableName = varName,
                VariableValue = "TASK_OVERRIDE"
            };

            task.Execute();

            // TaskEnvironment should have the task-scoped value
            Assert.Equal("TASK_OVERRIDE", taskEnv.GetEnvironmentVariable(varName));
            // Global env should still have the original value, not overwritten
            Assert.Equal("ORIGINAL_GLOBAL", Environment.GetEnvironmentVariable(varName));
        }
    }
}
