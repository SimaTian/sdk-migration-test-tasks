using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class AssemblyCacheResolverTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public AssemblyCacheResolverTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldResolveAssemblyUnderProjectDirectory()
        {
            string libDir = Path.Combine(_projectDir, "lib", "net8.0");
            Directory.CreateDirectory(libDir);
            File.WriteAllText(Path.Combine(libDir, "MyAssembly.dll"), "fake-dll");

            var task = new SdkTasks.Build.AssemblyCacheResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                AssemblyReferences = new ITaskItem[] { new TaskItem("MyAssembly") }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.NotEmpty(task.ResolvedReferences);
            string resolvedPath = task.ResolvedReferences[0].ItemSpec;
            Assert.StartsWith(_projectDir, resolvedPath, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldUseTaskEnvironmentGetAbsolutePathForProbePaths()
        {
            string libDir = Path.Combine(_projectDir, "lib", "net8.0");
            Directory.CreateDirectory(libDir);
            File.WriteAllText(Path.Combine(libDir, "TrackedAssembly.dll"), "fake-dll");

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Build.AssemblyCacheResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                AssemblyReferences = new ITaskItem[] { new TaskItem("TrackedAssembly") }
            };

            task.Execute();

            SharedTestHelpers.AssertGetAbsolutePathCalled(trackingEnv);
            Assert.Contains(trackingEnv.GetAbsolutePathArgs,
                arg => arg.Contains("bin") || arg.Contains("lib") || arg.Contains("obj"));
        }

        [Fact]
        public void ShouldResolveFromBinDirectory()
        {
            string binDir = Path.Combine(_projectDir, "bin");
            Directory.CreateDirectory(binDir);
            File.WriteAllText(Path.Combine(binDir, "BinAssembly.dll"), "fake-dll");

            var task = new SdkTasks.Build.AssemblyCacheResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                AssemblyReferences = new ITaskItem[] { new TaskItem("BinAssembly") }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.NotEmpty(task.ResolvedReferences);
            string resolvedPath = task.ResolvedReferences[0].ItemSpec;
            Assert.Contains("bin", resolvedPath, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(_projectDir, resolvedPath, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ResolvedPathsShouldNotContainProcessCwd()
        {
            string libDir = Path.Combine(_projectDir, "lib", "net8.0");
            Directory.CreateDirectory(libDir);
            File.WriteAllText(Path.Combine(libDir, "CwdCheckAssembly.dll"), "fake-dll");

            var task = new SdkTasks.Build.AssemblyCacheResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                AssemblyReferences = new ITaskItem[] { new TaskItem("CwdCheckAssembly") }
            };

            task.Execute();

            string cwd = Directory.GetCurrentDirectory();
            Assert.NotEmpty(task.ResolvedReferences);
            foreach (var item in task.ResolvedReferences)
            {
                if (!_projectDir.StartsWith(cwd, StringComparison.OrdinalIgnoreCase))
                    Assert.DoesNotContain(cwd, item.ItemSpec, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
