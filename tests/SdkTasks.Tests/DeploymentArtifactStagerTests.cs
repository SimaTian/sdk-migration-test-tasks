using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;
using SdkTasks.Deployment;

namespace SdkTasks.Tests
{
    public class DeploymentArtifactStagerTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public DeploymentArtifactStagerTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            var task = new DeploymentArtifactStager();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(DeploymentArtifactStager),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldResolveRelativePathsViaTaskEnvironment()
        {
            // Create input directory with a .dll file
            var inputDir = Path.Combine(_projectDir, "inputbin");
            Directory.CreateDirectory(inputDir);
            File.WriteAllText(Path.Combine(inputDir, "MyLib.dll"), "fake-dll");

            var stagingDir = Path.Combine(_projectDir, "staging");
            var inventoryPath = Path.Combine(_projectDir, "inventory.json");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new DeploymentArtifactStager
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                InputDirectories = new ITaskItem[] { new TaskItem("inputbin") },
                StagingDirectory = "staging",
                InventoryPath = "inventory.json"
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(1, task.TotalFilesStaged);
            Assert.True(File.Exists(Path.Combine(stagingDir, "MyLib.dll")));
            Assert.True(File.Exists(inventoryPath));
        }

        [Fact]
        public void ShouldCallGetAbsolutePathOnTaskEnvironment()
        {
            var inputDir = Path.Combine(_projectDir, "trackbin");
            Directory.CreateDirectory(inputDir);
            File.WriteAllText(Path.Combine(inputDir, "Test.dll"), "fake");

            var tracking = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new DeploymentArtifactStager
            {
                BuildEngine = _engine,
                TaskEnvironment = tracking,
                InputDirectories = new ITaskItem[] { new TaskItem("trackbin") },
                StagingDirectory = "trackstaging",
                InventoryPath = "trackinventory.json"
            };

            task.Execute();

            Assert.True(tracking.GetAbsolutePathCallCount > 0,
                "Task must call TaskEnvironment.GetAbsolutePath instead of Path.GetFullPath");
        }

        [Fact]
        public void ShouldResolveStagingDirRelativeToProjectDir()
        {
            var inputDir = Path.Combine(_projectDir, "srcbin");
            Directory.CreateDirectory(inputDir);
            File.WriteAllText(Path.Combine(inputDir, "App.dll"), "content");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new DeploymentArtifactStager
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                InputDirectories = new ITaskItem[] { new TaskItem("srcbin") },
                StagingDirectory = "output",
                InventoryPath = "inv.json"
            };

            task.Execute();

            // Staging dir should be under _projectDir, not CWD
            Assert.True(Directory.Exists(Path.Combine(_projectDir, "output")));
            Assert.True(File.Exists(Path.Combine(_projectDir, "output", "App.dll")));
        }

        [Fact]
        public void ShouldResolveInventoryPathRelativeToProjectDir()
        {
            var inputDir = Path.Combine(_projectDir, "invbin");
            Directory.CreateDirectory(inputDir);
            File.WriteAllText(Path.Combine(inputDir, "Lib.xml"), "xml-content");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new DeploymentArtifactStager
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                InputDirectories = new ITaskItem[] { new TaskItem("invbin") },
                StagingDirectory = "invstage",
                InventoryPath = "sub/inventory.json"
            };

            task.Execute();

            Assert.True(File.Exists(Path.Combine(_projectDir, "sub", "inventory.json")));
        }

        [Fact]
        public void ShouldHandleMissingInputDirectory()
        {
            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new DeploymentArtifactStager
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                InputDirectories = new ITaskItem[] { new TaskItem("nonexistent") },
                StagingDirectory = "staging",
                InventoryPath = "inv.json"
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(0, task.TotalFilesStaged);
            Assert.Contains(_engine.Warnings, w => w.Message!.Contains("does not exist"));
        }

        [Fact]
        public void ShouldFilterByAllowedExtensions()
        {
            var inputDir = Path.Combine(_projectDir, "filterbin");
            Directory.CreateDirectory(inputDir);
            File.WriteAllText(Path.Combine(inputDir, "Keep.dll"), "keep");
            File.WriteAllText(Path.Combine(inputDir, "Skip.txt"), "skip");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            var task = new DeploymentArtifactStager
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                InputDirectories = new ITaskItem[] { new TaskItem("filterbin") },
                StagingDirectory = "filterstage",
                InventoryPath = "filterinv.json",
                AllowedExtensions = ".dll"
            };

            task.Execute();

            Assert.Equal(1, task.TotalFilesStaged);
            Assert.True(File.Exists(Path.Combine(_projectDir, "filterstage", "Keep.dll")));
            Assert.False(File.Exists(Path.Combine(_projectDir, "filterstage", "Skip.txt")));
        }
    }
}
