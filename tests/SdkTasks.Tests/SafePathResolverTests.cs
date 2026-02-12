using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class SafePathResolverTests : IDisposable
    {
        private readonly TaskTestContext _ctx;
        private string _projectDir => _ctx.ProjectDir;
        private MockBuildEngine _engine => _ctx.Engine;

        public SafePathResolverTests() => _ctx = new TaskTestContext();
        public void Dispose() => _ctx.Dispose();

        [Fact]
        public void ShouldResolveRelativeToProjectDirectory()
        {
            var fileName = "null-check-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "data");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.SafePathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            var expectedResolved = Path.Combine(_projectDir, fileName);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains(expectedResolved));
        }

        [Fact]
        public void ShouldCallGetAbsolutePathOnTaskEnvironment()
        {
            var fileName = "tracking-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var tracking = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new SdkTasks.Build.SafePathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = tracking
            };

            task.Execute();

            SharedTestHelpers.AssertUsesGetAbsolutePath(tracking);
            Assert.Contains(fileName, tracking.GetAbsolutePathArgs);
        }

        [Fact]
        public void ShouldNotResolveRelativeToCwd()
        {
            var fileName = "cwd-check-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "data");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.SafePathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            // The resolved path must contain the project directory, not the CWD
            Assert.Contains(_engine.Messages, m => m.Message!.Contains(_projectDir));
            Assert.DoesNotContain(_engine.Messages, m => m.Message!.Contains("does not exist"));
        }

        [Fact]
        public void ShouldLogFileSizeWhenFileExists()
        {
            var fileName = "exists-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "hello world");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.SafePathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("bytes"));
            Assert.DoesNotContain(_engine.Messages, m => m.Message!.Contains("does not exist"));
        }

        [Fact]
        public void ShouldLogDoesNotExistWhenFileMissing()
        {
            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.SafePathResolver
            {
                BuildEngine = _engine,
                InputPath = "nonexistent-file.txt",
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("does not exist"));
        }

        [Fact]
        public void ShouldAutoInitializeProjectDirectoryFromBuildEngine()
        {
            // When TaskEnvironment.ProjectDirectory is empty, it should be auto-set
            // from BuildEngine.ProjectFileOfTaskNode
            var taskEnv = new TaskEnvironment();
            Assert.True(string.IsNullOrEmpty(taskEnv.ProjectDirectory));

            var task = new SdkTasks.Build.SafePathResolver
            {
                BuildEngine = _engine,
                InputPath = "somefile.txt",
                TaskEnvironment = taskEnv
            };

            task.Execute();

            // After Execute, ProjectDirectory should have been initialized
            Assert.False(string.IsNullOrEmpty(taskEnv.ProjectDirectory),
                "Task should auto-initialize ProjectDirectory from BuildEngine.ProjectFileOfTaskNode");
        }
    }
}
