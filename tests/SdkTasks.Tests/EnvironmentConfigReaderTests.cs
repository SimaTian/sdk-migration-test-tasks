using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class EnvironmentConfigReaderTests : IDisposable
    {
        private readonly List<string> _envVarsToClean = new();

        public void Dispose()
        {
            foreach (var name in _envVarsToClean)
                Environment.SetEnvironmentVariable(name, null);
        }

        private string SetGlobalEnvVar(string value)
        {
            var name = "MSBUILD_TEST_" + Guid.NewGuid().ToString("N")[..8];
            Environment.SetEnvironmentVariable(name, value);
            _envVarsToClean.Add(name);
            return name;
        }

        [Fact]
        public void ShouldReadFromTaskEnvironment()
        {
            var varName = SetGlobalEnvVar("GLOBAL_VALUE");

            var taskEnv = TaskEnvironmentHelper.CreateForTest();
            taskEnv.SetEnvironmentVariable(varName, "TASK_VALUE");

            var task = new SdkTasks.Configuration.EnvironmentConfigReader
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                VariableName = varName
            };

            task.Execute();

            // Task should read from TaskEnvironment, not global env
            Assert.Equal("TASK_VALUE", task.VariableValue);
        }

        [Fact]
        public void ShouldCallGetEnvironmentVariableOnTaskEnvironment()
        {
            var varName = SetGlobalEnvVar("GLOBAL_VALUE");

            var trackingEnv = new TrackingTaskEnvironment();
            trackingEnv.SetEnvironmentVariable(varName, "TRACKED_VALUE");

            var task = new SdkTasks.Configuration.EnvironmentConfigReader
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = trackingEnv,
                VariableName = varName
            };

            task.Execute();

            // Verify TaskEnvironment.GetEnvironmentVariable was called
            SharedTestHelpers.AssertUsesGetEnvironmentVariable(trackingEnv);
            Assert.Equal("TRACKED_VALUE", task.VariableValue);
        }

        [Fact]
        public void Execute_ReturnsTrue()
        {
            var varName = SetGlobalEnvVar("SOME_VALUE");

            var taskEnv = TaskEnvironmentHelper.CreateForTest();
            taskEnv.SetEnvironmentVariable(varName, "SOME_VALUE");

            var task = new SdkTasks.Configuration.EnvironmentConfigReader
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                VariableName = varName
            };

            var result = task.Execute();

            Assert.True(result);
        }

        [Fact]
        public void ShouldReturnNullForUnsetVariable()
        {
            var varName = "MSBUILD_UNSET_" + Guid.NewGuid().ToString("N")[..8];

            var taskEnv = TaskEnvironmentHelper.CreateForTest();

            var task = new SdkTasks.Configuration.EnvironmentConfigReader
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                VariableName = varName
            };

            task.Execute();

            Assert.Null(task.VariableValue);
        }

        [Fact]
        public void ShouldPreferTaskEnvironmentOverGlobalEnv()
        {
            var varName = SetGlobalEnvVar("GLOBAL_VALUE");

            // Set a different value in TaskEnvironment
            var trackingEnv = new TrackingTaskEnvironment();
            trackingEnv.SetEnvironmentVariable(varName, "TASK_SCOPED_VALUE");

            var task = new SdkTasks.Configuration.EnvironmentConfigReader
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = trackingEnv,
                VariableName = varName
            };

            task.Execute();

            // Must read the task-scoped value, proving it goes through TaskEnvironment
            Assert.Equal("TASK_SCOPED_VALUE", task.VariableValue);
            Assert.Equal(1, trackingEnv.GetEnvironmentVariableCallCount);
        }

        [Fact]
        public void ShouldFallBackToProcessEnvWhenNotInTaskEnvironment()
        {
            var varName = SetGlobalEnvVar("FALLBACK_VALUE");

            // Do NOT set in TaskEnvironment — should fall back to process env via TaskEnvironment
            var trackingEnv = new TrackingTaskEnvironment();

            var task = new SdkTasks.Configuration.EnvironmentConfigReader
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = trackingEnv,
                VariableName = varName
            };

            task.Execute();

            // Still routes through TaskEnvironment, which falls back to process env
            SharedTestHelpers.AssertUsesGetEnvironmentVariable(trackingEnv);
            Assert.Equal("FALLBACK_VALUE", task.VariableValue);
        }
    }
}
