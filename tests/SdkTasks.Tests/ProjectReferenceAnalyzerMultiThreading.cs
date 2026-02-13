using System;
using System.IO;
using Microsoft.Build.Framework;
using SdkTasks.Analysis;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests
{
    public class ProjectReferenceAnalyzerMultiThreading : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public ProjectReferenceAnalyzerMultiThreading()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ItAutoInitializesProjectDirectoryFromBuildEngine()
        {
            string projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <ProjectReference Include=""..\Lib\Lib.csproj"" />
  </ItemGroup>
</Project>";

            string srcDir = Path.Combine(_projectDir, "src");
            Directory.CreateDirectory(srcDir);
            string projectFile = Path.Combine(srcDir, "App.csproj");
            File.WriteAllText(projectFile, projectContent);

            string libDir = Path.Combine(_projectDir, "Lib");
            Directory.CreateDirectory(libDir);
            File.WriteAllText(Path.Combine(libDir, "Lib.csproj"), "<Project />");

            _engine.ProjectFileOfTaskNode = projectFile;

            var task = new ProjectReferenceAnalyzer
            {
                BuildEngine = _engine,
                TaskEnvironment = new TaskEnvironment(),
                ProjectFilePath = "App.csproj"
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(srcDir, task.TaskEnvironment.ProjectDirectory);
            Assert.Single(task.AnalyzedReferences);
            string resolvedPath = task.AnalyzedReferences[0].GetMetadata("ReferencePath");
            string expectedPath = Path.Combine(_projectDir, "Lib", "Lib.csproj");
            Assert.Equal(Path.GetFullPath(expectedPath), Path.GetFullPath(resolvedPath));
        }
    }
}
