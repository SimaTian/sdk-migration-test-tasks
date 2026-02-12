using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests.Build
{
    public class DirectoryContextSwitcherMultiThreadingTests : IDisposable
    {
        private readonly string _projectDir;

        public DirectoryContextSwitcherMultiThreadingTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void Execute_WithRelativeNewDirectory_ResolvesRelativeToProjectDirectory()
        {
            var subDir = Path.Combine(_projectDir, "output", "bin");
            Directory.CreateDirectory(subDir);

            var engine = new MockBuildEngine();
            var task = new SdkTasks.Build.DirectoryContextSwitcher
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                NewDirectory = Path.Combine("output", "bin")
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.Empty(engine.Errors);

            var expectedPath = Path.Combine(_projectDir, "output", "bin");
            var message = Assert.Single(engine.Messages);
            Assert.Contains(expectedPath, message.Message, StringComparison.OrdinalIgnoreCase);

            // Ensure the resolved path is NOT based on CWD
            var cwdBased = Path.Combine(Environment.CurrentDirectory, "output", "bin");
            if (!string.Equals(cwdBased, expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                Assert.DoesNotContain(cwdBased, message.Message);
            }
        }

        [Fact]
        public void Execute_WithAbsoluteNewDirectory_PassesThroughUnchanged()
        {
            var absoluteDir = TestHelper.CreateNonCwdTempDirectory();
            try
            {
                var engine = new MockBuildEngine();
                var task = new SdkTasks.Build.DirectoryContextSwitcher
                {
                    BuildEngine = engine,
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                    NewDirectory = absoluteDir
                };

                var result = task.Execute();

                Assert.True(result);
                var message = Assert.Single(engine.Messages);
                Assert.Contains(absoluteDir, message.Message, StringComparison.OrdinalIgnoreCase);

                // The resolved path should NOT be re-rooted under projectDir
                var reRooted = Path.Combine(_projectDir, absoluteDir);
                Assert.DoesNotContain(reRooted, message.Message);
            }
            finally
            {
                TestHelper.CleanupTempDirectory(absoluteDir);
            }
        }

        [Fact]
        public void Execute_LogsResolvedAndOriginalPaths()
        {
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Build.DirectoryContextSwitcher
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                NewDirectory = "relative-dir"
            };

            var result = task.Execute();

            Assert.True(result);
            var message = Assert.Single(engine.Messages);

            var resolvedPath = Path.Combine(_projectDir, "relative-dir");
            Assert.Contains(resolvedPath, message.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("relative-dir", message.Message);
            Assert.Contains("Directory context for build operations:", message.Message);
            Assert.Contains("resolved from:", message.Message);
        }

        [Fact]
        public void Execute_DoesNotModifyGlobalCwd()
        {
            var originalCwd = Environment.CurrentDirectory;

            var task = new SdkTasks.Build.DirectoryContextSwitcher
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                NewDirectory = "some-dir"
            };

            task.Execute();

            Assert.Equal(originalCwd, Environment.CurrentDirectory);
        }
    }
}
