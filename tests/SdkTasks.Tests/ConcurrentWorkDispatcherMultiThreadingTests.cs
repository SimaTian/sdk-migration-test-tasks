using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Build;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class ConcurrentWorkDispatcherMultiThreadingTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public ConcurrentWorkDispatcherMultiThreadingTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ItImplementsIMultiThreadableTask()
        {
            var task = new ConcurrentWorkDispatcher();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void ItHasMSBuildMultiThreadableTaskAttribute()
        {
            Assert.NotEmpty(
                typeof(ConcurrentWorkDispatcher)
                    .GetCustomAttributes(typeof(MSBuildMultiThreadableTaskAttribute), false));
        }

        [Fact]
        public void ItResolvesRelativeConfigPathViaTaskEnvironment()
        {
            var configRelPath = Path.Combine("configs", "app.json");
            var configAbsPath = Path.Combine(_projectDir, configRelPath);
            Directory.CreateDirectory(Path.GetDirectoryName(configAbsPath)!);
            File.WriteAllText(configAbsPath, "{}");

            var task = new ConcurrentWorkDispatcher
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                WorkItems = new ITaskItem[]
                {
                    new TaskItem("item1", new Dictionary<string, string>
                    {
                        { "Category", "Build" },
                        { "ConfigPath", configRelPath }
                    })
                }
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.Single(task.CompletedItems);

            var completed = task.CompletedItems[0];
            Assert.Equal(configAbsPath, completed.GetMetadata("ResolvedPath"));
            Assert.Equal("True", completed.GetMetadata("Exists"));
            Assert.Equal("Build", completed.GetMetadata("Category"));
        }

        [Fact]
        public void ItResolvesDotnetRootViaTaskEnvironment()
        {
            var fakeDotnetRoot = Path.Combine(Path.GetTempPath(), "dotnet-root-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(fakeDotnetRoot);
            try
            {
                var sdkPath = Path.Combine(fakeDotnetRoot, "sdk", "mytool");
                Directory.CreateDirectory(Path.GetDirectoryName(sdkPath)!);
                File.WriteAllText(sdkPath, "dummy");

                var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
                taskEnv.SetEnvironmentVariable("DOTNET_ROOT", fakeDotnetRoot);

                var task = new ConcurrentWorkDispatcher
                {
                    BuildEngine = _engine,
                    TaskEnvironment = taskEnv,
                    WorkItems = new ITaskItem[]
                    {
                        new TaskItem("mytool", new Dictionary<string, string>
                        {
                            { "Category", "ConfigPath" }
                        })
                    }
                };

                var result = task.Execute();

                Assert.True(result);
                Assert.Single(task.CompletedItems);
                Assert.Equal(sdkPath, task.CompletedItems[0].GetMetadata("ResolvedPath"));
                Assert.Equal("True", task.CompletedItems[0].GetMetadata("Exists"));
            }
            finally
            {
                TestHelper.CleanupTempDirectory(fakeDotnetRoot);
            }
        }

        [Fact]
        public void ItResolvesIdentityRelativeToProjectDirectory()
        {
            var task = new ConcurrentWorkDispatcher
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                WorkItems = new ITaskItem[]
                {
                    new TaskItem("subdir/file.txt", new Dictionary<string, string>
                    {
                        { "Category", "Other" }
                    })
                }
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.Single(task.CompletedItems);

            var expectedPath = Path.Combine(_projectDir, "subdir/file.txt");
            Assert.Equal(expectedPath, task.CompletedItems[0].GetMetadata("ResolvedPath"));
        }
    }
}
