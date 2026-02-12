using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class DualPathResolverTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public DualPathResolverTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldResolveBothPathsToProjectDirectory()
        {
            var primaryFile = "primary.txt";
            var secondaryFile = "secondary.txt";
            File.WriteAllText(Path.Combine(_projectDir, primaryFile), "same");
            File.WriteAllText(Path.Combine(_projectDir, secondaryFile), "same");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.DualPathResolver
            {
                BuildEngine = _engine,
                PrimaryPath = primaryFile,
                SecondaryPath = secondaryFile,
                TaskEnvironment = taskEnv
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.True(task.FilesMatch);
        }

        [Fact]
        public void ShouldCallTaskEnvironmentGetAbsolutePathForBothPaths()
        {
            var primaryFile = "p.txt";
            var secondaryFile = "s.txt";
            File.WriteAllText(Path.Combine(_projectDir, primaryFile), "data");
            File.WriteAllText(Path.Combine(_projectDir, secondaryFile), "data");

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Build.DualPathResolver
            {
                BuildEngine = _engine,
                PrimaryPath = primaryFile,
                SecondaryPath = secondaryFile,
                TaskEnvironment = trackingEnv
            };

            task.Execute();

            SharedTestHelpers.AssertGetAbsolutePathCalled(trackingEnv, 2);
        }

        [Fact]
        public void ShouldNotResolveEitherPathRelativeToCwd()
        {
            var primaryFile = "dual-primary.txt";
            var secondaryFile = "dual-secondary.txt";
            // Files only exist in projectDir, not CWD
            File.WriteAllText(Path.Combine(_projectDir, primaryFile), "content");
            File.WriteAllText(Path.Combine(_projectDir, secondaryFile), "content");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.DualPathResolver
            {
                BuildEngine = _engine,
                PrimaryPath = primaryFile,
                SecondaryPath = secondaryFile,
                TaskEnvironment = taskEnv
            };

            task.Execute();

            // Both files should be found and match since they're at projectDir
            Assert.True(task.FilesMatch,
                "Both paths should resolve relative to ProjectDirectory, finding the files");
        }

        [Fact]
        public void ShouldReportFilesDoNotMatchWhenContentDiffers()
        {
            var primaryFile = "match-p.txt";
            var secondaryFile = "match-s.txt";
            File.WriteAllText(Path.Combine(_projectDir, primaryFile), "content-A");
            File.WriteAllText(Path.Combine(_projectDir, secondaryFile), "content-B");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.DualPathResolver
            {
                BuildEngine = _engine,
                PrimaryPath = primaryFile,
                SecondaryPath = secondaryFile,
                TaskEnvironment = taskEnv
            };

            task.Execute();

            Assert.False(task.FilesMatch);
        }

        [Fact]
        public void ShouldLogResolvedPathsContainingProjectDirectory()
        {
            var primaryFile = "log-p.txt";
            var secondaryFile = "log-s.txt";
            File.WriteAllText(Path.Combine(_projectDir, primaryFile), "x");
            File.WriteAllText(Path.Combine(_projectDir, secondaryFile), "x");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Build.DualPathResolver
            {
                BuildEngine = _engine,
                PrimaryPath = primaryFile,
                SecondaryPath = secondaryFile,
                TaskEnvironment = taskEnv
            };

            task.Execute();

            Assert.Contains(_engine.Messages, m =>
                m.Message!.Contains("Primary:") && m.Message!.Contains(_projectDir));
            Assert.Contains(_engine.Messages, m =>
                m.Message!.Contains("Secondary:") && m.Message!.Contains(_projectDir));
        }
    }
}
