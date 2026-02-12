using System.Reflection;
using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Build;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests.Build
{
    public class DirectPathResolverMultiThreadingTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public DirectPathResolverMultiThreadingTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ResolvesRelativeFileViaProjectDirectory_NotCwd()
        {
            var fileName = "resolve-test-" + Guid.NewGuid().ToString("N")[..8] + ".txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var task = new DirectPathResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = fileName,
                EnvVarName = "UNUSED_VAR_" + Guid.NewGuid().ToString("N")[..8]
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("Processing file"));
            Assert.DoesNotContain(_engine.Messages, m => m.Message!.Contains("does not exist"));
        }

        [Fact]
        public void ResolvesRelativeDirectoryViaProjectDirectory_NotCwd()
        {
            var dirName = "subdir-" + Guid.NewGuid().ToString("N")[..8];
            var fullDir = Path.Combine(_projectDir, dirName);
            Directory.CreateDirectory(fullDir);
            File.WriteAllText(Path.Combine(fullDir, "a.txt"), "");
            File.WriteAllText(Path.Combine(fullDir, "b.txt"), "");

            var task = new DirectPathResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = dirName,
                EnvVarName = "UNUSED_VAR"
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("Found 2 file(s)"));
        }

        [Fact]
        public void ReadsEnvVarFromTaskEnvironment_NotProcessEnvironment()
        {
            var fileName = "env-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            taskEnv.SetEnvironmentVariable("CUSTOM_CONFIG", "my_custom_value");

            var task = new DirectPathResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                InputPath = fileName,
                EnvVarName = "CUSTOM_CONFIG"
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("my_custom_value"));
            Assert.DoesNotContain(_engine.Messages, m => m.Message!.Contains("not set"));
        }

        [Fact]
        public void LogsProjectRelativePaths_NotCwdPaths()
        {
            var fileName = "path-log-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var task = new DirectPathResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = fileName,
                EnvVarName = "DUMMY"
            };

            task.Execute();

            Assert.Contains(_engine.Messages, m =>
                m.Message!.Replace('\\', '/').Contains(_projectDir.Replace('\\', '/')));
        }

        [Fact]
        public void CallsGetAbsolutePathOnTaskEnvironment()
        {
            var fileName = "tracking-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new DirectPathResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                InputPath = fileName,
                EnvVarName = "TEST_VAR"
            };

            task.Execute();

            SharedTestHelpers.AssertUsesGetAbsolutePath(trackingEnv);
            Assert.Contains(fileName, trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void CallsGetEnvironmentVariableOnTaskEnvironment()
        {
            var fileName = "env-tracking-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new DirectPathResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                InputPath = fileName,
                EnvVarName = "MY_VAR"
            };

            task.Execute();

            SharedTestHelpers.AssertUsesGetEnvironmentVariable(trackingEnv);
        }

        [Fact]
        public void DefaultsToDefaultConfigWhenEnvVarUnset()
        {
            var fileName = "default-test.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var task = new DirectPathResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = fileName,
                EnvVarName = "DEFINITELY_NOT_SET_" + Guid.NewGuid().ToString("N")[..8]
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("not set; skipping"));
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("config 'default'"));
        }

        [Fact]
        public void NonExistentPath_LogsWithProjectRelativePath()
        {
            var task = new DirectPathResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = "nonexistent/path.txt",
                EnvVarName = "SOME_VAR"
            };

            var result = task.Execute();

            Assert.True(result);
            var msg = Assert.Single(_engine.Messages, m => m.Message!.Contains("does not exist"));
            Assert.Contains(_projectDir.Replace('\\', '/'), msg.Message!.Replace('\\', '/'));
        }

        [Fact]
        public void CwdIsNotModifiedByExecution()
        {
            var cwdBefore = Directory.GetCurrentDirectory();
            var fileName = "cwd-check.txt";
            File.WriteAllText(Path.Combine(_projectDir, fileName), "content");

            var task = new DirectPathResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                InputPath = fileName,
                EnvVarName = "DUMMY"
            };

            task.Execute();

            Assert.Equal(cwdBefore, Directory.GetCurrentDirectory());
        }

        [Fact]
        public void PreservesAllPublicProperties()
        {
            var expectedProperties = new[] { "TaskEnvironment", "InputPath", "EnvVarName" };

            var actualProperties = typeof(DirectPathResolver)
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
