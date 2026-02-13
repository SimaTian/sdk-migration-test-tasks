using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class BinaryContentWriterTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public BinaryContentWriterTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldWriteToProjectDirectory()
        {
            var relativePath = "streamout.bin";

            var task = new SdkTasks.Resources.BinaryContentWriter
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputPath = relativePath
            };

            task.Execute();

            var projectPath = Path.Combine(_projectDir, relativePath);
            Assert.True(File.Exists(projectPath), "Task should write to projectDir");
        }

        [Fact]
        public void InitializedProjectDirectoryFromBuildEngine()
        {
            var task = new SdkTasks.Resources.BinaryContentWriter();
            string expectedProjectFile = Path.Combine(_projectDir, "myproject.csproj");

            var buildEngine = new MockBuildEngineWithProjectFile(expectedProjectFile);
            task.BuildEngine = buildEngine;
            task.OutputPath = "out.bin";

            // Execute without explicitly setting TaskEnvironment
            task.Execute();

            Assert.Equal(_projectDir, task.TaskEnvironment.ProjectDirectory);
        }

        private class MockBuildEngineWithProjectFile : MockBuildEngine, IBuildEngine
        {
            private readonly string _projectFile;
            public MockBuildEngineWithProjectFile(string projectFile)
            {
                _projectFile = projectFile;
            }

            string IBuildEngine.ProjectFileOfTaskNode => _projectFile;
        }
    }
}
