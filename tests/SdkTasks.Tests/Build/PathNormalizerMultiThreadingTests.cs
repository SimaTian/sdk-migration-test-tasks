using System.Reflection;
using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Build;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests.Build
{
    public class PathNormalizerMultiThreadingTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly string _cwdBefore;
        private readonly MockBuildEngine _engine;

        public PathNormalizerMultiThreadingTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _cwdBefore = Directory.GetCurrentDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void Execute_ResolvesInputPathRelativeToProjectDirectory_NotCwd()
        {
            var relativePath = Path.Combine("subdir", "testfile.txt");
            var expectedAbsPath = Path.Combine(_projectDir, "subdir", "testfile.txt");
            Directory.CreateDirectory(Path.Combine(_projectDir, "subdir"));
            File.WriteAllText(expectedAbsPath, "test content");

            var task = new PathNormalizer
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = relativePath
            };

            var result = task.Execute();

            Assert.True(result, "Task should succeed");
            Assert.Contains(_engine.Messages, m =>
                m.Message!.Contains("File found at") && m.Message!.Contains(_projectDir));
        }

        [Fact]
        public void Execute_ResolvedPathContainsProjectDirectory()
        {
            var task = new PathNormalizer
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = "relative/path.txt"
            };

            task.Execute();

            Assert.Contains(_engine.Messages, m =>
                m.Message!.Contains("Resolved path:") && m.Message!.Contains(_projectDir));
        }

        [Fact]
        public void Execute_ResolvedPathDoesNotContainCwd()
        {
            var task = new PathNormalizer
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = "somefile.txt"
            };

            task.Execute();

            if (!_projectDir.Equals(_cwdBefore, StringComparison.OrdinalIgnoreCase))
            {
                Assert.Contains(_engine.Messages, m =>
                    m.Message!.Contains("Resolved path:") && !m.Message!.Contains(_cwdBefore));
            }
        }

        [Fact]
        public void Execute_CallsGetAbsolutePathOnTaskEnvironment()
        {
            var relativePath = "tracked.txt";
            File.WriteAllText(Path.Combine(_projectDir, relativePath), "data");

            var tracking = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new PathNormalizer
            {
                BuildEngine = _engine,
                TaskEnvironment = tracking,
                InputPath = relativePath
            };

            task.Execute();

            SharedTestHelpers.AssertUsesGetAbsolutePath(tracking);
            Assert.Contains(relativePath, tracking.GetAbsolutePathArgs);
        }

        [Fact]
        public void Execute_AbsoluteInputPath_UsedDirectly()
        {
            var absPath = Path.Combine(_projectDir, "absolute-file.txt");
            File.WriteAllText(absPath, "content");

            var task = new PathNormalizer
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = absPath
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("File found at"));
        }

        [Fact]
        public void Execute_EmptyInputPath_LogsErrorAndReturnsFalse()
        {
            var task = new PathNormalizer
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = ""
            };

            var result = task.Execute();

            Assert.False(result, "Task should fail when InputPath is empty");
            Assert.Contains(_engine.Errors, e => e.Message!.Contains("InputPath is required"));
        }

        [Fact]
        public void Execute_MissingFile_ReportsNotFound()
        {
            var task = new PathNormalizer
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = "nonexistent.txt"
            };

            var result = task.Execute();

            Assert.True(result, "Task should still succeed for missing files");
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("File not found at"));
        }

        [Fact]
        public void CwdIsNotModifiedByExecution()
        {
            var task = new PathNormalizer
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = "cwd-check.txt"
            };

            task.Execute();

            Assert.Equal(_cwdBefore, Directory.GetCurrentDirectory());
        }

        [Fact]
        public void PreservesAllPublicProperties()
        {
            var expectedProperties = new[] { "TaskEnvironment", "InputPath" };

            var actualProperties = typeof(PathNormalizer)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name)
                .ToArray();

            foreach (var expected in expectedProperties)
            {
                Assert.Contains(expected, actualProperties);
            }
        }
    }
}
