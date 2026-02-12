using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class PathNormalizerTests : IDisposable
    {
        private readonly TaskTestContext _ctx;
        private string _projectDir => _ctx.ProjectDir;
        private MockBuildEngine _engine => _ctx.Engine;

        public PathNormalizerTests() => _ctx = new TaskTestContext();
        public void Dispose() => _ctx.Dispose();

        [Fact]
        public void ShouldResolveRelativeToProjectDirectory()
        {
            var relativePath = "testfile.txt";
            File.WriteAllText(Path.Combine(_projectDir, relativePath), "hello");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.PathNormalizer
            {
                BuildEngine = _engine,
                InputPath = relativePath,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("File found at"));
        }

        [Fact]
        public void ShouldCallGetAbsolutePathOnTaskEnvironment()
        {
            var relativePath = "tracked.txt";
            File.WriteAllText(Path.Combine(_projectDir, relativePath), "data");

            var tracking = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new SdkTasks.Build.PathNormalizer
            {
                BuildEngine = _engine,
                InputPath = relativePath,
                TaskEnvironment = tracking
            };

            task.Execute();

            SharedTestHelpers.AssertUsesGetAbsolutePath(tracking);
            Assert.Contains(relativePath, tracking.GetAbsolutePathArgs);
        }

        [Fact]
        public void ShouldNotResolveRelativeToCwd()
        {
            var relativePath = "cwd-check.txt";
            // Only create file in projectDir, NOT in CWD
            File.WriteAllText(Path.Combine(_projectDir, relativePath), "content");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.PathNormalizer
            {
                BuildEngine = _engine,
                InputPath = relativePath,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            // Resolved path must point to projectDir, not CWD
            Assert.Contains(_engine.Messages, m =>
                m.Message!.Contains("Resolved path:") && m.Message!.Contains(_projectDir));
        }

        [Fact]
        public void ShouldLogErrorWhenInputPathIsEmpty()
        {
            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.PathNormalizer
            {
                BuildEngine = _engine,
                InputPath = string.Empty,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.False(result);
            Assert.Contains(_engine.Errors, e => e.Message!.Contains("InputPath is required"));
        }

        [Fact]
        public void ShouldReportFileNotFoundForMissingFile()
        {
            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.PathNormalizer
            {
                BuildEngine = _engine,
                InputPath = "nonexistent.txt",
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("File not found at"));
        }
    }
}
