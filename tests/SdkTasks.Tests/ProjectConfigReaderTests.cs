using System.Xml.Linq;
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
        public void Execute_EmptyXmlPath_LogsErrorAndReturnsFalse()
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
        public void Execute_ShouldResolveRelativeToProjectDirectory()
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
        public void Execute_UsesGetAbsolutePath_NotDirectPathResolution()
        {
            var relativePath = "config.xml";
            File.WriteAllText(Path.Combine(_projectDir, relativePath), "<root><child/></root>");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new SdkTasks.Compilation.ProjectConfigReader
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                XmlPath = relativePath
            };

            task.Execute();

            SharedTestHelpers.AssertUsesGetAbsolutePath(trackingEnv);
            Assert.Contains(relativePath, trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void Execute_ResolvedPathContainsProjectDirectory()
        {
            var relativePath = "project.xml";
            File.WriteAllText(Path.Combine(_projectDir, relativePath), "<root><node/></root>");

            var task = new SdkTasks.Compilation.ProjectConfigReader
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                XmlPath = relativePath
            };

            bool result = task.Execute();

            Assert.True(result);
            // Verify the file was modified in the project directory, not CWD
            var savedFile = Path.Combine(_projectDir, relativePath);
            var doc = XDocument.Load(savedFile);
            Assert.Equal("true", doc.Root?.Attribute("processed")?.Value);
        }

        [Fact]
        public void Execute_DoesNotResolveRelativeToCwd()
        {
            var relativePath = "nocwd.xml";
            var projectFilePath = Path.Combine(_projectDir, relativePath);
            File.WriteAllText(projectFilePath, "<root><element/></root>");

            // Ensure the file does NOT exist in CWD
            var cwdPath = Path.Combine(Directory.GetCurrentDirectory(), relativePath);
            Assert.False(File.Exists(cwdPath),
                "Test precondition: file must not exist in CWD");

            var task = new SdkTasks.Compilation.ProjectConfigReader
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                XmlPath = relativePath
            };

            bool result = task.Execute();

            // Task should succeed by resolving relative to ProjectDirectory
            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("Loaded XML with"));
        }

        [Fact]
        public void Execute_WithSubdirectoryPath_ResolvesRelativeToProjectDirectory()
        {
            var subdir = "configs";
            Directory.CreateDirectory(Path.Combine(_projectDir, subdir));
            var relativePath = Path.Combine(subdir, "settings.xml");
            File.WriteAllText(Path.Combine(_projectDir, relativePath), "<settings><key>value</key></settings>");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new SdkTasks.Compilation.ProjectConfigReader
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                XmlPath = relativePath
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.True(trackingEnv.GetAbsolutePathCallCount > 0);
            // Verify the saved file is in the project directory subtree
            var savedFile = Path.Combine(_projectDir, relativePath);
            var doc = XDocument.Load(savedFile);
            Assert.Equal("true", doc.Root?.Attribute("processed")?.Value);
        }
    }
}
