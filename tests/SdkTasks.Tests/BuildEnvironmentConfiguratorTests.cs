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

        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Configuration.BuildEnvironmentConfigurator();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Configuration.BuildEnvironmentConfigurator),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
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

            Assert.Null(Environment.GetEnvironmentVariable(varName));
            Assert.Equal("SET_VALUE", taskEnv.GetEnvironmentVariable(varName));
        }
    }
}
