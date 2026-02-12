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

            var tracking = SharedTestHelpers.CreateTrackingEnvironment(projectDir);
            var task = new SdkTasks.Build.PathResolutionCache
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = tracking,
                InputPaths = new[] { "src\\file.cs" },
            };

            Assert.True(task.Execute());

            SharedTestHelpers.AssertUsesGetCanonicalForm(tracking);
            Assert.Contains("src\\file.cs", tracking.GetCanonicalFormArgs);
        }

        [Fact]
        public void ShouldNotResolveRelativeToCwd()
        {
            var projectDir = CreateProjectDir();

            var taskEnv = TaskEnvironmentHelper.CreateForTest(projectDir);
            var task = new SdkTasks.Build.PathResolutionCache
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                InputPaths = new[] { "bin\\result.dll" },
            };

            Assert.True(task.Execute());

            var resolved = task.ResolvedPaths[0].ItemSpec;
            // Resolved path must be under projectDir, not process CWD
            Assert.StartsWith(projectDir, resolved, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Environment.CurrentDirectory, resolved, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldReturnEmptyArrayWhenNoInputPaths()
        {
            var projectDir = CreateProjectDir();

            var task = new SdkTasks.Build.PathResolutionCache
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                InputPaths = Array.Empty<string>(),
            };

            Assert.True(task.Execute());
            Assert.Empty(task.ResolvedPaths);
        }

        [Fact]
        public void ShouldSkipEmptyInputPaths()
        {
            var projectDir = CreateProjectDir();
            var engine = new MockBuildEngine();

            var task = new SdkTasks.Build.PathResolutionCache
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                InputPaths = new[] { "", "  ", "valid\\file.txt" },
            };

            Assert.True(task.Execute());
            Assert.Single(task.ResolvedPaths);
            Assert.Contains(engine.Warnings, w => w.Message!.Contains("empty input path"));
        }

        [Fact]
        public void ShouldSetMetadataOnResolvedItems()
        {
            var projectDir = CreateProjectDir();

            var task = new SdkTasks.Build.PathResolutionCache
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                InputPaths = new[] { "src\\main.cs" },
            };

            Assert.True(task.Execute());

            var item = task.ResolvedPaths[0];
            Assert.Equal("src\\main.cs", item.GetMetadata("OriginalPath"));
            Assert.Equal(".cs", item.GetMetadata("ResolvedExtension"));
            Assert.NotEmpty(item.GetMetadata("ResolvedDirectory"));
        }

        [Fact]
        public void ShouldResolveMultiplePathsToSameProjectDirectory()
        {
            var projectDir = CreateProjectDir();

            var task = new SdkTasks.Build.PathResolutionCache
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                InputPaths = new[] { "a.txt", "b.txt", "sub\\c.txt" },
            };

            Assert.True(task.Execute());

            Assert.Equal(3, task.ResolvedPaths.Length);
            foreach (var item in task.ResolvedPaths)
            {
                Assert.StartsWith(projectDir, item.ItemSpec, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void ShouldTrackAllInputPathsThroughGetCanonicalForm()
        {
            var projectDir = CreateProjectDir();
            var tracking = SharedTestHelpers.CreateTrackingEnvironment(projectDir);

            var task = new SdkTasks.Build.PathResolutionCache
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = tracking,
                InputPaths = new[] { "first.cs", "second.cs" },
            };

            Assert.True(task.Execute());

            Assert.True(tracking.GetCanonicalFormCallCount >= 2,
                "Task must call GetCanonicalForm for each input path");
            Assert.Contains("first.cs", tracking.GetCanonicalFormArgs);
            Assert.Contains("second.cs", tracking.GetCanonicalFormArgs);
        }
    }
}
