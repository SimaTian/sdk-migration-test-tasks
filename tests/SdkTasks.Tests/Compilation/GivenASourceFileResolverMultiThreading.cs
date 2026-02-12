using System.IO;
using Microsoft.Build.Framework;
using SdkTasks.Compilation;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests.Compilation
{
    public class GivenASourceFileResolverMultiThreading : IDisposable
    {
        private readonly string _projectDir;

        public GivenASourceFileResolverMultiThreading()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ItImplementsIMultiThreadableTask()
        {
            var task = new SourceFileResolver();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void ItHasMSBuildMultiThreadableTaskAttribute()
        {
            Assert.NotNull(
                typeof(SourceFileResolver)
                    .GetCustomAttributes(typeof(MSBuildMultiThreadableTaskAttribute), false)
                    .FirstOrDefault());
        }

        [Fact]
        public void ItResolvesRelativePathViaTaskEnvironment()
        {
            var relPath = Path.Combine("subdir", "test.txt");
            var absExpected = Path.Combine(_projectDir, relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absExpected)!);
            File.WriteAllText(absExpected, "hello");

            var engine = new MockBuildEngine();
            var task = new SourceFileResolver
            {
                InputPath = relPath,
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.Contains(engine.Messages, m => m.Message.Contains(absExpected));
            Assert.Contains(engine.Messages, m => m.Message.Contains("File size:"));
        }

        [Fact]
        public void ItDoesNotResolveAgainstCWD()
        {
            var relPath = Path.Combine("unique-sfr-" + Guid.NewGuid().ToString("N"), "file.cs");
            var absUnderProject = Path.Combine(_projectDir, relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absUnderProject)!);
            File.WriteAllText(absUnderProject, "content");

            var absUnderCwd = Path.Combine(Directory.GetCurrentDirectory(), relPath);
            Assert.False(File.Exists(absUnderCwd), "Test setup error: file should not exist under CWD");

            var engine = new MockBuildEngine();
            var task = new SourceFileResolver
            {
                InputPath = relPath,
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.Contains(engine.Messages, m => m.Message.Contains("File size:"));
        }

        [Fact]
        public void ItLogsWarningWhenFileDoesNotExist()
        {
            var engine = new MockBuildEngine();
            var task = new SourceFileResolver
            {
                InputPath = "nonexistent.txt",
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.Contains(engine.Warnings, w => w.Message.Contains("does not exist"));
        }

        [Fact]
        public void ItReturnsErrorWhenInputPathIsEmpty()
        {
            var engine = new MockBuildEngine();
            var task = new SourceFileResolver
            {
                InputPath = "",
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
            };

            var result = task.Execute();

            Assert.False(result);
        }

        [Fact]
        public void ItPreservesAllPublicProperties()
        {
            var actualProps = typeof(SourceFileResolver)
                .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Select(p => p.Name)
                .ToArray();

            Assert.Contains("InputPath", actualProps);
            Assert.Contains("TaskEnvironment", actualProps);
        }
    }
}
