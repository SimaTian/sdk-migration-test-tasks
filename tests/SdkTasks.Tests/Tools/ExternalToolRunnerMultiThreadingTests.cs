using System;
using System.IO;
using Microsoft.Build.Framework;
using SdkTasks.Tools;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests.Tools
{
    public class ExternalToolRunnerMultiThreadingTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly string _cwdBefore;

        public ExternalToolRunnerMultiThreadingTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _cwdBefore = Directory.GetCurrentDirectory();
        }

        public void Dispose()
        {
            TestHelper.CleanupTempDirectory(_projectDir);
        }

        [Fact]
        public void Execute_RunsProcessInProjectDirectory_NotCwd()
        {
            var engine = new MockBuildEngine();
            var task = new ExternalToolRunner
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                Command = "cmd.exe",
                Arguments = "/c cd"
            };

            var result = task.Execute();

            Assert.True(result, "Task should succeed");
            var outputMessage = engine.Messages!.Last().Message!;
            Assert.Contains(_projectDir, outputMessage, StringComparison.OrdinalIgnoreCase);
            if (!_projectDir.Equals(_cwdBefore, StringComparison.OrdinalIgnoreCase))
            {
                Assert.DoesNotContain(_cwdBefore, outputMessage, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void Execute_TwoTasksWithDifferentProjectDirs_IsolateCorrectly()
        {
            var dir2 = TestHelper.CreateNonCwdTempDirectory();
            try
            {
                var engine1 = new MockBuildEngine();
                var engine2 = new MockBuildEngine();
                var task1 = new ExternalToolRunner
                {
                    BuildEngine = engine1,
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                    Command = "cmd.exe",
                    Arguments = "/c cd"
                };
                var task2 = new ExternalToolRunner
                {
                    BuildEngine = engine2,
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir2),
                    Command = "cmd.exe",
                    Arguments = "/c cd"
                };

                Assert.True(task1.Execute());
                Assert.True(task2.Execute());

                var output1 = engine1.Messages!.Last().Message!;
                var output2 = engine2.Messages!.Last().Message!;
                Assert.Contains(_projectDir, output1, StringComparison.OrdinalIgnoreCase);
                Assert.Contains(dir2, output2, StringComparison.OrdinalIgnoreCase);
                Assert.NotEqual(output1.Trim(), output2.Trim(), StringComparer.OrdinalIgnoreCase);
            }
            finally
            {
                TestHelper.CleanupTempDirectory(dir2);
            }
        }

        [Fact]
        public void Execute_UsesTaskEnvironmentGetProcessStartInfo()
        {
            var tracking = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var engine = new MockBuildEngine();
            var task = new ExternalToolRunner
            {
                BuildEngine = engine,
                TaskEnvironment = tracking,
                Command = "cmd.exe",
                Arguments = "/c cd"
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.Contains(engine.Messages!, m =>
                m.Message!.Contains(_projectDir, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Execute_FailedProcess_ReturnsFalse()
        {
            var engine = new MockBuildEngine();
            var task = new ExternalToolRunner
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                Command = "cmd.exe",
                Arguments = "/c exit 1"
            };

            var result = task.Execute();

            Assert.False(result, "Task should return false when process exits with non-zero code");
        }

        [Fact]
        public void Execute_AutoInitializesProjectDirectoryFromBuildEngine()
        {
            var projectFile = Path.Combine(_projectDir, "test.csproj");
            File.WriteAllText(projectFile, "<Project />");
            var engine = new MockBuildEngine();
            var task = new ExternalToolRunner
            {
                BuildEngine = engine,
                TaskEnvironment = new TaskEnvironment(),
                Command = "cmd.exe",
                Arguments = "/c cd"
            };

            task.Execute();

            Assert.False(string.IsNullOrEmpty(task.TaskEnvironment.ProjectDirectory),
                "ProjectDirectory should be auto-initialized from BuildEngine.ProjectFileOfTaskNode");
        }

        [Fact]
        public void CwdIsNotModifiedByExecution()
        {
            var engine = new MockBuildEngine();
            var task = new ExternalToolRunner
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                Command = "cmd.exe",
                Arguments = "/c echo ok"
            };

            task.Execute();

            Assert.Equal(_cwdBefore, Directory.GetCurrentDirectory());
        }
    }
}
