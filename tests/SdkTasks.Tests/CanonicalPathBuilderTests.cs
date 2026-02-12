using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class CanonicalPathBuilderTests : IDisposable
    {
        private readonly TaskTestContext _ctx;
        private string _projectDir => _ctx.ProjectDir;
        private MockBuildEngine _engine => _ctx.Engine;

        public CanonicalPathBuilderTests() => _ctx = new TaskTestContext();
        public void Dispose() => _ctx.Dispose();

        [Fact]
        public void Execute_ShouldUseTaskEnvironmentGetAbsolutePath()
        {
            var fileName = "double-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "data");

            var taskEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new SdkTasks.Build.CanonicalPathBuilder
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            SharedTestHelpers.AssertUsesGetAbsolutePath(taskEnv);
        }

        [Fact]
        public void Execute_ShouldUseTaskEnvironmentGetCanonicalForm()
        {
            var fileName = "double-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "data");

            var taskEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new SdkTasks.Build.CanonicalPathBuilder
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            SharedTestHelpers.AssertUsesGetCanonicalForm(taskEnv);
        }

        [Fact]
        public void Execute_ShouldResolveRelativeToProjectDirectory()
        {
            var fileName = "double-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "data");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.CanonicalPathBuilder
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.StartsWith(_projectDir, task.CanonicalPath);
        }

        [Fact]
        public void Execute_WithDotDotSegments_ShouldCanonicalizeToProjectDirectory()
        {
            var relativePath = "subdir/../canon-test.txt";
            Directory.CreateDirectory(Path.Combine(_projectDir, "subdir"));
            File.WriteAllText(Path.Combine(_projectDir, "canon-test.txt"), "content");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.CanonicalPathBuilder
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                InputPath = relativePath
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.StartsWith(_projectDir, task.CanonicalPath);
            Assert.DoesNotContain("..", task.CanonicalPath);
        }

        [Fact]
        public void Execute_ShouldPassInputPathToGetAbsolutePath()
        {
            var fileName = "track-args.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "data");

            var taskEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new SdkTasks.Build.CanonicalPathBuilder
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = taskEnv
            };

            task.Execute();

            Assert.Contains(fileName, taskEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void Execute_ShouldSetCanonicalPathOutput()
        {
            var fileName = "output-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "data");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.CanonicalPathBuilder
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                InputPath = fileName
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.NotNull(task.CanonicalPath);
            Assert.NotEmpty(task.CanonicalPath);
        }

        [Fact]
        public void Execute_ShouldLogInputAndCanonicalPath()
        {
            var fileName = "log-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "data");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.CanonicalPathBuilder
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                InputPath = fileName
            };

            task.Execute();

            Assert.Contains(_engine.Messages, m => m.Message!.Contains(fileName));
            Assert.Contains(_engine.Messages, m => m.Message!.Contains(_projectDir));
        }

        [Fact]
        public void Execute_WithoutExplicitProjectDirectory_ShouldAutoInitFromBuildEngine()
        {
            var projectDir = TestHelper.CreateNonCwdTempDirectory();
            try
            {
                var projectFile = Path.Combine(projectDir, "test.csproj");
                File.WriteAllText(projectFile, "<Project/>");

                var engine = new MockBuildEngine();
                var taskEnv = new TrackingTaskEnvironment();
                var task = new SdkTasks.Build.CanonicalPathBuilder
                {
                    BuildEngine = engine,
                    TaskEnvironment = taskEnv,
                    InputPath = "init-test.txt"
                };

                bool result = task.Execute();

                Assert.True(result);
                SharedTestHelpers.AssertUsesGetAbsolutePath(taskEnv);
                SharedTestHelpers.AssertUsesGetCanonicalForm(taskEnv);
            }
            finally
            {
                TestHelper.CleanupTempDirectory(projectDir);
            }
        }
    }
}
