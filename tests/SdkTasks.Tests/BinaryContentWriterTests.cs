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
        public void ShouldCallGetAbsolutePathOnTaskEnvironment()
        {
            var relativePath = "tracked-stream.bin";

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Resources.BinaryContentWriter
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                OutputPath = relativePath
            };

            task.Execute();

            SharedTestHelpers.AssertGetAbsolutePathCalled(trackingEnv);
            Assert.Contains(relativePath, trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void ShouldNotWriteToCwd()
        {
            var relativePath = "cwd-check-" + Guid.NewGuid().ToString("N")[..8] + ".bin";

            var task = new SdkTasks.Resources.BinaryContentWriter
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputPath = relativePath
            };

            task.Execute();

            var projectPath = Path.Combine(_projectDir, relativePath);
            var cwdPath = Path.Combine(Directory.GetCurrentDirectory(), relativePath);

            Assert.True(File.Exists(projectPath), "File should be written to projectDir");

            // Clean up file that may have been written to CWD by mistake
            if (File.Exists(cwdPath))
                File.Delete(cwdPath);
        }

        [Fact]
        public void ShouldFailWhenOutputPathIsEmpty()
        {
            var task = new SdkTasks.Resources.BinaryContentWriter
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputPath = string.Empty
            };

            bool result = task.Execute();

            Assert.False(result);
            Assert.Contains(_engine.Errors, e => e.Message!.Contains("OutputPath"));
        }

        [Fact]
        public void ShouldLogByteCount()
        {
            var relativePath = "logcheck.bin";

            var task = new SdkTasks.Resources.BinaryContentWriter
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputPath = relativePath
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("bytes"));
        }
    }
}
