using System;
using System.IO;
using Microsoft.Build.Framework;
using SdkTasks.Resources;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests.Resources
{
    public class BinaryContentWriterMultiThreadingTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly string _cwdBefore;

        public BinaryContentWriterMultiThreadingTests()
        {
            _projectDir = Path.Combine(Path.GetTempPath(), "bcw-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_projectDir);
            _cwdBefore = Directory.GetCurrentDirectory();
        }

        [Fact]
        public void Execute_ResolvesOutputPathRelativeToProjectDirectory_NotCwd()
        {
            var task = new BinaryContentWriter();
            task.BuildEngine = new MockBuildEngine();
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir);
            task.OutputPath = Path.Combine("subdir", "output.bin");

            Directory.CreateDirectory(Path.Combine(_projectDir, "subdir"));

            var result = task.Execute();

            Assert.True(result, "Task should succeed");
            var expectedFile = Path.Combine(_projectDir, "subdir", "output.bin");
            Assert.True(File.Exists(expectedFile),
                $"Output file should exist at '{expectedFile}' (under ProjectDirectory), not under CWD '{_cwdBefore}'");

            var cwdFile = Path.Combine(_cwdBefore, "subdir", "output.bin");
            if (cwdFile != expectedFile)
            {
                Assert.False(File.Exists(cwdFile),
                    "Output file must NOT be created under CWD when ProjectDirectory differs");
            }
        }

        [Fact]
        public void Execute_WritesCorrectContent()
        {
            var task = new BinaryContentWriter();
            task.BuildEngine = new MockBuildEngine();
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir);
            task.OutputPath = "content.bin";

            var result = task.Execute();

            Assert.True(result);
            var outputFile = Path.Combine(_projectDir, "content.bin");
            Assert.True(File.Exists(outputFile));
            var content = File.ReadAllText(outputFile);
            Assert.Equal("Generated output content.", content);
        }

        [Fact]
        public void Execute_EmptyOutputPath_LogsErrorAndReturnsFalse()
        {
            var task = new BinaryContentWriter();
            task.BuildEngine = new MockBuildEngine();
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir);
            task.OutputPath = "";

            var result = task.Execute();

            Assert.False(result, "Task should fail when OutputPath is empty");
        }

        [Fact]
        public void Execute_AbsoluteOutputPath_UsedDirectly()
        {
            var absPath = Path.Combine(_projectDir, "absolute-output.bin");
            var task = new BinaryContentWriter();
            task.BuildEngine = new MockBuildEngine();
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir);
            task.OutputPath = absPath;

            var result = task.Execute();

            Assert.True(result);
            Assert.True(File.Exists(absPath));
        }

        [Fact]
        public void CwdIsNotModifiedByExecution()
        {
            var task = new BinaryContentWriter();
            task.BuildEngine = new MockBuildEngine();
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir);
            task.OutputPath = "cwd-check.bin";

            task.Execute();

            Assert.Equal(_cwdBefore, Directory.GetCurrentDirectory());
        }

        public void Dispose()
        {
            try { Directory.Delete(_projectDir, true); } catch { }
        }
    }
}
