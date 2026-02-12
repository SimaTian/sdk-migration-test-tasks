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
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Build.AssemblyCacheResolver();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Build.AssemblyCacheResolver),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldResolveToProjectDirectory()
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
    }
}
