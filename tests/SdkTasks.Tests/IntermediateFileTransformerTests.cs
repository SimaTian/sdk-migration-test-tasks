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
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Build.IntermediateFileTransformer();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Build.IntermediateFileTransformer),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldAutoInitializeProjectDirectoryFromBuildEngine()
        {
            var projectDir = CreateProjectDir();
            var projectFile = Path.Combine(projectDir, "test.proj");
            File.WriteAllText(projectFile, "<Project/>");
            File.WriteAllText(Path.Combine(projectDir, "input.txt"), "Test content");

            var engine = new MockBuildEngine { ProjectFileOfTaskNode = projectFile };

            var task = new SdkTasks.Build.IntermediateFileTransformer
            {
                BuildEngine = engine,
                TaskEnvironment = new TaskEnvironment(), // Empty TaskEnvironment
                InputFile = "input.txt",
                TransformName = "testxform",
            };

            Assert.True(task.Execute());
            Assert.Contains("Test content", task.TransformedContent);
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
    }
}
