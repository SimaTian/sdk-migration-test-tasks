using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class BinaryContentWriterTests : IDisposable
    {
        private readonly TaskTestContext _ctx;
        private string _projectDir => _ctx.ProjectDir;
        private MockBuildEngine _engine => _ctx.Engine;

        public BinaryContentWriterTests() => _ctx = new TaskTestContext();
        public void Dispose() => _ctx.Dispose();

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
        public void BinaryContentWriter_UsesGetAbsolutePath_NotForbiddenApis()
        {
            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Resources.BinaryContentWriter
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                OutputPath = "tracked.bin"
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.True(trackingEnv.GetAbsolutePathCallCount > 0,
                "Task must call TaskEnvironment.GetAbsolutePath instead of using Path.GetFullPath or relative paths directly");
            Assert.Contains("tracked.bin", trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void BinaryContentWriter_GetAbsolutePath_ResolvesRelativeToProjectDir()
        {
            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Resources.BinaryContentWriter
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                OutputPath = "resolve-test.bin"
            };

            task.Execute();

            var expectedPath = Path.Combine(_projectDir, "resolve-test.bin");
            Assert.True(File.Exists(expectedPath),
                $"GetAbsolutePath should resolve relative to ProjectDirectory '{_projectDir}', not CWD");

            var cwdPath = Path.Combine(Directory.GetCurrentDirectory(), "resolve-test.bin");
            Assert.False(File.Exists(cwdPath),
                "File must NOT be created under process CWD");
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
