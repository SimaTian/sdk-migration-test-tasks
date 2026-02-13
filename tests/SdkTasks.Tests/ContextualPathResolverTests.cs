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
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Build.ContextualPathResolver();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Build.ContextualPathResolver),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
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
        public void ShouldAutoInitializeProjectDirectoryFromBuildEngine()
        {
            var task = new SdkTasks.Build.ContextualPathResolver
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = new TaskEnvironment(),
                RelativePaths = new[] { "src\\file.cs" },
            };

            Assert.True(task.Execute());

            var expectedDir = Directory.GetCurrentDirectory();
            Assert.Equal(expectedDir, task.TaskEnvironment.ProjectDirectory);
            Assert.All(task.ResolvedItems,
                item => Assert.Equal(expectedDir, item.GetMetadata("ProjectDirectory")));
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
    }
}
