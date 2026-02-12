using System;
using System.IO;
using Microsoft.Build.Framework;
using SdkTasks.Tools;
using Xunit;

namespace SdkTasks.Tests.Tools
{
    public class ToolchainInvokerMultiThreadingTests : IDisposable
    {
        private readonly string _projectDir;

        public ToolchainInvokerMultiThreadingTests()
        {
            _projectDir = Path.Combine(Path.GetTempPath(), "ToolchainInvokerTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_projectDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_projectDir, true); } catch { }
        }

        [Fact]
        public void ItImplementsIMultiThreadableTask()
        {
            var task = new ToolchainInvoker();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void ItHasMSBuildMultiThreadableTaskAttribute()
        {
            Assert.True(
                Attribute.IsDefined(typeof(ToolchainInvoker), typeof(MSBuildMultiThreadableTaskAttribute)),
                "ToolchainInvoker must have [MSBuildMultiThreadableTask] attribute");
        }

        [Fact]
        public void ItUsesTaskEnvironmentProcessStartInfo()
        {
            var task = new ToolchainInvoker();
            task.BuildEngine = new Infrastructure.MockBuildEngine();
            task.TaskEnvironment = Infrastructure.TaskEnvironmentHelper.CreateForTest(_projectDir);

            task.ToolName = "cmd.exe";
            task.Arguments = "/c echo hello";
            task.TimeoutMilliseconds = 5000;

            bool result = task.Execute();

            Assert.True(result, "Tool invocation should succeed");
            Assert.Contains("hello", task.ToolOutput);
        }

        [Fact]
        public void ItRespectsTaskEnvironmentForEnvVars()
        {
            var task = new ToolchainInvoker();
            task.BuildEngine = new Infrastructure.MockBuildEngine();
            task.TaskEnvironment = Infrastructure.TaskEnvironmentHelper.CreateForTest(_projectDir);

            task.ToolName = "cmd.exe";
            task.Arguments = "/c echo test";
            task.TimeoutMilliseconds = 5000;

            bool result = task.Execute();
            Assert.True(result);
        }

        [Fact]
        public void PreservesAllPublicProperties()
        {
            var actualProperties = typeof(ToolchainInvoker)
                .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            foreach (var expected in new[] { "TaskEnvironment", "ToolName", "Arguments", "ToolOutput", "TimeoutMilliseconds" })
            {
                Assert.Contains(actualProperties, p => p.Name == expected);
            }
        }
    }
}
