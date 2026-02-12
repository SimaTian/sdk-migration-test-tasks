using System.Reflection;
using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Build;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests.Build
{
    public class CanonicalPathBuilderMultiThreadingTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly string _cwdBefore;
        private readonly MockBuildEngine _engine;

        public CanonicalPathBuilderMultiThreadingTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _cwdBefore = Directory.GetCurrentDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void Execute_ResolvesRelativeInputPath_RelativeToProjectDirectory()
        {
            var task = new CanonicalPathBuilder
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = Path.Combine("subdir", "file.txt")
            };

            var result = task.Execute();

            Assert.True(result, "Task should succeed");
            Assert.NotNull(task.CanonicalPath);
            Assert.StartsWith(_projectDir, task.CanonicalPath!);
        }

        [Fact]
        public void Execute_CanonicalPathOutput_DoesNotContainCwd()
        {
            var task = new CanonicalPathBuilder
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = "somefile.txt"
            };

            task.Execute();

            if (!_projectDir.Equals(_cwdBefore, StringComparison.OrdinalIgnoreCase))
            {
                Assert.DoesNotContain(_cwdBefore, task.CanonicalPath!);
            }
        }

        [Fact]
        public void Execute_LogsCanonicalPath_ContainingProjectDirectory()
        {
            var task = new CanonicalPathBuilder
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = "relative/path.txt"
            };

            task.Execute();

            Assert.Contains(_engine.Messages, m =>
                m.Message!.Contains("Canonical:") && m.Message!.Contains(_projectDir));
        }

        [Fact]
        public void Execute_CallsGetAbsolutePathAndGetCanonicalForm()
        {
            var tracking = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new CanonicalPathBuilder
            {
                BuildEngine = _engine,
                TaskEnvironment = tracking,
                InputPath = "tracked.txt"
            };

            task.Execute();

            SharedTestHelpers.AssertUsesGetAbsolutePath(tracking);
            Assert.Contains("tracked.txt", tracking.GetAbsolutePathArgs);
            SharedTestHelpers.AssertUsesGetCanonicalForm(tracking);
        }

        [Fact]
        public void Execute_AbsoluteInputPath_CanonicalPathPreservesRoot()
        {
            var absPath = Path.Combine(_projectDir, "absolute-file.txt");

            var task = new CanonicalPathBuilder
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = absPath
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.NotNull(task.CanonicalPath);
            Assert.StartsWith(_projectDir, task.CanonicalPath!);
        }

        [Fact]
        public void Execute_SetsCanonicalPathOutputProperty()
        {
            var task = new CanonicalPathBuilder
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = "output-test.txt"
            };

            task.Execute();

            Assert.NotNull(task.CanonicalPath);
            Assert.NotEmpty(task.CanonicalPath!);
        }

        [Fact]
        public void CwdIsNotModifiedByExecution()
        {
            var task = new CanonicalPathBuilder
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = "cwd-check.txt"
            };

            task.Execute();

            Assert.Equal(_cwdBefore, Directory.GetCurrentDirectory());
        }

        [Fact]
        public void Execute_AllStringOutputsUnderProjectDirectory()
        {
            var task = new CanonicalPathBuilder
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = "reflect-test.txt"
            };

            task.Execute();

            SharedTestHelpers.AssertAllStringOutputsUnderProjectDir(task, _projectDir);
        }

        [Fact]
        public void PreservesAllPublicProperties()
        {
            var expectedProperties = new[] { "TaskEnvironment", "InputPath", "CanonicalPath" };

            var actualProperties = typeof(CanonicalPathBuilder)
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
