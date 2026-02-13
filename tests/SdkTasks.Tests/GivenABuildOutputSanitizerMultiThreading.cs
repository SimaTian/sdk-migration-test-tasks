using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Cleanup;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests
{
    public class GivenABuildOutputSanitizerMultiThreading : IDisposable
    {
        private readonly List<string> _tempDirs = new();

        private string CreateProjectDir()
        {
            var dir = TestHelper.CreateNonCwdTempDirectory();
            _tempDirs.Add(dir);
            return dir;
        }

        public void Dispose()
        {
            foreach (var dir in _tempDirs)
                TestHelper.CleanupTempDirectory(dir);
        }

        [Fact]
        public void ItResolvesTargetDirectoriesViaTaskEnvironment()
        {
            var projectDir = CreateProjectDir();
            var relativeDir = "sanitize-" + Guid.NewGuid().ToString("N");
            var targetDir = Path.Combine(projectDir, relativeDir);
            Directory.CreateDirectory(targetDir);
            var filePath = Path.Combine(targetDir, "temp.txt");
            File.WriteAllText(filePath, "data");

            var task = new BuildOutputSanitizer
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                TargetDirectories = new ITaskItem[] { new TaskItem(relativeDir) },
                RetainPatterns = string.Empty,
                ManageLockedFiles = false,
                AggressiveClean = false
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(1, task.RemovedFiles);
            Assert.Equal(0, task.RetainedFiles);
            Assert.False(File.Exists(filePath));
        }

        [Fact]
        public void ItInitializesProjectDirectoryFromBuildEngine()
        {
            var projectDir = CreateProjectDir();
            var relativeDir = "sanitize-" + Guid.NewGuid().ToString("N");
            var targetDir = Path.Combine(projectDir, relativeDir);
            Directory.CreateDirectory(targetDir);
            var filePath = Path.Combine(targetDir, "temp.txt");
            File.WriteAllText(filePath, "data");

            var engine = new MockBuildEngine
            {
                ProjectFileOfTaskNode = Path.Combine(projectDir, "test.csproj")
            };

            var task = new BuildOutputSanitizer
            {
                BuildEngine = engine,
                TaskEnvironment = new TaskEnvironment(),
                TargetDirectories = new ITaskItem[] { new TaskItem(relativeDir) },
                RetainPatterns = string.Empty,
                ManageLockedFiles = false,
                AggressiveClean = false
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(projectDir, task.TaskEnvironment.ProjectDirectory);
            Assert.Equal(1, task.RemovedFiles);
        }

        [Fact]
        public void ItUsesTaskEnvironmentCanonicalForm()
        {
            var projectDir = CreateProjectDir();
            var relativeDir = "sanitize-" + Guid.NewGuid().ToString("N");
            var targetDir = Path.Combine(projectDir, relativeDir);
            Directory.CreateDirectory(targetDir);
            var filePath = Path.Combine(targetDir, "temp.txt");
            File.WriteAllText(filePath, "data");

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = projectDir };
            var task = new BuildOutputSanitizer
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = trackingEnv,
                TargetDirectories = new ITaskItem[] { new TaskItem(relativeDir) },
                RetainPatterns = string.Empty,
                ManageLockedFiles = false,
                AggressiveClean = false
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.True(trackingEnv.GetCanonicalFormCallCount > 0);
            Assert.Contains(relativeDir, trackingEnv.GetCanonicalFormArgs);
        }
    }
}
