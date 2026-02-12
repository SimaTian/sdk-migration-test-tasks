using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class LegacyPathResolverTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public LegacyPathResolverTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldResolveRelativeToProjectDirectory()
        {
            var fileName = "test-input.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Compatibility.LegacyPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                EnvVarName = "TEST_VAR",
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.DoesNotContain(_engine.Messages, m => m.Message!.Contains("File not found"));
        }

        [Fact]
        public void ShouldCallGetAbsolutePathOnTaskEnvironment()
        {
            var fileName = "tracking-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new SdkTasks.Compatibility.LegacyPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                EnvVarName = "TEST_VAR",
                TaskEnvironment = trackingEnv
            };

            task.Execute();

            // Assert CORRECT behavior: task uses TaskEnvironment.GetAbsolutePath, not Path.GetFullPath
            SharedTestHelpers.AssertUsesGetAbsolutePath(trackingEnv);
            Assert.Contains(fileName, trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void ShouldCallGetEnvironmentVariableOnTaskEnvironment()
        {
            var fileName = "env-tracking.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new SdkTasks.Compatibility.LegacyPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                EnvVarName = "MY_TEST_VAR",
                TaskEnvironment = trackingEnv
            };

            task.Execute();

            // Assert CORRECT behavior: task uses TaskEnvironment.GetEnvironmentVariable, not Environment.GetEnvironmentVariable
            SharedTestHelpers.AssertUsesGetEnvironmentVariable(trackingEnv);
        }

        [Fact]
        public void ShouldResolvePathToProjectDirNotCwd()
        {
            var fileName = "resolve-check.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new SdkTasks.Compatibility.LegacyPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                EnvVarName = "TEST_VAR",
                TaskEnvironment = trackingEnv
            };

            task.Execute();

            // Assert CORRECT behavior: resolved path is relative to ProjectDirectory, not CWD
            var expectedPath = Path.Combine(_projectDir, fileName);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains(expectedPath));
        }

        [Fact]
        public void ShouldLogWithEnvVarWhenSet()
        {
            var fileName = "env-set.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            taskEnv.SetEnvironmentVariable("MY_CONFIG", "config-value");

            var task = new SdkTasks.Compatibility.LegacyPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                EnvVarName = "MY_CONFIG",
                TaskEnvironment = taskEnv
            };

            task.Execute();

            // Assert CORRECT behavior: task logs message containing the env var value
            Assert.Contains(_engine.Messages, m =>
                m.Message!.Contains("MY_CONFIG") && m.Message!.Contains("config-value"));
        }

        [Fact]
        public void ShouldLogDefaultsWhenEnvVarNotSet()
        {
            var fileName = "env-unset.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Compatibility.LegacyPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                EnvVarName = "NONEXISTENT_VAR",
                TaskEnvironment = taskEnv
            };

            task.Execute();

            // Assert CORRECT behavior: task logs that env var is not set
            Assert.Contains(_engine.Messages, m =>
                m.Message!.Contains("NONEXISTENT_VAR") && m.Message!.Contains("not set"));
        }

        [Fact]
        public void ShouldAutoInitializeProjectDirectoryFromBuildEngine()
        {
            var fileName = "auto-init.txt";

            // Use default TaskEnvironment with empty ProjectDirectory
            var task = new SdkTasks.Compatibility.LegacyPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                EnvVarName = "TEST_VAR"
            };

            // Execute should succeed even without explicit ProjectDirectory setup
            bool result = task.Execute();
            Assert.True(result);
        }
    }
}
