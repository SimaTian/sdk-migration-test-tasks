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
        public void ShouldResolveRelativeFileToProjectDirectory()
        {
            var fileName = "direct-path-test.txt";
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
            Assert.DoesNotContain(_engine.Messages, m => m.Message!.Contains("does not exist"));
        }

        [Fact]
        public void ShouldResolveRelativeDirectoryToProjectDirectory()
        {
            var dirName = "testsubdir";
            var dirPath = Path.Combine(_projectDir, dirName);
            Directory.CreateDirectory(dirPath);
            File.WriteAllText(Path.Combine(dirPath, "dummy.txt"), "x");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.DirectPathResolver
            {
                BuildEngine = _engine,
                InputPath = dirName,
                EnvVarName = "SOME_VAR",
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("Found") && m.Message!.Contains("file(s)"));
        }

        [Fact]
        public void ShouldCallGetAbsolutePathOnTaskEnvironment()
        {
            var fileName = "tracking-path-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new SdkTasks.Build.DirectPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                EnvVarName = "TEST_VAR",
                TaskEnvironment = trackingEnv
            };

            task.Execute();

            SharedTestHelpers.AssertUsesGetAbsolutePath(trackingEnv);
            Assert.Contains(fileName, trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void ShouldCallGetEnvironmentVariableOnTaskEnvironment()
        {
            var fileName = "env-tracking-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new SdkTasks.Build.DirectPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                EnvVarName = "MY_CONFIG_VAR",
                TaskEnvironment = trackingEnv
            };

            task.Execute();

            SharedTestHelpers.AssertUsesGetEnvironmentVariable(trackingEnv);
        }

        [Fact]
        public void ShouldNotResolveRelativeToCwd()
        {
            // File exists ONLY in _projectDir, not in CWD
            var fileName = "noncwd-test-" + Guid.NewGuid().ToString("N")[..8] + ".txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.DirectPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                EnvVarName = "DUMMY_VAR",
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            // Task must find the file in ProjectDirectory, not report it missing
            Assert.True(result);
            Assert.DoesNotContain(_engine.Messages, m => m.Message!.Contains("does not exist"));
        }

        [Fact]
        public void ShouldLogDefaultWhenEnvVarNotSet()
        {
            var fileName = "default-config-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.DirectPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                EnvVarName = "UNSET_VAR_" + Guid.NewGuid().ToString("N")[..8],
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("config 'default'"));
        }

        [Fact]
        public void ShouldUseResolvedPathFromProjectDir()
        {
            var fileName = "resolved-path-check.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new SdkTasks.Build.DirectPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                EnvVarName = "TEST_VAR",
                TaskEnvironment = trackingEnv
            };

            task.Execute();

            // The resolved path logged in messages should contain the project directory
            Assert.Contains(_engine.Messages, m => m.Message!.Contains(_projectDir));
        }

        [Fact]
        public void ShouldReadEnvVarFromTaskEnvironment()
        {
            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            taskEnv.SetEnvironmentVariable("MY_CONFIG", "custom_value");

            var fileName = "env-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var task = new SdkTasks.Build.DirectPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                EnvVarName = "MY_CONFIG",
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("custom_value"));
            Assert.DoesNotContain(_engine.Messages, m => m.Message!.Contains("not set"));
        }

        [Fact]
        public void ShouldEnumerateDirectoryRelativeToProjectDirectory()
        {
            var subDir = "build-output";
            var fullSubDir = Path.Combine(_projectDir, subDir);
            Directory.CreateDirectory(fullSubDir);
            File.WriteAllText(Path.Combine(fullSubDir, "a.dll"), "");
            File.WriteAllText(Path.Combine(fullSubDir, "b.dll"), "");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.DirectPathResolver
            {
                BuildEngine = _engine,
                InputPath = subDir,
                EnvVarName = "UNUSED_VAR",
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("Found 2 file(s)"));
        }

        [Fact]
        public void ShouldLogProjectRelativePathForNonExistentPath()
        {
            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.DirectPathResolver
            {
                BuildEngine = _engine,
                InputPath = "nonexistent/path.txt",
                EnvVarName = "SOME_VAR",
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            var msg = Assert.Single(_engine.Messages, m => m.Message!.Contains("does not exist"));
            Assert.Contains(_projectDir.Replace('\\', '/'), msg.Message!.Replace('\\', '/'));
        }

        [Fact]
        public void ShouldFallbackToDefaultWhenEnvVarNotSet()
        {
            var fileName = "fallback-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.DirectPathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                EnvVarName = "DEFINITELY_NOT_SET_VAR_" + Guid.NewGuid().ToString("N")[..8],
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("not set; skipping"));
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("config 'default'"));
        }
    }
}
