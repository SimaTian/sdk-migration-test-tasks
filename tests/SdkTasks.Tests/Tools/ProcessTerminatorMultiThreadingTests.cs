using System;
using System.IO;
using Microsoft.Build.Framework;
using SdkTasks.Tools;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests.Tools
{
    public class ProcessTerminatorMultiThreadingTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly string _cwdBefore;

        public ProcessTerminatorMultiThreadingTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _cwdBefore = Directory.GetCurrentDirectory();
        }

        [Fact]
        public void Execute_AlwaysReturnsFalse_InNonCwdContext()
        {
            var engine = new MockBuildEngine();
            var task = new ProcessTerminator
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
            };

            bool result = task.Execute();

            Assert.False(result);
        }

        [Fact]
        public void Execute_LogsForbiddenError_InNonCwdContext()
        {
            var engine = new MockBuildEngine();
            var task = new ProcessTerminator
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
            };

            task.Execute();

            Assert.Single(engine.Errors);
            Assert.Contains(engine.Errors,
                e => e.Message.Contains("kill", StringComparison.OrdinalIgnoreCase)
                  && e.Message.Contains("forbidden", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Execute_LogsProcessInfo()
        {
            var engine = new MockBuildEngine();
            var task = new ProcessTerminator
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
            };

            task.Execute();

            Assert.Contains(engine.Messages,
                m => m.Message.Contains("cleanup", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(engine.Messages,
                m => m.Message.Contains("PID:", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Execute_MultipleInstances_IsolatedResults()
        {
            var engine1 = new MockBuildEngine();
            var engine2 = new MockBuildEngine();

            var task1 = new ProcessTerminator
            {
                BuildEngine = engine1,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
            };
            var task2 = new ProcessTerminator
            {
                BuildEngine = engine2,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
            };

            bool result1 = task1.Execute();
            bool result2 = task2.Execute();

            Assert.False(result1);
            Assert.False(result2);
            Assert.Single(engine1.Errors);
            Assert.Single(engine2.Errors);
        }

        [Fact]
        public void Execute_LogMessagesDoNotContainCwdPaths()
        {
            var engine = new MockBuildEngine();
            var task = new ProcessTerminator
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
            };

            task.Execute();

            var cwd = Environment.CurrentDirectory;
            foreach (var msg in engine.Messages)
            {
                Assert.DoesNotContain(cwd + Path.DirectorySeparatorChar, msg.Message ?? "");
            }
            foreach (var err in engine.Errors)
            {
                Assert.DoesNotContain(cwd + Path.DirectorySeparatorChar, err.Message ?? "");
            }
        }

        [Fact]
        public void CwdIsNotModifiedByExecution()
        {
            var task = new ProcessTerminator
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
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
