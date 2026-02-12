using System.Reflection;
using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests.Build
{
    public class GivenAPathResolutionCacheMultiThreading : IDisposable
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
        public void ItResolvesRelativePathsViaTaskEnvironment()
        {
            var projectDir = CreateProjectDir();

            var task = new SdkTasks.Build.PathResolutionCache();
            task.BuildEngine = new MockBuildEngine();
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);
            task.InputPaths = new[] { "subdir/file.txt" };

            var result = task.Execute();

            Assert.True(result);
            Assert.Single(task.ResolvedPaths);
            Assert.StartsWith(projectDir, task.ResolvedPaths[0].ItemSpec, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ItSetsCorrectMetadataOnResolvedPaths()
        {
            var projectDir = CreateProjectDir();

            var task = new SdkTasks.Build.PathResolutionCache();
            task.BuildEngine = new MockBuildEngine();
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);
            task.InputPaths = new[] { "src/app.dll" };

            task.Execute();

            var item = task.ResolvedPaths[0];
            Assert.Equal("src/app.dll", item.GetMetadata("OriginalPath"));
            Assert.Equal(".dll", item.GetMetadata("ResolvedExtension"));
            Assert.StartsWith(projectDir, item.GetMetadata("ResolvedDirectory"), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ItReturnsEmptyOutputForEmptyInput()
        {
            var task = new SdkTasks.Build.PathResolutionCache();
            task.BuildEngine = new MockBuildEngine();
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest();
            task.InputPaths = Array.Empty<string>();

            var result = task.Execute();

            Assert.True(result);
            Assert.Empty(task.ResolvedPaths);
        }

        [Fact]
        public void ItSkipsBlankInputPathsWithWarning()
        {
            var projectDir = CreateProjectDir();

            var engine = new MockBuildEngine();
            var task = new SdkTasks.Build.PathResolutionCache();
            task.BuildEngine = engine;
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);
            task.InputPaths = new[] { "valid/path.txt", "  ", "another/path.txt" };

            task.Execute();

            Assert.Equal(2, task.ResolvedPaths.Length);
            Assert.Contains(engine.Warnings, w => w.Message != null && w.Message.Contains("empty input path", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ItPreservesPublicApiSurface()
        {
            var actualProperties = typeof(SdkTasks.Build.PathResolutionCache)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name)
                .ToList();

            Assert.Contains("TaskEnvironment", actualProperties);
            Assert.Contains("InputPaths", actualProperties);
            Assert.Contains("ResolvedPaths", actualProperties);
        }
    }
}
