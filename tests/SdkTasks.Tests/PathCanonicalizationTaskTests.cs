using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class PathCanonicalizationTaskTests : IDisposable
    {
        private readonly List<string> _tempDirs = new();

        private string CreateTempDir()
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
        public void Execute_WithRelativePath_ShouldUseGetCanonicalForm()
        {
            var projectDir = CreateTempDir();
            var relativePath = "subdir/../canon-test.txt";
            Directory.CreateDirectory(Path.Combine(projectDir, "subdir"));
            File.WriteAllText(Path.Combine(projectDir, "canon-test.txt"), "content");

            var taskEnv = SharedTestHelpers.CreateTrackingEnvironment(projectDir);
            var task = new SdkTasks.Build.PathCanonicalizationTask
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                InputPath = relativePath
            };

            bool result = task.Execute();

            Assert.True(result);
            SharedTestHelpers.AssertUsesGetCanonicalForm(taskEnv);
        }

        [Fact]
        public void Execute_ShouldResolveRelativeToProjectDirectory()
        {
            var projectDir = CreateTempDir();
            var relativePath = "subdir/../canon-test.txt";
            Directory.CreateDirectory(Path.Combine(projectDir, "subdir"));
            File.WriteAllText(Path.Combine(projectDir, "canon-test.txt"), "content");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(projectDir);
            var task = new SdkTasks.Build.PathCanonicalizationTask
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                InputPath = relativePath
            };

            bool result = task.Execute();
            var engine = (MockBuildEngine)task.BuildEngine;

            Assert.True(result);
            Assert.Contains(engine.Messages, m => m.Message!.Contains(projectDir));
        }

        [Fact]
        public void Execute_WithExistingFile_ShouldReadContent()
        {
            var projectDir = CreateTempDir();
            var relativePath = "canon-read.txt";
            File.WriteAllText(Path.Combine(projectDir, relativePath), "hello world");

            var taskEnv = SharedTestHelpers.CreateTrackingEnvironment(projectDir);
            var task = new SdkTasks.Build.PathCanonicalizationTask
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                InputPath = relativePath
            };

            bool result = task.Execute();
            var engine = (MockBuildEngine)task.BuildEngine;

            Assert.True(result);
            Assert.Contains(engine.Messages, m => m.Message!.Contains("Read") && m.Message!.Contains("characters"));
        }

        [Fact]
        public void Execute_WithEmptyInputPath_ShouldLogError()
        {
            var projectDir = CreateTempDir();
            var taskEnv = TaskEnvironmentHelper.CreateForTest(projectDir);
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Build.PathCanonicalizationTask
            {
                BuildEngine = engine,
                TaskEnvironment = taskEnv,
                InputPath = string.Empty
            };

            bool result = task.Execute();

            Assert.False(result);
            Assert.Contains(engine.Errors, e => e.Message!.Contains("InputPath is required"));
        }

        [Fact]
        public void Execute_ShouldPassInputPathToGetCanonicalForm()
        {
            var projectDir = CreateTempDir();
            var relativePath = "some/../target.txt";
            Directory.CreateDirectory(Path.Combine(projectDir, "some"));
            File.WriteAllText(Path.Combine(projectDir, "target.txt"), "data");

            var taskEnv = SharedTestHelpers.CreateTrackingEnvironment(projectDir);
            var task = new SdkTasks.Build.PathCanonicalizationTask
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                InputPath = relativePath
            };

            task.Execute();

            Assert.Contains(relativePath, taskEnv.GetCanonicalFormArgs);
        }

        [Fact]
        public void Execute_WithoutExplicitProjectDirectory_ShouldAutoInitFromBuildEngine()
        {
            var projectDir = CreateTempDir();
            var projectFile = Path.Combine(projectDir, "test.csproj");
            File.WriteAllText(projectFile, "<Project/>");
            File.WriteAllText(Path.Combine(projectDir, "init-test.txt"), "auto");

            var engine = new MockBuildEngine();
            var taskEnv = new TrackingTaskEnvironment();
            var task = new SdkTasks.Build.PathCanonicalizationTask
            {
                BuildEngine = engine,
                TaskEnvironment = taskEnv,
                InputPath = "init-test.txt"
            };

            bool result = task.Execute();

            // Task should have auto-initialized ProjectDirectory from BuildEngine
            Assert.True(result);
            SharedTestHelpers.AssertUsesGetCanonicalForm(taskEnv);
        }

        [Fact]
        public void Execute_WithNonExistentFile_StillSucceeds()
        {
            var projectDir = CreateTempDir();
            var taskEnv = TaskEnvironmentHelper.CreateForTest(projectDir);
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Build.PathCanonicalizationTask
            {
                BuildEngine = engine,
                TaskEnvironment = taskEnv,
                InputPath = "nonexistent.txt"
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(engine.Messages, m => m.Message!.Contains("Canonical path:") && m.Message!.Contains(projectDir));
            Assert.DoesNotContain(engine.Messages, m => m.Message!.Contains("Read"));
            Assert.Empty(engine.Errors);
        }

        [Fact]
        public void Execute_WithAbsoluteInputPath_PassesThroughUnchanged()
        {
            var fileDir = CreateTempDir();
            var filePath = Path.Combine(fileDir, "absolute-test.txt");
            File.WriteAllText(filePath, "absolute content");

            var otherDir = CreateTempDir();
            var taskEnv = TaskEnvironmentHelper.CreateForTest(otherDir);
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Build.PathCanonicalizationTask
            {
                BuildEngine = engine,
                TaskEnvironment = taskEnv,
                InputPath = filePath
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(engine.Messages, m => m.Message!.Contains(filePath));
            Assert.Contains(engine.Messages, m => m.Message!.Contains("Read") && m.Message!.Contains("characters"));
            Assert.Empty(engine.Errors);
        }
    }
}
