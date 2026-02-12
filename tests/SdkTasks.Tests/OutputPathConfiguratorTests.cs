using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class OutputPathConfiguratorTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public OutputPathConfiguratorTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldResolveToProjectDirectory()
        {
            var task = new SdkTasks.Build.OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputDirectory = "bin"
            };

            task.Execute();

            Assert.Contains(_projectDir, task.ResolvedOutputDirectory, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldCallTaskEnvironmentGetAbsolutePathViaUtilityClass()
        {
            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Build.OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                OutputDirectory = "bin"
            };

            task.Execute();

            SharedTestHelpers.AssertGetAbsolutePathCalled(trackingEnv);
        }

        [Fact]
        public void ShouldNotResolveOutputRelativeToCwd()
        {
            var task = new SdkTasks.Build.OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputDirectory = "output"
            };

            task.Execute();

            Assert.NotNull(task.ResolvedOutputDirectory);
            // Resolved path must be under ProjectDirectory, not process CWD
            Assert.Contains(_projectDir, task.ResolvedOutputDirectory!, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Directory.GetCurrentDirectory(), task.ResolvedOutputDirectory!,
                StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldResolveIntermediateDirectoryToProjectDirectory()
        {
            var task = new SdkTasks.Build.OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputDirectory = "bin",
                IntermediateDirectory = "obj"
            };

            task.Execute();

            Assert.NotNull(task.ResolvedIntermediateDirectory);
            Assert.Contains(_projectDir, task.ResolvedIntermediateDirectory!, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldResolveProjectReferencesToProjectDirectory()
        {
            var task = new SdkTasks.Build.OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputDirectory = "bin",
                ProjectReferences = new ITaskItem[]
                {
                    new TaskItem("..\\Lib\\Lib.csproj")
                }
            };

            task.Execute();

            Assert.NotEmpty(task.ResolvedProjectReferences);
            string resolvedRef = task.ResolvedProjectReferences[0].ItemSpec;
            Assert.StartsWith(_projectDir, resolvedRef, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldUseTaskEnvironmentForAllPathResolution()
        {
            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Build.OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                OutputDirectory = "bin",
                IntermediateDirectory = "obj",
                ProjectReferences = new ITaskItem[] { new TaskItem("ref.csproj") }
            };

            task.Execute();

            SharedTestHelpers.AssertGetAbsolutePathCalled(trackingEnv, 3);
        }

        [Fact]
        public void ShouldDefaultIntermediateToOutputWhenNotSpecified()
        {
            var task = new SdkTasks.Build.OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputDirectory = "bin"
            };

            task.Execute();

            Assert.Equal(task.ResolvedOutputDirectory, task.ResolvedIntermediateDirectory);
        }

        [Fact]
        public void ShouldTrackGetAbsolutePathArgsForReferences()
        {
            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Build.OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                OutputDirectory = "bin",
                ProjectReferences = new ITaskItem[]
                {
                    new TaskItem("..\\Lib\\Lib.csproj"),
                    new TaskItem("..\\Common\\Common.csproj")
                }
            };

            task.Execute();

            SharedTestHelpers.AssertGetAbsolutePathCalled(trackingEnv, 3);
            Assert.Contains("bin", trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void ShouldResolveMultipleProjectReferencesToProjectDirectory()
        {
            var task = new SdkTasks.Build.OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputDirectory = "bin",
                ProjectReferences = new ITaskItem[]
                {
                    new TaskItem("..\\Lib\\Lib.csproj"),
                    new TaskItem("src\\App\\App.csproj")
                }
            };

            task.Execute();

            Assert.Equal(2, task.ResolvedProjectReferences.Length);
            foreach (var resolved in task.ResolvedProjectReferences)
            {
                Assert.StartsWith(_projectDir, resolved.ItemSpec, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void ShouldReturnTrueOnSuccessfulExecution()
        {
            var task = new SdkTasks.Build.OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputDirectory = "bin"
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Empty(_engine.Errors);
        }

        [Fact]
        public void ShouldHandleEmptyProjectReferences()
        {
            var task = new SdkTasks.Build.OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputDirectory = "bin",
                ProjectReferences = Array.Empty<ITaskItem>()
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Empty(task.ResolvedProjectReferences);
        }

        [Fact]
        public void ShouldPreserveOriginalItemSpecMetadataOnReferences()
        {
            var task = new SdkTasks.Build.OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                OutputDirectory = "bin",
                ProjectReferences = new ITaskItem[]
                {
                    new TaskItem("..\\Lib\\Lib.csproj")
                }
            };

            task.Execute();

            Assert.NotEmpty(task.ResolvedProjectReferences);
            string originalSpec = task.ResolvedProjectReferences[0].GetMetadata("OriginalItemSpec");
            Assert.Equal("..\\Lib\\Lib.csproj", originalSpec);
        }
    }
}
