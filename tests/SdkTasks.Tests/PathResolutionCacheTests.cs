using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class PathResolutionCacheTests : IDisposable
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
            var task = new SdkTasks.Build.PathResolutionCache();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Build.PathResolutionCache),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldResolveToOwnProjectDirectory()
        {
            var dir1 = CreateProjectDir();
            var dir2 = CreateProjectDir();

            var engine = new MockBuildEngine();

            var task1 = new SdkTasks.Build.PathResolutionCache
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir1),
                InputPaths = new[] { "obj\\output.json" },
            };

            var task2 = new SdkTasks.Build.PathResolutionCache
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir2),
                InputPaths = new[] { "obj\\output.json" },
            };

            Assert.True(task1.Execute());
            Assert.True(task2.Execute());

            var resolved1 = task1.ResolvedPaths[0].ItemSpec;
            var resolved2 = task2.ResolvedPaths[0].ItemSpec;

            Assert.StartsWith(dir1, resolved1, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(dir2, resolved2, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(resolved1, resolved2);
        }
    }
}
