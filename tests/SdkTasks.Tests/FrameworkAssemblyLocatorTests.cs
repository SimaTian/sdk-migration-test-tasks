using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class FrameworkAssemblyLocatorTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public FrameworkAssemblyLocatorTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Build.FrameworkAssemblyLocator();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Build.FrameworkAssemblyLocator),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldUseTaskEnvironmentForRuntimePackResolution()
        {
            string binDir = Path.Combine(_projectDir, "bin");
            Directory.CreateDirectory(binDir);
            File.WriteAllText(Path.Combine(binDir, "TestLib.dll"), "fake");

            var taskEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            taskEnv.SetEnvironmentVariable("DOTNET_ROOT", _projectDir);

            var task = new SdkTasks.Build.FrameworkAssemblyLocator
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                References = new ITaskItem[] { new TaskItem("TestLib") },
                TargetFramework = "net8.0",
                RuntimeIdentifier = "win-x64"
            };

            task.Execute();

            Assert.NotEmpty(task.ResolvedReferences);
            bool runtimePackResolvedViaTaskEnv = taskEnv.GetAbsolutePathArgs.Any(arg =>
                arg.Contains("packs") && arg.Contains("Microsoft.NETCore.App.Runtime"));
            Assert.True(runtimePackResolvedViaTaskEnv,
                "Task should use TaskEnvironment.GetAbsolutePath() for runtime pack path resolution");
        }
    }
}
