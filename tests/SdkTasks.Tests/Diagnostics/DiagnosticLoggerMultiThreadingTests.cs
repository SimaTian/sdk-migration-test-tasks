using System;
using System.IO;
using Microsoft.Build.Framework;
using SdkTasks.Diagnostics;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests.Diagnostics
{
    public class DiagnosticLoggerMultiThreadingTests : IDisposable
    {
        private readonly string _projectDir;

        public DiagnosticLoggerMultiThreadingTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
        }

        [Fact]
        public void Execute_WithNonCwdProjectDir_LogsMessageViaBuildEngine()
        {
            var engine = new MockBuildEngine();
            var task = new DiagnosticLogger
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                Message = "Multi-thread safe message"
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(engine.Messages,
                m => m.Message!.Contains("Multi-thread safe message"));
        }

        [Fact]
        public void Execute_DoesNotWriteToConsole()
        {
            var engine = new MockBuildEngine();
            var task = new DiagnosticLogger
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                Message = "Should not appear on Console"
            };

            var originalOut = Console.Out;
            try
            {
                using var sw = new StringWriter();
                Console.SetOut(sw);

                task.Execute();

                Assert.DoesNotContain("Should not appear on Console", sw.ToString());
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        [Fact]
        public void Execute_NullMessage_LogsEmptyString()
        {
            var engine = new MockBuildEngine();
            var task = new DiagnosticLogger
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                Message = null
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.True(engine.Messages.Count > 0, "Should log at least one message even for null input");
        }

        [Fact]
        public void Execute_WithTrackingEnvironment_Succeeds()
        {
            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var engine = new MockBuildEngine();
            var task = new DiagnosticLogger
            {
                BuildEngine = engine,
                TaskEnvironment = trackingEnv,
                Message = "Tracked"
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(engine.Messages, m => m.Message!.Contains("Tracked"));
        }

        [Fact]
        public void CwdIsNotModifiedByExecution()
        {
            var cwdBefore = Directory.GetCurrentDirectory();
            var task = new DiagnosticLogger
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                Message = "CWD stability check"
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
