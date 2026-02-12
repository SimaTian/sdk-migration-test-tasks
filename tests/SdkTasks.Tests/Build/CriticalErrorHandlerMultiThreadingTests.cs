using System;
using System.IO;
using Microsoft.Build.Framework;
using SdkTasks.Build;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests.Build
{
    public class CriticalErrorHandlerMultiThreadingTests : IDisposable
    {
        private readonly string _projectDir;

        public CriticalErrorHandlerMultiThreadingTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
        }

        [Fact]
        public void Execute_NoError_ReturnsTrueWithTaskEnvironment()
        {
            var tracking = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var engine = new MockBuildEngine();
            var task = new CriticalErrorHandler
            {
                BuildEngine = engine,
                TaskEnvironment = tracking,
                ErrorMessage = string.Empty
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.Empty(engine.Errors);
        }

        [Fact]
        public void Execute_WithError_ReturnsFalseAndLogsError()
        {
            var tracking = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var engine = new MockBuildEngine();
            var task = new CriticalErrorHandler
            {
                BuildEngine = engine,
                TaskEnvironment = tracking,
                ErrorMessage = "Out of memory"
            };

            var result = task.Execute();

            Assert.False(result);
            Assert.Contains(engine.Errors, e => e.Message.Contains("Out of memory"));
        }

        [Fact]
        public void Execute_MultipleInstances_IsolatedResults()
        {
            var tracking1 = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var tracking2 = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var engine1 = new MockBuildEngine();
            var engine2 = new MockBuildEngine();

            var task1 = new CriticalErrorHandler
            {
                BuildEngine = engine1,
                TaskEnvironment = tracking1,
                ErrorMessage = "Fatal error in task1"
            };
            var task2 = new CriticalErrorHandler
            {
                BuildEngine = engine2,
                TaskEnvironment = tracking2,
                ErrorMessage = string.Empty
            };

            var result1 = task1.Execute();
            var result2 = task2.Execute();

            Assert.False(result1);
            Assert.True(result2);
            Assert.Contains(engine1.Errors, e => e.Message.Contains("Fatal error in task1"));
            Assert.Empty(engine2.Errors);
        }

        [Fact]
        public void CwdIsNotModifiedByExecution()
        {
            var cwdBefore = Directory.GetCurrentDirectory();
            var task = new CriticalErrorHandler
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                ErrorMessage = "test error"
            };

            task.Execute();

            Assert.Equal(cwdBefore, Directory.GetCurrentDirectory());
        }

        public void Dispose()
        {
            TestHelper.CleanupTempDirectory(_projectDir);
        }
    }
}
