using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using SdkTasks.Compilation;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests.Compilation
{
    public class BatchItemProcessorMultiThreadingTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly string _cwdBefore;

        public BatchItemProcessorMultiThreadingTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _cwdBefore = Directory.GetCurrentDirectory();
        }

        [Fact]
        public void Execute_ResolvesRelativePathsToProjectDirectory_NotCwd()
        {
            var relativePaths = new[] { "file1.txt", "subdir/file2.txt" };

            var task = new BatchItemProcessor
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                RelativePaths = relativePaths
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(2, task.AbsolutePaths.Length);
            foreach (var absPath in task.AbsolutePaths)
            {
                Assert.StartsWith(_projectDir, absPath);
            }
            Assert.Equal(Path.Combine(_projectDir, "file1.txt"), task.AbsolutePaths[0]);
        }

        [Fact]
        public void Execute_OutputPathsDoNotContainCwd()
        {
            var relativePaths = new[] { "cwd-check.txt" };

            var task = new BatchItemProcessor
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                RelativePaths = relativePaths
            };

            task.Execute();

            if (_projectDir != _cwdBefore)
            {
                foreach (var absPath in task.AbsolutePaths)
                {
                    Assert.DoesNotContain(_cwdBefore, absPath);
                }
            }
        }

        [Fact]
        public void Execute_CallsGetAbsolutePathOnTaskEnvironment()
        {
            var relativePaths = new[] { "tracked1.txt", "tracked2.txt", "tracked3.txt" };

            var tracking = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new BatchItemProcessor
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = tracking,
                RelativePaths = relativePaths
            };

            task.Execute();

            Assert.Equal(3, tracking.GetAbsolutePathCallCount);
            Assert.Contains("tracked1.txt", tracking.GetAbsolutePathArgs);
            Assert.Contains("tracked2.txt", tracking.GetAbsolutePathArgs);
            Assert.Contains("tracked3.txt", tracking.GetAbsolutePathArgs);
        }

        [Fact]
        public void Execute_LogMessagesContainProjectDirectoryPaths()
        {
            var relativePaths = new[] { "log-check.txt" };

            var engine = new MockBuildEngine();
            var task = new BatchItemProcessor
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                RelativePaths = relativePaths
            };

            task.Execute();

            Assert.Contains(engine.Messages, m =>
                m.Message.Contains("Resolved path:") && m.Message.Contains(_projectDir));
        }

        [Fact]
        public void Execute_LogMessagesDoNotContainCwdPaths()
        {
            var relativePaths = new[] { "no-cwd.txt" };

            var engine = new MockBuildEngine();
            var task = new BatchItemProcessor
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                RelativePaths = relativePaths
            };

            task.Execute();

            if (_projectDir != _cwdBefore)
            {
                var resolvedPathMessages = engine.Messages
                    .Where(m => m.Message.Contains("Resolved path:"))
                    .ToList();
                foreach (var msg in resolvedPathMessages)
                {
                    Assert.DoesNotContain(_cwdBefore, msg.Message);
                }
            }
        }

        [Fact]
        public void Execute_EmptyArray_ReturnsSuccessWithZeroPaths()
        {
            var engine = new MockBuildEngine();
            var task = new BatchItemProcessor
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                RelativePaths = Array.Empty<string>()
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Empty(task.AbsolutePaths);
            Assert.Contains(engine.Messages, m => m.Message.Contains("Resolved 0 paths"));
        }

        [Fact]
        public void Execute_PreservesArrayOrderAndCount()
        {
            var relativePaths = new[] { "z.txt", "a.txt", "m.txt" };

            var task = new BatchItemProcessor
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                RelativePaths = relativePaths
            };

            task.Execute();

            Assert.Equal(3, task.AbsolutePaths.Length);
            Assert.EndsWith("z.txt", task.AbsolutePaths[0]);
            Assert.EndsWith("a.txt", task.AbsolutePaths[1]);
            Assert.EndsWith("m.txt", task.AbsolutePaths[2]);
        }

        [Fact]
        public void CwdIsNotModifiedByExecution()
        {
            var task = new BatchItemProcessor
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                RelativePaths = new[] { "stability.txt" }
            };

            task.Execute();

            Assert.Equal(_cwdBefore, Directory.GetCurrentDirectory());
        }

        public void Dispose()
        {
            TestHelper.CleanupTempDirectory(_projectDir);
        }
    }
}
