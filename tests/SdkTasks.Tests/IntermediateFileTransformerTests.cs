using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class IntermediateFileTransformerTests : IDisposable
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
        public void ShouldUseIsolatedTempFiles()
        {
            var dir1 = CreateProjectDir();
            var dir2 = CreateProjectDir();
            var transformName = "testxform";

            File.WriteAllText(Path.Combine(dir1, "input.txt"), "Content from project A");
            File.WriteAllText(Path.Combine(dir2, "input.txt"), "Content from project B");

            var engine1 = new MockBuildEngine();
            var engine2 = new MockBuildEngine();

            var task1 = new SdkTasks.Build.IntermediateFileTransformer
            {
                BuildEngine = engine1,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir1),
                InputFile = "input.txt",
                TransformName = transformName,
            };

            var task2 = new SdkTasks.Build.IntermediateFileTransformer
            {
                BuildEngine = engine2,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir2),
                InputFile = "input.txt",
                TransformName = transformName,
            };

            Assert.True(task1.Execute());
            Assert.True(task2.Execute());

            var task1TempMsg = engine1.Messages.FirstOrDefault(m =>
                m.Message?.Contains("Wrote intermediate result") == true);
            var task2TempMsg = engine2.Messages.FirstOrDefault(m =>
                m.Message?.Contains("Wrote intermediate result") == true);
            Assert.NotNull(task1TempMsg);
            Assert.NotNull(task2TempMsg);
            Assert.Contains(dir1, task1TempMsg!.Message!, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(dir2, task2TempMsg!.Message!, StringComparison.OrdinalIgnoreCase);

            Assert.Contains("Content from project A", task1.TransformedContent);
            Assert.Contains("Content from project B", task2.TransformedContent);
        }

        [Fact]
        public void Execute_UsesTaskEnvironmentGetAbsolutePath_ForInputFile()
        {
            var dir = CreateProjectDir();
            File.WriteAllText(Path.Combine(dir, "input.txt"), "Hello world");

            var tracking = SharedTestHelpers.CreateTrackingEnvironment(dir);

            var task = new SdkTasks.Build.IntermediateFileTransformer
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = tracking,
                InputFile = "input.txt",
                TransformName = "track",
            };

            Assert.True(task.Execute());

            // Verify TaskEnvironment.GetAbsolutePath was called for the input file
            SharedTestHelpers.AssertUsesGetAbsolutePath(tracking);
            Assert.Contains("input.txt", tracking.GetAbsolutePathArgs);
        }

        [Fact]
        public void Execute_ResolvesInputFileRelativeToProjectDirectory_NotCwd()
        {
            var dir = CreateProjectDir();
            File.WriteAllText(Path.Combine(dir, "source.txt"), "Source content");

            var task = new SdkTasks.Build.IntermediateFileTransformer
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir),
                InputFile = "source.txt",
                TransformName = "resolve",
            };

            Assert.True(task.Execute());

            // The file was read from the project directory, not the process CWD
            Assert.Contains("Source content", task.TransformedContent);
        }

        [Fact]
        public void Execute_IntermediateFileWrittenUnderProjectDirectory()
        {
            var dir = CreateProjectDir();
            File.WriteAllText(Path.Combine(dir, "data.txt"), "Some data");

            var engine = new MockBuildEngine();

            var task = new SdkTasks.Build.IntermediateFileTransformer
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir),
                InputFile = "data.txt",
                TransformName = "intermed",
            };

            Assert.True(task.Execute());

            Assert.Contains(engine.Messages,
                m => m.Message!.Contains("Wrote intermediate result") && m.Message!.Contains(dir));
        }

        [Fact]
        public void Execute_MissingInputFile_LogsErrorAndReturnsFalse()
        {
            var dir = CreateProjectDir();

            var engine = new MockBuildEngine();

            var task = new SdkTasks.Build.IntermediateFileTransformer
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir),
                InputFile = "nonexistent.txt",
                TransformName = "fail",
            };

            Assert.False(task.Execute());
            Assert.NotEmpty(engine.Errors);
        }

        [Fact]
        public void Execute_TokenReplacementUsesProjectDirectory()
        {
            var dir = CreateProjectDir();
            File.WriteAllText(Path.Combine(dir, "tmpl.txt"), "ProjectDir=$(ProjectDir)");

            var task = new SdkTasks.Build.IntermediateFileTransformer
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir),
                InputFile = "tmpl.txt",
                TransformName = "token",
            };

            Assert.True(task.Execute());

            // $(ProjectDir) token should be replaced with actual project directory
            Assert.Contains(dir, task.TransformedContent);
            Assert.DoesNotContain("$(ProjectDir)", task.TransformedContent);
        }
    }
}
