using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Build;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests.Build
{
    public class GivenASafePathResolverMultiThreading : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public GivenASafePathResolverMultiThreading()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ResolvesRelativePathToProjectDirectory_NotCwd()
        {
            // Arrange: create a file ONLY in the project directory, not in CWD
            var relativePath = Path.Combine("subdir", "testfile.txt");
            var expectedAbsPath = Path.Combine(_projectDir, "subdir", "testfile.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(expectedAbsPath)!);
            File.WriteAllText(expectedAbsPath, "some content");

            var task = new SafePathResolver
            {
                BuildEngine = _engine,
                InputPath = relativePath,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
            };

            // Act
            bool result = task.Execute();

            // Assert: task found the file via ProjectDirectory, not CWD
            Assert.True(result);
            // Log message should contain the project-dir-based absolute path and "bytes"
            Assert.Contains(_engine.Messages,
                m => m.Message!.Contains(expectedAbsPath) && m.Message.Contains("bytes"));
        }

        [Fact]
        public void LogsFileNotFound_WhenFileOnlyExistsInCwd()
        {
            // Arrange: create file in CWD but NOT in project directory
            var relativePath = "cwd-only-safepath.txt";
            var cwdFile = Path.Combine(Directory.GetCurrentDirectory(), relativePath);
            bool createdCwdFile = false;
            try
            {
                if (!File.Exists(cwdFile))
                {
                    File.WriteAllText(cwdFile, "cwd content");
                    createdCwdFile = true;
                }

                var task = new SafePathResolver
                {
                    BuildEngine = _engine,
                    InputPath = relativePath,
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
                };

                // Act
                bool result = task.Execute();

                // Assert: task should NOT find the file (it's in CWD, not ProjectDirectory)
                Assert.True(result);
                Assert.Contains(_engine.Messages,
                    m => m.Message!.Contains("but file does not exist"));
            }
            finally
            {
                if (createdCwdFile) File.Delete(cwdFile);
            }
        }

        [Fact]
        public void ReportsFileSizeWhenFileExists()
        {
            // Arrange: create a file with known content in project directory
            var fileName = "sized-file.dat";
            var filePath = Path.Combine(_projectDir, fileName);
            var content = "hello world";
            File.WriteAllText(filePath, content);
            long expectedSize = new FileInfo(filePath).Length;

            var task = new SafePathResolver
            {
                BuildEngine = _engine,
                InputPath = fileName,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
            };

            // Act
            bool result = task.Execute();

            // Assert: log should contain "(<size> bytes)"
            Assert.True(result);
            Assert.Contains(_engine.Messages,
                m => m.Message!.Contains($"({expectedSize} bytes)"));
        }

        [Fact]
        public void HandlesAbsoluteInputPathUnchanged()
        {
            // Arrange: provide an already-absolute path
            var absolutePath = Path.Combine(_projectDir, "absolute-test.txt");
            File.WriteAllText(absolutePath, "absolute content");

            var task = new SafePathResolver
            {
                BuildEngine = _engine,
                InputPath = absolutePath,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
            };

            // Act
            bool result = task.Execute();

            // Assert: absolute path passes through unchanged
            Assert.True(result);
            Assert.Contains(_engine.Messages,
                m => m.Message!.Contains(absolutePath) && m.Message.Contains("bytes"));
        }
    }
}
