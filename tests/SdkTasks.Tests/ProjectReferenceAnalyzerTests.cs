using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class ProjectReferenceAnalyzerTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public ProjectReferenceAnalyzerTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldUseTaskEnvironmentForPathResolution()
        {
            string projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <ProjectReference Include=""..\Lib\Lib.csproj"" />
  </ItemGroup>
</Project>";

            string srcDir = Path.Combine(_projectDir, "src");
            Directory.CreateDirectory(srcDir);
            File.WriteAllText(Path.Combine(srcDir, "App.csproj"), projectContent);

            string libProjContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <ProjectReference Include=""..\Common\Common.csproj"" />
  </ItemGroup>
</Project>";
            string libDir = Path.Combine(_projectDir, "Lib");
            Directory.CreateDirectory(libDir);
            File.WriteAllText(Path.Combine(libDir, "Lib.csproj"), libProjContent);

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = srcDir };
            var task = new SdkTasks.Analysis.ProjectReferenceAnalyzer
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                ProjectFilePath = "App.csproj",
                ResolveTransitive = true
            };

            task.Execute();

            Assert.NotEmpty(task.AnalyzedReferences);
            Assert.True(trackingEnv.GetAbsolutePathCallCount > 0 || trackingEnv.GetCanonicalFormCallCount > 0,
                "Task should use TaskEnvironment for path resolution");
        }

        [Fact]
        public void ShouldResolveTransitiveReferences()
        {
            string projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <ProjectReference Include=""..\Lib\Lib.csproj"" />
  </ItemGroup>
</Project>";

            string srcDir = Path.Combine(_projectDir, "src");
            Directory.CreateDirectory(srcDir);
            File.WriteAllText(Path.Combine(srcDir, "App.csproj"), projectContent);

            string libProjContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <ProjectReference Include=""..\Common\Common.csproj"" />
  </ItemGroup>
</Project>";
            string libDir = Path.Combine(_projectDir, "Lib");
            Directory.CreateDirectory(libDir);
            File.WriteAllText(Path.Combine(libDir, "Lib.csproj"), libProjContent);

            var taskEnv = TaskEnvironmentHelper.CreateForTest(srcDir);
            var task = new SdkTasks.Analysis.ProjectReferenceAnalyzer
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                ProjectFilePath = "App.csproj",
                ResolveTransitive = true
            };

            task.Execute();

            Assert.NotEmpty(task.AnalyzedReferences);
            var transitiveRef = task.AnalyzedReferences.FirstOrDefault(
                r => r.GetMetadata("IsTransitive") == "True");
            Assert.NotNull(transitiveRef);
            var refPath = transitiveRef!.GetMetadata("ReferencePath");
            if (!string.IsNullOrEmpty(refPath))
                Assert.Contains(_projectDir, refPath, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldResolveProjectReferencesRelativeToProjectDirectory()
        {
            string projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <ProjectReference Include=""..\Lib\Lib.csproj"" />
  </ItemGroup>
</Project>";

            string srcDir = Path.Combine(_projectDir, "src");
            Directory.CreateDirectory(srcDir);
            File.WriteAllText(Path.Combine(srcDir, "App.csproj"), projectContent);

            string libDir = Path.Combine(_projectDir, "Lib");
            Directory.CreateDirectory(libDir);
            File.WriteAllText(Path.Combine(libDir, "Lib.csproj"),
                @"<Project Sdk=""Microsoft.NET.Sdk""></Project>");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(srcDir);
            var task = new SdkTasks.Analysis.ProjectReferenceAnalyzer
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                ProjectFilePath = "App.csproj",
                ResolveTransitive = false
            };

            task.Execute();

            Assert.NotEmpty(task.AnalyzedReferences);
            var directRef = task.AnalyzedReferences.FirstOrDefault(
                r => r.GetMetadata("ReferenceType") == "ProjectReference");
            Assert.NotNull(directRef);
            var refPath = directRef!.GetMetadata("ReferencePath");
            Assert.Contains(_projectDir, refPath, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldDetectProjectType()
        {
            string projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
</Project>";

            File.WriteAllText(Path.Combine(_projectDir, "App.csproj"), projectContent);

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new SdkTasks.Analysis.ProjectReferenceAnalyzer
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                ProjectFilePath = "App.csproj",
                ResolveTransitive = false
            };

            task.Execute();

            Assert.Equal("ConsoleApplication", task.ProjectType);
        }
    }
}
