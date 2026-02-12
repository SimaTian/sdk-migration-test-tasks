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

        [Fact]
        public void ShouldCallGetCanonicalFormOnTaskEnvironment()
        {
            var projectDir = CreateProjectDir();
            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = projectDir };

            var task = new SdkTasks.Build.PathResolutionCache
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = trackingEnv,
                InputPaths = new[] { "src\\file.cs" },
            };

            Assert.True(task.Execute());

            SharedTestHelpers.AssertGetCanonicalFormCalled(trackingEnv);
            Assert.Contains("src\\file.cs", trackingEnv.GetCanonicalFormArgs);
        }

        [Fact]
        public void ShouldNotResolveRelativeToProcessCwd()
        {
            var projectDir = CreateProjectDir();
            var cwd = Directory.GetCurrentDirectory();

            var task = new SdkTasks.Build.PathResolutionCache
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                InputPaths = new[] { "relative\\path.txt" },
            };

            Assert.True(task.Execute());

            var resolvedPath = task.ResolvedPaths[0].ItemSpec;
            Assert.StartsWith(projectDir, resolvedPath, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(cwd, resolvedPath, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldCacheResolvedPathsAcrossInvocations()
        {
            var projectDir = CreateProjectDir();
            var engine = new MockBuildEngine();

            var task1 = new SdkTasks.Build.PathResolutionCache
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                InputPaths = new[] { "cached-file.txt" },
            };

            var task2 = new SdkTasks.Build.PathResolutionCache
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                InputPaths = new[] { "cached-file.txt" },
            };

            Assert.True(task1.Execute());
            Assert.True(task2.Execute());

            // Both should resolve to the same path under ProjectDirectory
            Assert.Equal(task1.ResolvedPaths[0].ItemSpec, task2.ResolvedPaths[0].ItemSpec);

            // Second invocation should get a cache hit
            Assert.Contains(engine.Messages,
                m => m.Message != null && m.Message.Contains("Cache hit"));
        }

        [Fact]
        public void ShouldResolveMultiplePathsRelativeToProjectDirectory()
        {
            var projectDir = CreateProjectDir();

            var task = new SdkTasks.Build.PathResolutionCache
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                InputPaths = new[] { "fileA.cs", "sub\\fileB.cs", "..\\fileC.cs" },
            };

            Assert.True(task.Execute());

            Assert.Equal(3, task.ResolvedPaths.Length);
            foreach (var item in task.ResolvedPaths)
            {
                Assert.True(Path.IsPathRooted(item.ItemSpec),
                    $"Resolved path '{item.ItemSpec}' should be absolute");
            }
        }
    }
}
