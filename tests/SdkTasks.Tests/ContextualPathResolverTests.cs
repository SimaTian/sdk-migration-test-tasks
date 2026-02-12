using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Build;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class ContextualPathResolverTests : IDisposable
    {
        private readonly TaskTestContext _ctx;

        public ContextualPathResolverTests() => _ctx = new TaskTestContext();

        private string CreateProjectDir() => _ctx.CreateAdditionalProjectDir();

        public void Dispose() => _ctx.Dispose();

        [Fact]
        public void ShouldNotModifyGlobalCwd()
        {
            var dir1 = CreateProjectDir();
            var originalCwd = Environment.CurrentDirectory;

            var cwdChanged = false;
            var executing = true;

            var monitor = new Thread(() =>
            {
                while (Volatile.Read(ref executing))
                {
                    if (!Environment.CurrentDirectory.Equals(originalCwd, StringComparison.OrdinalIgnoreCase))
                    {
                        cwdChanged = true;
                        break;
                    }
                }
            });
            monitor.IsBackground = true;
            monitor.Start();

            for (int i = 0; i < 50 && !cwdChanged; i++)
            {
                var task = new ContextualPathResolver
                {
                    BuildEngine = new MockBuildEngine(),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir1),
                    RelativePaths = new[] { "src\\file.cs", "lib\\helper.cs", "tests\\test.cs" },
                };
                task.Execute();
            }

            Volatile.Write(ref executing, false);
            monitor.Join(2000);

            Assert.False(cwdChanged,
                "Task must not modify Environment.CurrentDirectory.");

            Environment.CurrentDirectory = originalCwd;
        }

        [Fact]
        public void ShouldResolveToOwnProjectDirectory()
        {
            var dir1 = CreateProjectDir();
            var dir2 = CreateProjectDir();

            var task1 = new ContextualPathResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir1),
                RelativePaths = new[] { "src\\file.cs" },
            };

            var task2 = new ContextualPathResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir2),
                RelativePaths = new[] { "src\\file.cs" },
            };

            Assert.True(task1.Execute());
            Assert.True(task2.Execute());

            var resolved1 = task1.ResolvedItems[0].ItemSpec;
            var resolved2 = task2.ResolvedItems[0].ItemSpec;

            Assert.StartsWith(dir1, resolved1, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(dir2, resolved2, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(resolved1, resolved2);
        }

        [Fact]
        public void ShouldUseGetCanonicalForm_ViaTaskEnvironment()
        {
            var projectDir = CreateProjectDir();
            var tracking = SharedTestHelpers.CreateTrackingEnvironment(projectDir);

            var task = new ContextualPathResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = tracking,
                RelativePaths = new[] { "src\\file.cs", "lib\\helper.cs" },
            };

            Assert.True(task.Execute());

            // Task must call GetCanonicalForm for each relative path
            SharedTestHelpers.AssertMinimumGetCanonicalFormCalls(tracking, 2);
        }

        [Fact]
        public void EmptyPaths_ReturnsSuccessWithNoItems()
        {
            var projectDir = CreateProjectDir();

            var task = new ContextualPathResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                RelativePaths = Array.Empty<string>(),
            };

            Assert.True(task.Execute());
            Assert.Empty(task.ResolvedItems);
        }

        [Fact]
        public void SetsMetadataOnResolvedItems()
        {
            var projectDir = CreateProjectDir();

            var task = new ContextualPathResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                RelativePaths = new[] { "src\\file.cs" },
            };

            Assert.True(task.Execute());

            var item = task.ResolvedItems[0];
            Assert.Equal("src\\file.cs", item.GetMetadata("OriginalRelativePath"));
            Assert.Equal(projectDir, item.GetMetadata("ProjectDirectory"));
            Assert.Equal("False", item.GetMetadata("IsRooted"));
        }

        [Fact]
        public void SkipsWhitespaceEntries_WithWarning()
        {
            var projectDir = CreateProjectDir();
            var engine = new MockBuildEngine();

            var task = new ContextualPathResolver
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                RelativePaths = new[] { "  ", "src\\file.cs" },
            };

            Assert.True(task.Execute());
            Assert.Single(task.ResolvedItems);
            Assert.Contains(engine.Warnings, w => w.Message!.Contains("empty relative path"));
        }

        [Fact]
        public void AutoInitializesProjectDirectory_FromBuildEngine()
        {
            var tracking = new TrackingTaskEnvironment();
            // Leave ProjectDirectory empty to trigger auto-init from BuildEngine

            var task = new ContextualPathResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = tracking,
                RelativePaths = new[] { "src\\file.cs" },
            };

            Assert.True(task.Execute());

            // GetCanonicalForm should be called during auto-init for the project file path
            SharedTestHelpers.AssertMinimumGetCanonicalFormCalls(tracking, 1);
        }

        [Fact]
        public void WarnsWhenPathEscapesProjectDirectory()
        {
            var projectDir = CreateProjectDir();
            var engine = new MockBuildEngine();

            var task = new ContextualPathResolver
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                RelativePaths = new[] { "..\\..\\etc\\passwd" },
            };

            Assert.True(task.Execute());
            Assert.Contains(engine.Warnings, w => w.Message!.Contains("escapes project directory"));
        }
    }
}
