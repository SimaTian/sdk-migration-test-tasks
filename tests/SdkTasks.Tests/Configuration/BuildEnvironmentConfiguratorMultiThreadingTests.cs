using System;
using Microsoft.Build.Framework;
using SdkTasks.Configuration;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests.Configuration
{
    public class BuildEnvironmentConfiguratorMultiThreadingTests
    {
        [Fact]
        public void ItSetsEnvironmentVariableViaTaskEnvironment()
        {
            // Arrange
            var tracking = new TrackingTaskEnvironment();
            var task = new BuildEnvironmentConfigurator
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = tracking,
                VariableName = "MY_BUILD_VAR",
                VariableValue = "my_value"
            };

            // Act
            var result = task.Execute();

            // Assert
            Assert.True(result);
            Assert.Equal("my_value", tracking.GetEnvironmentVariable("MY_BUILD_VAR"));
        }

        [Fact]
        public void ItSetsEnvironmentVariableToNullWhenValueIsNull()
        {
            // Arrange
            var tracking = new TrackingTaskEnvironment();
            tracking.SetEnvironmentVariable("EXISTING_VAR", "old_value");
            var task = new BuildEnvironmentConfigurator
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = tracking,
                VariableName = "EXISTING_VAR",
                VariableValue = null
            };

            // Act
            var result = task.Execute();

            // Assert
            Assert.True(result);
            Assert.Null(tracking.GetEnvironmentVariable("EXISTING_VAR"));
        }

        [Fact]
        public void ItDoesNotUseEnvironmentDirectly()
        {
            // Verify that setting a variable via the task does NOT affect
            // the real process environment (proving TaskEnvironment isolation)
            var uniqueName = $"TEST_VAR_{Guid.NewGuid():N}";
            var tracking = new TrackingTaskEnvironment();
            var task = new BuildEnvironmentConfigurator
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = tracking,
                VariableName = uniqueName,
                VariableValue = "should_not_leak"
            };

            task.Execute();

            Assert.Null(Environment.GetEnvironmentVariable(uniqueName));
        }
    }
}
