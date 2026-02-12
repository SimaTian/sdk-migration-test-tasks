using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class ProjectConfigReaderTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public ProjectConfigReaderTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldResolveRelativeToProjectDirectory()
        {
            var relativePath = "data.xml";
            File.WriteAllText(Path.Combine(_projectDir, relativePath), "<root><item/></root>");

            var task = new SdkTasks.Compilation.ProjectConfigReader
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                XmlPath = relativePath
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("Loaded XML with"));
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("Saved updated XML"));
        }

        [Fact]
        public void ShouldCallGetAbsolutePathOnTaskEnvironment()
        {
            var relativePath = "tracked.xml";
            File.WriteAllText(Path.Combine(_projectDir, relativePath), "<root><item/></root>");

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Compilation.ProjectConfigReader
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                XmlPath = relativePath
            };

            task.Execute();

            SharedTestHelpers.AssertGetAbsolutePathCalled(trackingEnv);
            Assert.Contains(relativePath, trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void ShouldLoadAndSaveXmlInProjectDir()
        {
            var relativePath = "config.xml";
            var fullPath = Path.Combine(_projectDir, relativePath);
            File.WriteAllText(fullPath, "<root><entry/></root>");

            var task = new SdkTasks.Compilation.ProjectConfigReader
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                XmlPath = relativePath
            };

            bool result = task.Execute();

            Assert.True(result);
            // Verify the file in projectDir was actually modified (processed attribute added)
            var content = File.ReadAllText(fullPath);
            Assert.Contains("processed", content);
        }

        [Fact]
        public void ShouldFailWhenXmlPathIsEmpty()
        {
            var task = new SdkTasks.Compilation.ProjectConfigReader
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                XmlPath = ""
            };

            bool result = task.Execute();

            Assert.False(result);
            Assert.Contains(_engine.Errors, e => e.Message!.Contains("XmlPath is required"));
        }

        [Fact]
        public void ShouldNotResolveRelativeToCwd()
        {
            var relativePath = "nocwd.xml";
            File.WriteAllText(Path.Combine(_projectDir, relativePath), "<root/>");

            // Do NOT place the file in CWD — only in projectDir
            var cwdPath = Path.Combine(Directory.GetCurrentDirectory(), relativePath);
            bool cwdFileExisted = File.Exists(cwdPath);

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Compilation.ProjectConfigReader
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                XmlPath = relativePath
            };

            bool result = task.Execute();

            Assert.True(result);
            // Verify the resolved path points to projectDir, not CWD
            Assert.True(trackingEnv.GetAbsolutePathCallCount > 0,
                "Task must use TaskEnvironment.GetAbsolutePath, not resolve against CWD");
            var resolvedPath = trackingEnv.GetAbsolutePath(relativePath);
            Assert.StartsWith(_projectDir, resolvedPath, StringComparison.OrdinalIgnoreCase);

            // Ensure no file was created in CWD as a side effect
            if (!cwdFileExisted)
                Assert.False(File.Exists(cwdPath), "Task should not create files in process CWD");
        }

        [Fact]
        public void ShouldAutoInitProjectDirectoryFromBuildEngine()
        {
            // MockBuildEngine.ProjectFileOfTaskNode returns "test.csproj",
            // which resolves to the process CWD directory.
            var expectedDir = Path.GetDirectoryName(Path.GetFullPath("test.csproj"))!;
            var relativePath = "autoinit-" + Guid.NewGuid().ToString("N")[..8] + ".xml";
            File.WriteAllText(Path.Combine(expectedDir, relativePath), "<root><node/></root>");

            try
            {
                var taskEnv = new TaskEnvironment();
                var task = new SdkTasks.Compilation.ProjectConfigReader
                {
                    BuildEngine = _engine,
                    TaskEnvironment = taskEnv,
                    XmlPath = relativePath
                };

                task.Execute();

                Assert.False(string.IsNullOrEmpty(taskEnv.ProjectDirectory),
                    "Task should auto-initialize ProjectDirectory from BuildEngine when it is empty");
            }
            finally
            {
                File.Delete(Path.Combine(expectedDir, relativePath));
            }
        }
    }
}
