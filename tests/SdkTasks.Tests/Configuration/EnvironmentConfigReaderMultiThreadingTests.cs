using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;
using SdkTasks.Configuration;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests.Configuration
{
    public class EnvironmentConfigReaderMultiThreadingTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;
        private readonly List<string> _envVarsToClean = new();

        public EnvironmentConfigReaderMultiThreadingTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose()
        {
            TestHelper.CleanupTempDirectory(_projectDir);
            foreach (var name in _envVarsToClean)
                Environment.SetEnvironmentVariable(name, null);
        }

        [Fact]
        public void ItReadsFromTaskEnvironmentNotProcessEnvironment()
        {
            var varName = "MSBUILD_ECR_TEST_" + Guid.NewGuid().ToString("N")[..8];
            Environment.SetEnvironmentVariable(varName, "PROCESS_VALUE");
            _envVarsToClean.Add(varName);

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            taskEnv.SetEnvironmentVariable(varName, "TASK_VALUE");

            var task = new EnvironmentConfigReader
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                VariableName = varName
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.Equal("TASK_VALUE", task.VariableValue);
        }

        [Fact]
        public void ItReturnsNullForUnsetVariable()
        {
            var varName = "MSBUILD_ECR_NONEXISTENT_" + Guid.NewGuid().ToString("N")[..8];

            var task = new EnvironmentConfigReader
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                VariableName = varName
            };

            task.Execute();

            Assert.Null(task.VariableValue);
        }

        [Fact]
        public void ItPreservesAllPublicProperties()
        {
            var declaredProps = typeof(EnvironmentConfigReader)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(p => p.Name)
                .OrderBy(n => n)
                .ToArray();

            var expected = new[] { "TaskEnvironment", "VariableName", "VariableValue" }
                .OrderBy(n => n)
                .ToArray();

            Assert.Equal(expected, declaredProps);
        }

        [Fact]
        public void ItReturnsEmptyStringForEmptyVariable()
        {
            var varName = "MSBUILD_ECR_EMPTY_" + Guid.NewGuid().ToString("N")[..8];

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            taskEnv.SetEnvironmentVariable(varName, "");

            var task = new EnvironmentConfigReader
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                VariableName = varName
            };

            task.Execute();

            Assert.Equal("", task.VariableValue);
        }

        [Fact]
        public void ItAlwaysReturnsTrue()
        {
            var varName = "MSBUILD_ECR_MISSING_" + Guid.NewGuid().ToString("N")[..8];

            var task = new EnvironmentConfigReader
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                VariableName = varName
            };

            var result = task.Execute();

            Assert.True(result);
        }
    }
}
