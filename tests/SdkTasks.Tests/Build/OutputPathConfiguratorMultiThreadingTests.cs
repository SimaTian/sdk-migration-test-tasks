using System.Reflection;
using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Build;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests.Build
{
    public class OutputPathConfiguratorMultiThreadingTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public OutputPathConfiguratorMultiThreadingTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ItResolvesOutputDirectoryRelativeToProjectDirectory()
        {
            var task = new OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputDirectory = "bin\\Release"
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.StartsWith(_projectDir, task.ResolvedOutputDirectory);
            Assert.EndsWith(Path.DirectorySeparatorChar.ToString(), task.ResolvedOutputDirectory);
        }

        [Fact]
        public void ItFallsBackIntermediateToOutputWhenEmpty()
        {
            var task = new OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputDirectory = "bin\\Debug",
                IntermediateDirectory = ""
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.Equal(task.ResolvedOutputDirectory, task.ResolvedIntermediateDirectory);
        }

        [Fact]
        public void ItResolvesProjectReferencesRelativeToProjectDirectory()
        {
            var refItem = new TaskItem("..\\ClassLib\\ClassLib.csproj");
            var task = new OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputDirectory = "bin\\Release",
                ProjectReferences = new ITaskItem[] { refItem }
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.Single(task.ResolvedProjectReferences);
            // Resolved path should be relative to projectDir's parent (.. navigation)
            var parentDir = Directory.GetParent(_projectDir)!.FullName;
            Assert.StartsWith(parentDir, task.ResolvedProjectReferences[0].ItemSpec);
            Assert.Equal("..\\ClassLib\\ClassLib.csproj",
                task.ResolvedProjectReferences[0].GetMetadata("OriginalItemSpec"));
            Assert.Equal("ClassLib",
                task.ResolvedProjectReferences[0].GetMetadata("ProjectName"));
        }

        [Fact]
        public void ItWarnsOnInvalidOutputDirectoryPath()
        {
            var task = new OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputDirectory = "   " // whitespace-only = invalid
            };

            var result = task.Execute();

            Assert.True(result); // warnings, not errors
            Assert.Equal(string.Empty, task.ResolvedOutputDirectory);
        }

        [Fact]
        public void ItPreservesAllPublicProperties()
        {
            var taskSpecificExpected = new[]
            {
                "TaskEnvironment", "OutputDirectory", "IntermediateDirectory",
                "ProjectReferences", "ResolvedOutputDirectory",
                "ResolvedIntermediateDirectory", "ResolvedProjectReferences"
            };

            var actualProperties = typeof(OutputPathConfigurator)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name)
                .ToArray();

            foreach (var expected in taskSpecificExpected)
            {
                Assert.Contains(expected, actualProperties);
            }
        }
    }
}
