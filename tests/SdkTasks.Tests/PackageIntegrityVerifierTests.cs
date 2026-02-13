using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;
using SdkTasks.Packaging;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class PackageIntegrityVerifierTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public PackageIntegrityVerifierTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose()
        {
            TestHelper.CleanupTempDirectory(_projectDir);
        }

        [Fact]
        public void ImplementsIMultiThreadableTask()
        {
            var task = new PackageIntegrityVerifier();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(PackageIntegrityVerifier),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldResolvePathsRelativeToProjectDirectory()
        {
            // Arrange
            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            
            // Create a fake package structure in the project directory
            string localCacheDir = Path.Combine(_projectDir, "local-packages");
            string packageId = "TestPackage";
            string version = "1.0.0";
            string packageDir = Path.Combine(localCacheDir, packageId.ToLowerInvariant(), version);
            string libDir = Path.Combine(packageDir, "lib", "netstandard2.0");
            
            Directory.CreateDirectory(libDir);
            File.WriteAllText(Path.Combine(libDir, "TestPackage.dll"), "fake dll content");
            
            // Create nuspec
            string nuspecContent = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
  <metadata>
    <id>{packageId}</id>
    <version>{version}</version>
  </metadata>
</package>";
            File.WriteAllText(Path.Combine(packageDir, $"{packageId.ToLowerInvariant()}.nuspec"), nuspecContent);

            var pkgItem = new TaskItem(packageId);
            pkgItem.SetMetadata("Version", version);

            var task = new PackageIntegrityVerifier
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                DeclaredPackages = new[] { pkgItem },
                PackageCacheDirectory = "local-packages", // Relative path!
                TargetFramework = "netstandard2.0"
            };

            // Act
            bool result = task.Execute();

            // Assert
            Assert.True(result, "Task should succeed");
            Assert.Empty(task.UnresolvedPackages);
            
            // Check verification report path - should be in project dir
            Assert.False(string.IsNullOrEmpty(task.VerificationReport));
            Assert.StartsWith(_projectDir, task.VerificationReport);
            Assert.True(File.Exists(task.VerificationReport));
        }

        [Fact]
        public void ShouldUseTaskEnvironmentForEnvironmentVariables()
        {
            // Arrange
            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            string customCacheDir = Path.Combine(_projectDir, "custom-nuget-cache");
            Directory.CreateDirectory(customCacheDir);

            // Set environment variable via TaskEnvironment
            taskEnv.SetEnvironmentVariable("NUGET_PACKAGES", customCacheDir);

            // Create a package in the custom cache
            string packageId = "EnvPackage";
            string version = "2.0.0";
            string packageDir = Path.Combine(customCacheDir, packageId.ToLowerInvariant(), version);
            Directory.CreateDirectory(Path.Combine(packageDir, "lib", "net6.0"));
            File.WriteAllText(Path.Combine(packageDir, "lib", "net6.0", "EnvPackage.dll"), "dll");
            File.WriteAllText(Path.Combine(packageDir, $"{packageId.ToLowerInvariant()}.nuspec"), 
                $@"<package><metadata><id>{packageId}</id><version>{version}</version></metadata></package>");

            var pkgItem = new TaskItem(packageId);
            pkgItem.SetMetadata("Version", version);

            var task = new PackageIntegrityVerifier
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                DeclaredPackages = new[] { pkgItem },
                PackageCacheDirectory = "", // Empty to trigger default resolution
                TargetFramework = "net6.0"
            };

            // Act
            bool result = task.Execute();

            // Assert
            Assert.True(result);
            Assert.Empty(task.UnresolvedPackages);
        }
    }
}
