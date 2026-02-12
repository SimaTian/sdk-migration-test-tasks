using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class ToolchainInvokerTests : IDisposable
    {
        private readonly List<string> _tempDirs = new();

        private string CreateProjectDir()
        {
            var dir = TestHelper.CreateNonCwdTempDirectory();
            _tempDirs.Add(dir);
            return dir;
        }

        public void Dispose()
        {
            foreach (var dir in _tempDirs)
                TestHelper.CleanupTempDirectory(dir);
        }

        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Tools.ToolchainInvoker();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Tools.ToolchainInvoker),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldRunInProjectDirectory()
        {
            var dir1 = CreateProjectDir();
            var dir2 = CreateProjectDir();

            var task1 = new SdkTasks.Tools.ToolchainInvoker
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir1),
                ToolName = "cmd.exe",
                Arguments = "/c cd",
                TimeoutMilliseconds = 5000,
            };

            var task2 = new SdkTasks.Tools.ToolchainInvoker
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir2),
                ToolName = "cmd.exe",
                Arguments = "/c cd",
                TimeoutMilliseconds = 5000,
            };

            Assert.True(task1.Execute());
            Assert.True(task2.Execute());

            Assert.Contains(dir1, task1.ToolOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(dir2, task2.ToolOutput, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(task1.ToolOutput.Trim(), task2.ToolOutput.Trim());
        }
    }
}
