using System.IO;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using SdkTasks.Compilation;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests.Compilation
{
    public class ProjectConfigReaderMultiThreadingTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public ProjectConfigReaderMultiThreadingTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ItDoesNotResolveAgainstCWD()
        {
            var relPath = "unique-pcr-" + Guid.NewGuid().ToString("N") + ".xml";
            var absUnderProject = Path.Combine(_projectDir, relPath);
            File.WriteAllText(absUnderProject, "<root><item/></root>");

            var absUnderCwd = Path.Combine(Directory.GetCurrentDirectory(), relPath);
            Assert.False(File.Exists(absUnderCwd), "Test setup error: file should not exist under CWD");

            var task = new ProjectConfigReader
            {
                XmlPath = relPath,
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
            };

            var result = task.Execute();

            Assert.True(result);
        }

        [Fact]
        public void ItModifiesXmlFileUnderProjectDirectory()
        {
            var relPath = "config.xml";
            var absPath = Path.Combine(_projectDir, relPath);
            File.WriteAllText(absPath, "<root><item/></root>");

            var task = new ProjectConfigReader
            {
                XmlPath = relPath,
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
            };

            task.Execute();

            var doc = XDocument.Load(absPath);
            Assert.Equal("true", doc.Root?.Attribute("processed")?.Value);
        }

        [Fact]
        public void ItReturnsErrorWhenXmlPathIsEmpty()
        {
            var task = new ProjectConfigReader
            {
                XmlPath = "",
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
            };

            var result = task.Execute();

            Assert.False(result);
            Assert.Contains(_engine.Errors, e => e.Message!.Contains("XmlPath is required"));
        }

        [Fact]
        public void ItPreservesAllPublicProperties()
        {
            var props = typeof(ProjectConfigReader)
                .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Select(p => p.Name)
                .ToArray();

            Assert.Contains("XmlPath", props);
            Assert.Contains("TaskEnvironment", props);
        }

        [Fact]
        public void ItLogsElementCountFromProjectDirFile()
        {
            var relPath = "elements.xml";
            File.WriteAllText(Path.Combine(_projectDir, relPath),
                "<root><a/><b/><c/></root>");

            var task = new ProjectConfigReader
            {
                XmlPath = relPath,
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
            };

            task.Execute();

            // root + a + b + c = 4 descendants
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("4 elements"));
        }
    }
}
