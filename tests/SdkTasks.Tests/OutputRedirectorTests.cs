using System;
using System.IO;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests
{
    public class OutputRedirectorTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public OutputRedirectorTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void UsesProjectDirectoryFromBuildEngineWhenProjectDirectoryNotSet()
        {
            var subDir = "logs-" + Guid.NewGuid().ToString("N");
            var relativePath = Path.Combine(subDir, "redirected.log");
            var expectedPath = Path.Combine(_projectDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(expectedPath)!);

            var projectFile = Path.Combine(_projectDir, "test.csproj");
            _engine.ProjectFileOfTaskNode = projectFile;

            var task = new SdkTasks.Diagnostics.OutputRedirector
            {
                BuildEngine = _engine,
                LogFilePath = relativePath,
                TaskEnvironment = new TaskEnvironment()
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.True(File.Exists(expectedPath));
            Assert.Contains(_engine.Messages, m => m.Message!.Contains("Redirected output to log file."));
        }
    }
}
