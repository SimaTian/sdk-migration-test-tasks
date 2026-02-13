using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;
using System.IO;

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
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Configuration.EnvironmentConfigReader();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Configuration.EnvironmentConfigReader),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
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
        public void Execute_ShouldInitializeProjectDirectory_FromBuildEngine()
        {
            // Arrange
            var task = new SdkTasks.Configuration.EnvironmentConfigReader
            {
                BuildEngine = new MockBuildEngine(), // Returns "test.csproj"
                TaskEnvironment = new TaskEnvironment(), // ProjectDirectory is Empty
                VariableName = "TEST_VAR"
            };

            // Act
            task.Execute();

            // Assert
            Assert.False(string.IsNullOrEmpty(task.TaskEnvironment.ProjectDirectory), 
                "TaskEnvironment.ProjectDirectory should be initialized from BuildEngine.ProjectFileOfTaskNode");
            
            // "test.csproj" resolves to CWD/test.csproj, so ProjectDirectory should be CWD
            var expectedDir = Directory.GetCurrentDirectory();
            Assert.Equal(expectedDir, task.TaskEnvironment.ProjectDirectory);
        }
    }
}
