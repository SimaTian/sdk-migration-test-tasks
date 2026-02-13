using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class VersionMetadataUpdaterTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public VersionMetadataUpdaterTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Versioning.VersionMetadataUpdater();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Versioning.VersionMetadataUpdater),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldResolveTargetFilesViaTaskEnvironment()
        {
            string csprojContent = @"<Project>
  <PropertyGroup>
    <Version>1.0.0</Version>
    <AssemblyVersion>1.0.0.0</AssemblyVersion>
    <FileVersion>1.0.0</FileVersion>
  </PropertyGroup>
</Project>";
            string filePath = Path.Combine(_projectDir, "Test.csproj");
            File.WriteAllText(filePath, csprojContent);

            var tracking = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };

            var task = new SdkTasks.Versioning.VersionMetadataUpdater
            {
                BuildEngine = _engine,
                TaskEnvironment = tracking,
                TargetFiles = new ITaskItem[] { new TaskItem("Test.csproj") },
                VersionPrefix = "2.0.0",
                PreserveOriginals = false
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.True(tracking.GetAbsolutePathCallCount > 0, "Should use TaskEnvironment.GetAbsolutePath");
        }

        [Fact]
        public void ShouldUpdateCsprojVersionTags()
        {
            string csprojContent = @"<Project>
  <PropertyGroup>
    <Version>1.0.0</Version>
    <AssemblyVersion>1.0.0.0</AssemblyVersion>
    <FileVersion>1.0.0</FileVersion>
  </PropertyGroup>
</Project>";
            string filePath = Path.Combine(_projectDir, "Test.csproj");
            File.WriteAllText(filePath, csprojContent);

            var task = new SdkTasks.Versioning.VersionMetadataUpdater
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                TargetFiles = new ITaskItem[] { new TaskItem("Test.csproj") },
                VersionPrefix = "3.1.0",
                VersionSuffix = "beta",
                PreserveOriginals = false
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.Single(task.UpdatedFiles);

            string updated = File.ReadAllText(filePath);
            Assert.Contains("<Version>3.1.0-beta</Version>", updated);
            Assert.Contains("<AssemblyVersion>3.1.0.0</AssemblyVersion>", updated);
        }

        [Fact]
        public void ShouldUseBuildLabelFromTaskEnvironment()
        {
            string csprojContent = @"<Project>
  <PropertyGroup>
    <Version>1.0.0</Version>
  </PropertyGroup>
</Project>";
            string filePath = Path.Combine(_projectDir, "Test.csproj");
            File.WriteAllText(filePath, csprojContent);

            var tracking = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };

            var task = new SdkTasks.Versioning.VersionMetadataUpdater
            {
                BuildEngine = _engine,
                TaskEnvironment = tracking,
                TargetFiles = new ITaskItem[] { new TaskItem("Test.csproj") },
                VersionPrefix = "1.0.0",
                PreserveOriginals = false
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.True(tracking.GetEnvironmentVariableCallCount > 0,
                "Should use TaskEnvironment.GetEnvironmentVariable instead of Environment.GetEnvironmentVariable");
        }

        [Fact]
        public void ShouldPreserveOriginals()
        {
            string csprojContent = @"<Project>
  <PropertyGroup>
    <Version>1.0.0</Version>
  </PropertyGroup>
</Project>";
            string filePath = Path.Combine(_projectDir, "Test.csproj");
            File.WriteAllText(filePath, csprojContent);

            var task = new SdkTasks.Versioning.VersionMetadataUpdater
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                TargetFiles = new ITaskItem[] { new TaskItem("Test.csproj") },
                VersionPrefix = "2.0.0",
                PreserveOriginals = true
            };

            var result = task.Execute();

            Assert.True(result);
            Assert.True(File.Exists(Path.Combine(_projectDir, "Test.csproj.bak")));
        }

        [Fact]
        public void ShouldResolvePreservationDirectoryViaTaskEnvironment()
        {
            string csprojContent = @"<Project>
  <PropertyGroup>
    <Version>1.0.0</Version>
  </PropertyGroup>
</Project>";
            string filePath = Path.Combine(_projectDir, "Test.csproj");
            File.WriteAllText(filePath, csprojContent);

            string backupDir = Path.Combine(_projectDir, "backups");
            Directory.CreateDirectory(backupDir);

            var tracking = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };

            var task = new SdkTasks.Versioning.VersionMetadataUpdater
            {
                BuildEngine = _engine,
                TaskEnvironment = tracking,
                TargetFiles = new ITaskItem[] { new TaskItem("Test.csproj") },
                VersionPrefix = "2.0.0",
                PreserveOriginals = true,
                PreservationDirectory = "backups"
            };

            var result = task.Execute();

            Assert.True(result);
            // GetAbsolutePath should be called for both the file and the preservation directory
            Assert.True(tracking.GetAbsolutePathCallCount >= 2);
            Assert.True(File.Exists(Path.Combine(backupDir, "Test.csproj.bak")));
        }
    }
}
