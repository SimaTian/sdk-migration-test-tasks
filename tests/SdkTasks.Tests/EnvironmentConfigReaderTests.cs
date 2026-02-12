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

        [Fact]
        public void ShouldReadFromTaskEnvironment()
        {
            var varName = "MSBUILD_TEST_" + Guid.NewGuid().ToString("N")[..8];
            Environment.SetEnvironmentVariable(varName, "GLOBAL_VALUE");
            _envVarsToClean.Add(varName);

            var taskEnv = TaskEnvironmentHelper.CreateForTest();
            taskEnv.SetEnvironmentVariable(varName, "TASK_VALUE");

            var task = new SdkTasks.Configuration.EnvironmentConfigReader
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                VariableName = varName
            };

            task.Execute();

            Assert.Equal("TASK_VALUE", task.VariableValue);
        }

        [Fact]
        public void ShouldCallGetEnvironmentVariableOnTaskEnvironment()
        {
            var varName = "MSBUILD_TRACK_" + Guid.NewGuid().ToString("N")[..8];

            var trackingEnv = new TrackingTaskEnvironment();
            trackingEnv.SetEnvironmentVariable(varName, "TRACKED_VALUE");

            var task = new SdkTasks.Configuration.EnvironmentConfigReader
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = trackingEnv,
                VariableName = varName
            };

            task.Execute();

            SharedTestHelpers.AssertGetEnvironmentVariableCalled(trackingEnv);
        }

        [Fact]
        public void ShouldPreferTaskEnvironmentOverGlobalEnv()
        {
            var varName = "MSBUILD_PREF_" + Guid.NewGuid().ToString("N")[..8];
            Environment.SetEnvironmentVariable(varName, "GLOBAL");
            _envVarsToClean.Add(varName);

            var taskEnv = TaskEnvironmentHelper.CreateForTest();
            taskEnv.SetEnvironmentVariable(varName, "TASK_SCOPED");

            var task = new SdkTasks.Configuration.EnvironmentConfigReader
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                VariableName = varName
            };

            task.Execute();

            // Task should read from TaskEnvironment, not global Environment
            Assert.Equal("TASK_SCOPED", task.VariableValue);
        }
    }
}
