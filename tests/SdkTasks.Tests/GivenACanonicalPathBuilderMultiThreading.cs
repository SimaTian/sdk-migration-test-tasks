using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Build;
using SdkTasks.Tests.Infrastructure;
using System;
using System.IO;
using Xunit;

namespace SdkTasks.Tests
{
    public class GivenACanonicalPathBuilderMultiThreading : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public GivenACanonicalPathBuilderMultiThreading()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ItResolvesRelativePathsViaTaskEnvironment()
        {
            var fileName = "relative/path/file.txt";
            var expectedAbsPath = Path.Combine(_projectDir, "relative", "path", "file.txt");
            
            Directory.CreateDirectory(Path.GetDirectoryName(expectedAbsPath)!);
            File.WriteAllText(expectedAbsPath, "test");

            var task = new CanonicalPathBuilder
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
            };

            bool result = task.Execute();

            result.Should().BeTrue();
            task.CanonicalPath.Should().NotBeNullOrEmpty();
            task.CanonicalPath.Should().StartWith(_projectDir, "Canonical output path should be rooted in ProjectDirectory"); 
        }
    }
}
