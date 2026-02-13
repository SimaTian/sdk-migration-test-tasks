using System;
using System.IO;
using Microsoft.Build.Framework;
using SdkTasks.Analysis;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests
{
    public class GivenADependencyTopologyAnalyzerMultiThreading : IDisposable
    {
        private readonly string _projectDir;
        private readonly string _workDir;

        public GivenADependencyTopologyAnalyzerMultiThreading()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _workDir = TestHelper.CreateNonCwdTempDirectory();
        }

        public void Dispose()
        {
            TestHelper.CleanupTempDirectory(_projectDir);
            TestHelper.CleanupTempDirectory(_workDir);
        }

        [Fact]
        public void ItResolvesRelativePathsAgainstProjectDirectory()
        {
            string projectFile = CreateProjectLayout(_projectDir);
            var engine = new MockBuildEngine { ProjectFileOfTaskNode = projectFile };
            var task = new DependencyTopologyAnalyzer
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                ScanRootDirectory = string.Empty,
                TopologyOutputPath = "topology.dot"
            };

            string originalCwd = Environment.CurrentDirectory;
            Environment.CurrentDirectory = _workDir;
            try
            {
                bool result = task.Execute();

                Assert.True(result);
                Assert.NotEmpty(task.ResolvedBuildOrder);
                Assert.All(task.ResolvedBuildOrder, item =>
                    Assert.StartsWith(_projectDir, item.ItemSpec, StringComparison.OrdinalIgnoreCase));

                string expectedOutputPath = Path.Combine(_projectDir, "topology.dot");
                Assert.True(File.Exists(expectedOutputPath));

                Assert.Contains(engine.Messages, m => m.Message != null &&
                    m.Message.Contains(_projectDir, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                Environment.CurrentDirectory = originalCwd;
            }
        }

        [Fact]
        public void ItInitializesProjectDirectoryWhenMissing()
        {
            string projectFile = CreateProjectLayout(_projectDir);
            var engine = new MockBuildEngine { ProjectFileOfTaskNode = projectFile };
            var task = new DependencyTopologyAnalyzer
            {
                BuildEngine = engine,
                TaskEnvironment = new TaskEnvironment(),
                ScanRootDirectory = string.Empty
            };

            string originalCwd = Environment.CurrentDirectory;
            Environment.CurrentDirectory = _workDir;
            try
            {
                bool result = task.Execute();

                Assert.True(result);
                Assert.Equal(_projectDir, task.TaskEnvironment.ProjectDirectory);
                Assert.NotEmpty(task.ResolvedBuildOrder);
            }
            finally
            {
                Environment.CurrentDirectory = originalCwd;
            }
        }

        [Fact]
        public void ItFailsToInitializeProjectDirectoryCorrectlyWithRelativePathAndCwdChange()
        {
            // This test verifies that we do NOT rely on CWD (via Path.GetFullPath) when resolving ProjectFileOfTaskNode.
            string projectFile = "Relative.csproj";
            var engine = new MockBuildEngine { ProjectFileOfTaskNode = projectFile };
            var task = new DependencyTopologyAnalyzer
            {
                BuildEngine = engine,
                TaskEnvironment = new TaskEnvironment(),
                ScanRootDirectory = string.Empty
            };

            string originalCwd = Environment.CurrentDirectory;
            Environment.CurrentDirectory = _workDir;
            try
            {
                try 
                {
                    task.Execute();
                }
                catch
                {
                    // Ignore exceptions
                }

                if (!string.IsNullOrEmpty(task.TaskEnvironment.ProjectDirectory))
                {
                     Assert.False(task.TaskEnvironment.ProjectDirectory.StartsWith(_workDir, StringComparison.OrdinalIgnoreCase),
                        "ProjectDirectory should not be resolved against the process CurrentDirectory when it is potentially unstable.");
                }
            }
            finally
            {
                Environment.CurrentDirectory = originalCwd;
            }
        }
        
        private static string CreateProjectLayout(string projectDir)
        {
            Directory.CreateDirectory(projectDir);

            string libPath = Path.Combine(projectDir, "Lib.csproj");
            File.WriteAllText(libPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

            string appPath = Path.Combine(projectDir, "App.csproj");
            string appContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <ProjectReference Include=""Lib.csproj"" />
  </ItemGroup>
</Project>";
            File.WriteAllText(appPath, appContent);

            return appPath;
        }
    }
}
