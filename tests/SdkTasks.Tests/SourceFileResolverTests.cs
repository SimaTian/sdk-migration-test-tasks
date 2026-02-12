using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class SourceFileResolverTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public SourceFileResolverTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldResolveRelativeToProjectDirectory()
        {
            var relativePath = "ignoretask.txt";
            File.WriteAllText(Path.Combine(_projectDir, relativePath), "data");

            var task = new SdkTasks.Compilation.SourceFileResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = relativePath
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("File size:"));
        }

        [Fact]
        public void ShouldCallGetAbsolutePathOnTaskEnvironment()
        {
            var relativePath = "tracked-source.txt";
            File.WriteAllText(Path.Combine(_projectDir, relativePath), "tracked");

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Compilation.SourceFileResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                InputPath = relativePath
            };

            task.Execute();

            SharedTestHelpers.AssertGetAbsolutePathCalled(trackingEnv);
            Assert.Contains(relativePath, trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void ResolvedPathShouldContainProjectDirectory()
        {
            var relativePath = "resolve-check.txt";
            File.WriteAllText(Path.Combine(_projectDir, relativePath), "data");

            var task = new SdkTasks.Compilation.SourceFileResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = relativePath
            };

            task.Execute();

            // The resolved path logged should reference the projectDir
            Assert.Contains(_engine.Messages, m => m.Message!.Contains(_projectDir));
        }

        [Fact]
        public void FileDoesNotExist_LogsWarning()
        {
            var task = new SdkTasks.Compilation.SourceFileResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = "nonexistent.txt"
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Warnings, w => w.Message!.Contains("does not exist"));
        }

        [Fact]
        public void EmptyInputPath_LogsErrorAndReturnsFalse()
        {
            var task = new SdkTasks.Compilation.SourceFileResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = ""
            };

            bool result = task.Execute();

            Assert.False(result);
            Assert.Contains(_engine.Errors, e => e.Message!.Contains("InputPath is required"));
        }

        [Fact]
        public void DoesNotResolveRelativeToCwd()
        {
            var relativePath = "cwd-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, relativePath), "data");

            var tracking = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Compilation.SourceFileResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = tracking,
                InputPath = relativePath
            };

            bool result = task.Execute();

            Assert.True(result);
            // The resolved path should be under projectDir, not CWD
            var resolvedMsg = _engine.Messages.FirstOrDefault(m => m.Message!.Contains("Resolved"));
            Assert.NotNull(resolvedMsg);
            Assert.Contains(_projectDir, resolvedMsg.Message!);
        }
    }
}
