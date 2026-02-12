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
        public void BinaryContentWriter_ResolvesOutputPathRelativeToProjectDirectory()
        {
            var task = new SdkTasks.Resources.BinaryContentWriter
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputPath = Path.Combine("subdir", "output.bin")
            };

            Directory.CreateDirectory(Path.Combine(_projectDir, "subdir"));

            var result = task.Execute();

            Assert.True(result);
            var expectedFile = Path.Combine(_projectDir, "subdir", "output.bin");
            Assert.True(File.Exists(expectedFile),
                $"Output file should exist at '{expectedFile}' (under ProjectDirectory), not under CWD");

            var content = File.ReadAllText(expectedFile);
            Assert.Equal("Generated output content.", content);

            var cwdPath = Path.Combine(Directory.GetCurrentDirectory(), "subdir", "output.bin");
            Assert.False(File.Exists(cwdPath),
                "Output file must NOT be written under CWD");
        }

        [Fact]
        public void BinaryContentWriter_LogMessageDoesNotContainCwdPath()
        {
            var task = new SdkTasks.Resources.BinaryContentWriter
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputPath = "output.bin"
            };

            task.Execute();

            var logText = string.Join(" ", _engine.Messages.Select(m => m.Message));
            var cwd = Directory.GetCurrentDirectory();
            Assert.DoesNotContain(cwd, logText);
        }

        [Fact]
        public void BinaryContentWriter_EmptyOutputPath_LogsErrorAndReturnsFalse()
        {
            var task = new SdkTasks.Resources.BinaryContentWriter
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputPath = ""
            };

            var result = task.Execute();

            Assert.False(result);
            Assert.Contains(_engine.Errors, e => e.Message.Contains("OutputPath is required"));
        }
    }
}
