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
                TaskEnvironment = taskEnv,
                InputPath = fileName,
                EnvVarName = "TEST_VAR"
            };

            bool result = task.Execute();

            // Assert CORRECT behavior: task should find the file at ProjectDir
            Assert.True(result);
            Assert.DoesNotContain(_engine.Messages, m => m.Message!.Contains("File not found"));
        }

        [Fact]
        public void ShouldUseTaskEnvironmentGetAbsolutePath()
        {
            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var fileName = "tracked-input.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var task = new SdkTasks.Compatibility.LegacyPathResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                InputPath = fileName,
                EnvVarName = "TEST_VAR"
            };

            task.Execute();

            SharedTestHelpers.AssertGetAbsolutePathCalled(trackingEnv);
            Assert.Contains(fileName, trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void ShouldUseTaskEnvironmentGetEnvironmentVariable()
        {
            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            trackingEnv.SetEnvironmentVariable("LEGACY_CONFIG", "legacy-value");

            var fileName = "env-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var task = new SdkTasks.Compatibility.LegacyPathResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                InputPath = fileName,
                EnvVarName = "LEGACY_CONFIG"
            };

            task.Execute();

            SharedTestHelpers.AssertGetEnvironmentVariableCalled(trackingEnv);
        }

        [Fact]
        public void ShouldResolvePathToProjectDirNotCwd()
        {
            var engine = new MockBuildEngine();
            var fileName = "path-resolve-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Compatibility.LegacyPathResolver
            {
                BuildEngine = engine,
                TaskEnvironment = taskEnv,
                InputPath = fileName,
                EnvVarName = "SOME_VAR"
            };

            task.Execute();

            // Assert CORRECT behavior: resolved path in messages should contain projectDir
            var expectedResolved = Path.Combine(_projectDir, fileName);
            Assert.Contains(engine.Messages, m => m.Message!.Contains(expectedResolved));
        }

        [Fact]
        public void ShouldReadEnvVarFromTaskEnvironmentNotGlobal()
        {
            var engine = new MockBuildEngine();
            var varName = "MSBUILD_LEGACY_TEST_" + Guid.NewGuid().ToString("N")[..8];
            Environment.SetEnvironmentVariable(varName, "GLOBAL_VALUE");
            try
            {
                var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
                taskEnv.SetEnvironmentVariable(varName, "TASK_VALUE");

                var fileName = "env-override.txt";
                File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

                var task = new SdkTasks.Compatibility.LegacyPathResolver
                {
                    BuildEngine = engine,
                    TaskEnvironment = taskEnv,
                    InputPath = fileName,
                    EnvVarName = varName
                };

                task.Execute();

                // Assert CORRECT behavior: task should use TASK_VALUE from TaskEnvironment
                Assert.Contains(engine.Messages, m => m.Message!.Contains("TASK_VALUE"));
            }
            finally
            {
                Environment.SetEnvironmentVariable(varName, null);
            }
        }
    }
}
