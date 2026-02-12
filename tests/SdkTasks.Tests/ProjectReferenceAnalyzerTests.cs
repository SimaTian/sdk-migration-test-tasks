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
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Analysis.ProjectReferenceAnalyzer();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Analysis.ProjectReferenceAnalyzer),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldResolveToProjectDirectory()
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

            var taskEnv = new TrackingTaskEnvironment { ProjectDirectory = srcDir };
            var task = new SdkTasks.Analysis.ProjectReferenceAnalyzer
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                ProjectFilePath = "App.csproj",
                ResolveTransitive = true
            };

            task.Execute();

            Assert.NotEmpty(task.AnalyzedReferences);
            Assert.True(taskEnv.GetAbsolutePathCallCount > 0 || taskEnv.GetCanonicalFormCallCount > 0,
                "Task should use TaskEnvironment for path resolution");
        }
    }
}
