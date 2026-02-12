using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class DirectPathResolverTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public DirectPathResolverTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldResolveRelativeToProjectDirectory()
        {
            var fileName = "ignore-env-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.DirectPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                EnvVarName = "TEST_CONFIG",
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("Processing file"));
        }

        [Fact]
        public void ShouldCallTaskEnvironmentGetAbsolutePath()
        {
            var fileName = "tracking-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Build.DirectPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                EnvVarName = "UNUSED_VAR",
                TaskEnvironment = trackingEnv
            };

            task.Execute();

            SharedTestHelpers.AssertGetAbsolutePathCalled(trackingEnv);
        }

        [Fact]
        public void ShouldCallTaskEnvironmentGetEnvironmentVariable()
        {
            var fileName = "env-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            trackingEnv.SetEnvironmentVariable("MY_CONFIG", "task-scoped-value");

            var task = new SdkTasks.Build.DirectPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                EnvVarName = "MY_CONFIG",
                TaskEnvironment = trackingEnv
            };

            task.Execute();

            SharedTestHelpers.AssertGetEnvironmentVariableCalled(trackingEnv);
        }

        [Fact]
        public void ShouldReadEnvVarFromTaskEnvironmentNotGlobal()
        {
            var fileName = "env-scope-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var varName = "MSBUILD_TEST_" + Guid.NewGuid().ToString("N")[..8];
            Environment.SetEnvironmentVariable(varName, "GLOBAL_VALUE");

            try
            {
                var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
                taskEnv.SetEnvironmentVariable(varName, "TASK_VALUE");

                var task = new SdkTasks.Build.DirectPathResolver
                {
                    BuildEngine = _engine,
                    InputPath = fileName,
                    EnvVarName = varName,
                    TaskEnvironment = taskEnv
                };

                task.Execute();

                // Task should use TASK_VALUE from TaskEnvironment, not GLOBAL_VALUE
                Assert.Contains(_engine.Messages, m => m.Message!.Contains("TASK_VALUE"));
                Assert.DoesNotContain(_engine.Messages, m => m.Message!.Contains("GLOBAL_VALUE"));
            }
            finally
            {
                Environment.SetEnvironmentVariable(varName, null);
            }
        }

        [Fact]
        public void ShouldNotResolveRelativeToCwd()
        {
            var fileName = "cwd-check.txt";
            // File exists ONLY in projectDir, not in CWD
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.DirectPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                EnvVarName = "UNUSED",
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            // Should find the file at projectDir, not report "does not exist"
            Assert.DoesNotContain(_engine.Messages, m => m.Message!.Contains("does not exist"));
        }

        [Fact]
        public void ShouldResolveDirectoryRelativeToProjectDirectory()
        {
            var subDir = "mysubdir_" + Guid.NewGuid().ToString("N")[..6];
            var fullSubDir = Path.Combine(_projectDir, subDir);
            Directory.CreateDirectory(fullSubDir);
            File.WriteAllText(Path.Combine(fullSubDir, "dummy.txt"), "x");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.DirectPathResolver
            {
                BuildEngine = _engine,
                InputPath = subDir,
                EnvVarName = "UNUSED",
                TaskEnvironment = taskEnv
            };

            task.Execute();

            Assert.Contains(_engine.Messages, m => m.Message!.Contains("Found") && m.Message!.Contains("file(s)"));
        }
    }
}
