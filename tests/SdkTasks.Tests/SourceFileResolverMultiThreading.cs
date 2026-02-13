using System;
using System.IO;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests
{
    public class SourceFileResolverMultiThreading : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public SourceFileResolverMultiThreading()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ItResolvesRelativePathsViaTaskEnvironment()
        {
            var fileName = "relative-path-test.txt";
            var absolutePath = Path.Combine(_projectDir, fileName);
            File.WriteAllText(absolutePath, "test content");

            var task = new SdkTasks.Compilation.SourceFileResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains(absolutePath));
        }

        [Fact]
        public void ItAutoInitializesProjectDirectoryFromBuildEngine()
        {
            var fileName = "auto-init-test.txt";
            var absolutePath = Path.Combine(_projectDir, fileName);
            File.WriteAllText(absolutePath, "test content");

            _engine.ProjectFileOfTaskNode = Path.Combine(_projectDir, "test.csproj");

            var task = new SdkTasks.Compilation.SourceFileResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = new TaskEnvironment()
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(_projectDir, task.TaskEnvironment.ProjectDirectory);
            Assert.Contains(_engine.Messages, m => m.Message!.Contains(absolutePath));
        }
    }
}
