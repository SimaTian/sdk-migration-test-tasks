using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class BuildEnvironmentConfiguratorTests : IDisposable
    {
        private readonly TaskTestContext _ctx;
        private readonly List<string> _envVarsToClean = new();

        public BuildEnvironmentConfiguratorTests() => _ctx = new TaskTestContext();

        private string CreateTempDir() => _ctx.CreateAdditionalProjectDir();

        public void Dispose()
        {
            _ctx.Dispose();
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
        public void ShouldNotModifyGlobalState()
        {
            var varName = "MSBUILD_SET_TEST_" + Guid.NewGuid().ToString("N")[..8];
            _envVarsToClean.Add(varName);

            var taskEnv = TaskEnvironmentHelper.CreateForTest();

            var task = new SdkTasks.Configuration.BuildEnvironmentConfigurator
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                VariableName = varName,
                VariableValue = "SET_VALUE"
            };

            task.Execute();

            // Assert CORRECT behavior: global env should NOT be modified
            Assert.Null(Environment.GetEnvironmentVariable(varName));
        }

        [Fact]
        public void ShouldStoreValueInTaskEnvironment()
        {
            var varName = "MSBUILD_SET_TEST_" + Guid.NewGuid().ToString("N")[..8];
            _envVarsToClean.Add(varName);

            var taskEnv = TaskEnvironmentHelper.CreateForTest();

            var task = new SdkTasks.Configuration.BuildEnvironmentConfigurator
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                VariableName = varName,
                VariableValue = "TASK_VALUE"
            };

            task.Execute();

            // Assert CORRECT behavior: value should be stored in TaskEnvironment
            Assert.Equal("TASK_VALUE", taskEnv.GetEnvironmentVariable(varName));
        }

        [Fact]
        public void ShouldUseTaskEnvironmentNotGlobalEnvironment()
        {
            // Pre-set a global env var to a known value
            var varName = SetGlobalEnvVar("GLOBAL_VALUE");

            var projectDir = CreateTempDir();
            var taskEnv = TaskEnvironmentHelper.CreateForTest(projectDir);

            var task = new SdkTasks.Configuration.BuildEnvironmentConfigurator
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                VariableName = varName,
                VariableValue = "TASK_OVERRIDE"
            };

            task.Execute();

            // Assert CORRECT behavior: global env retains original value
            Assert.Equal("GLOBAL_VALUE", Environment.GetEnvironmentVariable(varName));
            // Assert CORRECT behavior: TaskEnvironment has the new value
            Assert.Equal("TASK_OVERRIDE", taskEnv.GetEnvironmentVariable(varName));
        }

        [Fact]
        public void ShouldSetVariableInProjectDirectoryContext()
        {
            var projectDir = CreateTempDir();
            var varName = "MSBUILD_SET_TEST_" + Guid.NewGuid().ToString("N")[..8];
            _envVarsToClean.Add(varName);

            // Use TrackingTaskEnvironment to verify TaskEnvironment methods are used
            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(projectDir);

            var task = new SdkTasks.Configuration.BuildEnvironmentConfigurator
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = trackingEnv,
                VariableName = varName,
                VariableValue = "TRACKED_VALUE"
            };

            var result = task.Execute();

            Assert.True(result);
            // Value is accessible through TaskEnvironment, not global state
            Assert.Equal("TRACKED_VALUE", trackingEnv.GetEnvironmentVariable(varName));
            Assert.Null(Environment.GetEnvironmentVariable(varName));
        }

        [Fact]
        public void ShouldHandleNullValue()
        {
            var varName = "MSBUILD_SET_TEST_" + Guid.NewGuid().ToString("N")[..8];
            _envVarsToClean.Add(varName);

            var taskEnv = TaskEnvironmentHelper.CreateForTest();
            // Pre-set a value in TaskEnvironment
            taskEnv.SetEnvironmentVariable(varName, "EXISTING");

            var task = new SdkTasks.Configuration.BuildEnvironmentConfigurator
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                VariableName = varName,
                VariableValue = null
            };

            var result = task.Execute();

            Assert.True(result);
            // Assert CORRECT behavior: null value clears the variable in TaskEnvironment
            Assert.Null(taskEnv.GetEnvironmentVariable(varName));
        }

        [Fact]
        public void ShouldReturnTrue()
        {
            var varName = "MSBUILD_SET_TEST_" + Guid.NewGuid().ToString("N")[..8];
            _envVarsToClean.Add(varName);

            var task = new SdkTasks.Configuration.BuildEnvironmentConfigurator
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                VariableName = varName,
                VariableValue = "VALUE"
            };

            Assert.True(task.Execute());
        }
    }
}
