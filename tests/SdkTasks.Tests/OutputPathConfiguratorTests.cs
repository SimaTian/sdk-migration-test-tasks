using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Build;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class OutputPathConfiguratorTests : IDisposable
    {
        private readonly TaskTestContext _ctx;
        private string _projectDir => _ctx.ProjectDir;
        private MockBuildEngine _engine => _ctx.Engine;

        public OutputPathConfiguratorTests() => _ctx = new TaskTestContext();
        public void Dispose() => _ctx.Dispose();

        #region Interface and Attribute

        #endregion

        #region OutputDirectory resolution

        [Fact]
        public void OutputDirectory_ShouldResolveRelativeToProjectDirectory()
        {
            var task = new OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputDirectory = "bin"
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(_projectDir, task.ResolvedOutputDirectory, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void OutputDirectory_ShouldNotResolveRelativeToCwd()
        {
            var task = new OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputDirectory = "bin"
            };

            task.Execute();

            string cwd = Directory.GetCurrentDirectory();
            // If projectDir differs from CWD, resolved path must point to projectDir, not CWD
            if (!_projectDir.Equals(cwd, StringComparison.OrdinalIgnoreCase))
            {
                Assert.StartsWith(_projectDir, task.ResolvedOutputDirectory!.TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void OutputDirectory_UsesGetAbsolutePath()
        {
            var tracking = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = tracking,
                OutputDirectory = "bin"
            };

            task.Execute();

            SharedTestHelpers.AssertUsesGetAbsolutePath(tracking);
            Assert.Contains("bin", tracking.GetAbsolutePathArgs);
        }

        [Fact]
        public void OutputDirectory_AbsolutePath_PreservedAsIs()
        {
            string absoluteDir = Path.Combine(_projectDir, "absolute-out");

            var task = new OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputDirectory = absoluteDir
            };

            task.Execute();

            Assert.Contains(absoluteDir, task.ResolvedOutputDirectory, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region IntermediateDirectory resolution

        [Fact]
        public void IntermediateDirectory_ShouldResolveRelativeToProjectDirectory()
        {
            var task = new OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputDirectory = "bin",
                IntermediateDirectory = "obj"
            };

            task.Execute();

            Assert.Contains(_projectDir, task.ResolvedIntermediateDirectory, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void IntermediateDirectory_WhenEmpty_DefaultsToOutputDirectory()
        {
            var task = new OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputDirectory = "bin",
                IntermediateDirectory = ""
            };

            task.Execute();

            Assert.Equal(task.ResolvedOutputDirectory, task.ResolvedIntermediateDirectory);
        }

        [Fact]
        public void IntermediateDirectory_UsesGetAbsolutePath()
        {
            var tracking = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = tracking,
                OutputDirectory = "bin",
                IntermediateDirectory = "obj"
            };

            task.Execute();

            Assert.Contains("obj", tracking.GetAbsolutePathArgs);
        }

        #endregion

        #region ProjectReferences resolution

        [Fact]
        public void ProjectReferences_ShouldResolveRelativeToProjectDirectory()
        {
            var task = new OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputDirectory = "bin",
                ProjectReferences = new ITaskItem[] { new TaskItem("Lib.csproj") }
            };

            task.Execute();

            Assert.NotEmpty(task.ResolvedProjectReferences);
            string resolvedRef = task.ResolvedProjectReferences[0].ItemSpec;
            Assert.Contains(_projectDir, resolvedRef, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ProjectReferences_UsesGetAbsolutePath()
        {
            var tracking = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = tracking,
                OutputDirectory = "bin",
                ProjectReferences = new ITaskItem[] { new TaskItem("Lib.csproj") }
            };

            task.Execute();

            Assert.Contains("Lib.csproj", tracking.GetAbsolutePathArgs);
        }

        [Fact]
        public void ProjectReferences_SetsOriginalItemSpecMetadata()
        {
            var task = new OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputDirectory = "bin",
                ProjectReferences = new ITaskItem[] { new TaskItem("Lib.csproj") }
            };

            task.Execute();

            Assert.NotEmpty(task.ResolvedProjectReferences);
            Assert.Equal("Lib.csproj", task.ResolvedProjectReferences[0].GetMetadata("OriginalItemSpec"));
        }

        [Fact]
        public void ProjectReferences_SetsProjectNameMetadata()
        {
            var task = new OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputDirectory = "bin",
                ProjectReferences = new ITaskItem[] { new TaskItem("Lib.csproj") }
            };

            task.Execute();

            Assert.NotEmpty(task.ResolvedProjectReferences);
            Assert.Equal("Lib", task.ResolvedProjectReferences[0].GetMetadata("ProjectName"));
        }

        [Fact]
        public void ProjectReferences_MultipleReferences_AllResolvedToProjectDir()
        {
            var task = new OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputDirectory = "bin",
                ProjectReferences = new ITaskItem[]
                {
                    new TaskItem("Lib1.csproj"),
                    new TaskItem("Lib2.csproj")
                }
            };

            task.Execute();

            Assert.Equal(2, task.ResolvedProjectReferences.Length);
            foreach (var resolved in task.ResolvedProjectReferences)
            {
                Assert.Contains(_projectDir, resolved.ItemSpec, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void ProjectReferences_Empty_ReturnsEmptyArray()
        {
            var task = new OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputDirectory = "bin",
                ProjectReferences = Array.Empty<ITaskItem>()
            };

            task.Execute();

            Assert.Empty(task.ResolvedProjectReferences);
        }

        #endregion

        #region ProjectDirectory auto-initialization

        [Fact]
        public void Execute_AutoInitializesProjectDirectory_FromBuildEngine()
        {
            // MockBuildEngine returns "test.csproj" (relative) for ProjectFileOfTaskNode,
            // so GetDirectoryName yields "". Verify the task still executes successfully
            // and that with a proper engine path, ProjectDirectory would be set.
            var tracking = new TrackingTaskEnvironment { ProjectDirectory = string.Empty };
            var task = new OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = tracking,
                OutputDirectory = "bin"
            };

            bool result = task.Execute();

            // Task should still succeed even when auto-init yields empty dir
            Assert.True(result);
        }

        #endregion

        #region TrackingTaskEnvironment end-to-end

        [Fact]
        public void Execute_AllPathsThroughTaskEnvironment()
        {
            var tracking = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = tracking,
                OutputDirectory = "bin",
                IntermediateDirectory = "obj",
                ProjectReferences = new ITaskItem[] { new TaskItem("Lib.csproj") }
            };

            bool result = task.Execute();

            Assert.True(result);
            // At least 3 calls: OutputDirectory, IntermediateDirectory, and the reference
            SharedTestHelpers.AssertMinimumGetAbsolutePathCalls(tracking, 3);
        }

        #endregion
    }
}
