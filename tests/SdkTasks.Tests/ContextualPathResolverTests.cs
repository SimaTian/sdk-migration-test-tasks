using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class ContextualPathResolverTests : IDisposable
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
                var task = new SdkTasks.Build.ContextualPathResolver
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

            var task1 = new SdkTasks.Build.ContextualPathResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir1),
                RelativePaths = new[] { "src\\file.cs" },
            };

            var task2 = new SdkTasks.Build.ContextualPathResolver
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
        public void ShouldUseTaskEnvironmentGetCanonicalForm()
        {
            var dir1 = CreateProjectDir();

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = dir1 };
            var task = new SdkTasks.Build.ContextualPathResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = trackingEnv,
                RelativePaths = new[] { "src\\file.cs" },
            };

            task.Execute();

            SharedTestHelpers.AssertGetCanonicalFormCalled(trackingEnv);
        }

        [Fact]
        public void ShouldResolveMultipleRelativePaths()
        {
            var dir1 = CreateProjectDir();
            var paths = new[] { "src\\file1.cs", "lib\\file2.cs", "tests\\file3.cs" };

            var task = new SdkTasks.Build.ContextualPathResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir1),
                RelativePaths = paths,
            };

            Assert.True(task.Execute());
            Assert.Equal(3, task.ResolvedItems.Length);

            foreach (var item in task.ResolvedItems)
            {
                Assert.StartsWith(dir1, item.ItemSpec, StringComparison.OrdinalIgnoreCase);
                Assert.False(string.IsNullOrEmpty(item.GetMetadata("OriginalRelativePath")));
            }
        }

        [Fact]
        public void ShouldSetProjectDirectoryMetadata()
        {
            var dir1 = CreateProjectDir();

            var task = new SdkTasks.Build.ContextualPathResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir1),
                RelativePaths = new[] { "src\\file.cs" },
            };

            task.Execute();

            Assert.NotEmpty(task.ResolvedItems);
            string metadata = task.ResolvedItems[0].GetMetadata("ProjectDirectory");
            Assert.Equal(dir1, metadata);
        }
    }
}
