// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Packaging;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class PackageIntegrityCheckerTests : IDisposable
    {
        private readonly TaskTestContext _ctx;
        private string _tempDir => _ctx.ProjectDir;
        private MockBuildEngine _engine => _ctx.Engine;

        public PackageIntegrityCheckerTests() => _ctx = new TaskTestContext();
        public void Dispose() => _ctx.Dispose();

        #region Path resolution via TaskEnvironment

        [Fact]
        public void Execute_PackagesDirectory_ShouldResolveRelativeToProjectDirectory()
        {
            // Create a packages directory only under _tempDir (not CWD)
            string pkgDir = Path.Combine(_tempDir, "packages", "fakepackage", "1.0.0", "lib");
            Directory.CreateDirectory(pkgDir);

            var pkg = new TaskItem("FakePackage");
            pkg.SetMetadata("Version", "1.0.0");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_tempDir);

            var task = new PackageIntegrityChecker
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                PackagesDirectory = "packages",
                PackagesToValidate = new ITaskItem[] { pkg }
            };

            bool result = task.Execute();

            // Verify TaskEnvironment.GetAbsolutePath was called for PackagesDirectory
            SharedTestHelpers.AssertUsesGetAbsolutePath(trackingEnv);
            Assert.Contains("packages", trackingEnv.GetAbsolutePathArgs);

            // Package should be found under _tempDir, not CWD
            Assert.True(result);
            Assert.NotEmpty(task.ValidatedPackages);
        }

        [Fact]
        public void Execute_NuGetConfigPath_ShouldResolveRelativeToProjectDirectory()
        {
            // Create a NuGet config with a repository path under _tempDir
            string configContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <config>
    <add key=""repositoryPath"" value=""custom-packages"" />
  </config>
</configuration>";

            File.WriteAllText(Path.Combine(_tempDir, "nuget.config"), configContent);

            // Create a package under the custom-packages folder
            string customPkgDir = Path.Combine(_tempDir, "custom-packages", "testpkg", "2.0.0", "lib");
            Directory.CreateDirectory(customPkgDir);

            var pkg = new TaskItem("TestPkg");
            pkg.SetMetadata("Version", "2.0.0");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_tempDir);

            var task = new PackageIntegrityChecker
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                PackagesDirectory = "nonexistent-local",
                NuGetConfigPath = "nuget.config",
                PackagesToValidate = new ITaskItem[] { pkg }
            };

            bool result = task.Execute();

            // Verify GetAbsolutePath was called for both PackagesDirectory and NuGetConfigPath
            SharedTestHelpers.AssertMinimumGetAbsolutePathCalls(trackingEnv, 2);
            Assert.Contains("nuget.config", trackingEnv.GetAbsolutePathArgs);

            // Package found via config repository path should resolve under _tempDir
            Assert.True(result);
            Assert.NotEmpty(task.ValidatedPackages);
        }

        #endregion

        #region Environment variable access via TaskEnvironment

        [Fact]
        public void Execute_ShouldUseTaskEnvironmentForNuGetPackagesEnvVar()
        {
            // Set NUGET_PACKAGES only in TaskEnvironment (not process env)
            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_tempDir);
            string nugetPkgDir = Path.Combine(_tempDir, "nuget-global");
            trackingEnv.SetEnvironmentVariable("NUGET_PACKAGES", nugetPkgDir);

            // Create the package only in the TaskEnvironment-scoped global packages folder
            string packageDir = Path.Combine(nugetPkgDir, "envpkg", "3.0.0", "lib");
            Directory.CreateDirectory(packageDir);

            var pkg = new TaskItem("EnvPkg");
            pkg.SetMetadata("Version", "3.0.0");

            var task = new PackageIntegrityChecker
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                PackagesDirectory = "nonexistent-local",
                PackagesToValidate = new ITaskItem[] { pkg }
            };

            bool result = task.Execute();

            // Verify TaskEnvironment.GetEnvironmentVariable was called
            SharedTestHelpers.AssertUsesGetEnvironmentVariable(trackingEnv);

            // Package should be found via the TaskEnvironment env var path
            Assert.True(result);
            Assert.NotEmpty(task.ValidatedPackages);
        }

        [Fact]
        public void Execute_ShouldFallBackToUserProfileViaTaskEnvironment()
        {
            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_tempDir);

            // Ensure NUGET_PACKAGES is not set (force fallback to USERPROFILE/.nuget/packages)
            trackingEnv.SetEnvironmentVariable("NUGET_PACKAGES", null);
            trackingEnv.SetEnvironmentVariable("USERPROFILE", _tempDir);
            trackingEnv.SetEnvironmentVariable("HOME", _tempDir);

            // Create the package under the task-scoped global packages folder
            string globalPkgDir = Path.Combine(_tempDir, ".nuget", "packages");
            string packageDir = Path.Combine(globalPkgDir, "profilepkg", "1.0.0", "lib");
            Directory.CreateDirectory(packageDir);

            var pkg = new TaskItem("ProfilePkg");
            pkg.SetMetadata("Version", "1.0.0");

            var task = new PackageIntegrityChecker
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                PackagesDirectory = "nonexistent-local",
                PackagesToValidate = new ITaskItem[] { pkg }
            };

            bool result = task.Execute();

            // Verify TaskEnvironment was used for environment variable access
            SharedTestHelpers.AssertMinimumGetEnvironmentVariableCalls(trackingEnv, 2);

            // Package should be found via the USERPROFILE-based global folder
            Assert.True(result);
            Assert.NotEmpty(task.ValidatedPackages);
        }

        #endregion

        #region Validation output

        [Fact]
        public void Execute_InvalidPackage_ShouldPopulateInvalidPackagesOutput()
        {
            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_tempDir);

            // Create a packages directory but no actual package in it
            string packagesDir = Path.Combine(_tempDir, "empty-packages");
            Directory.CreateDirectory(packagesDir);

            var pkg = new TaskItem("MissingPackage");
            pkg.SetMetadata("Version", "1.0.0");

            var task = new PackageIntegrityChecker
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                PackagesDirectory = "empty-packages",
                PackagesToValidate = new ITaskItem[] { pkg }
            };

            bool result = task.Execute();

            // Non-strict mode should succeed even with invalid packages
            Assert.True(result);
            Assert.NotEmpty(task.InvalidPackages);
            Assert.Empty(task.ValidatedPackages);
        }

        [Fact]
        public void Execute_StrictMode_InvalidPackage_ShouldReturnFalse()
        {
            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_tempDir);

            string packagesDir = Path.Combine(_tempDir, "strict-packages");
            Directory.CreateDirectory(packagesDir);

            var pkg = new TaskItem("MissingPackage");
            pkg.SetMetadata("Version", "1.0.0");

            var task = new PackageIntegrityChecker
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                PackagesDirectory = "strict-packages",
                StrictMode = true,
                PackagesToValidate = new ITaskItem[] { pkg }
            };

            bool result = task.Execute();

            Assert.False(result, "StrictMode should cause Execute to return false for invalid packages");
            Assert.NotEmpty(_engine.Errors);
        }

        #endregion

        #region ProjectDirectory auto-initialization

        [Fact]
        public void Execute_ShouldAutoInitializeProjectDirectory_WhenEmpty()
        {
            // Create a valid package so the task can complete
            string packagesDir = Path.Combine(_tempDir, "packages", "autopkg", "1.0.0", "lib");
            Directory.CreateDirectory(packagesDir);

            var pkg = new TaskItem("AutoPkg");
            pkg.SetMetadata("Version", "1.0.0");

            var taskEnv = new TrackingTaskEnvironment { ProjectDirectory = string.Empty };

            var task = new PackageIntegrityChecker
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                PackagesDirectory = "packages",
                PackagesToValidate = new ITaskItem[] { pkg }
            };

            // Execute should auto-initialize ProjectDirectory from BuildEngine.ProjectFileOfTaskNode
            task.Execute();

            Assert.False(string.IsNullOrEmpty(taskEnv.ProjectDirectory),
                "Task should auto-initialize ProjectDirectory from BuildEngine.ProjectFileOfTaskNode");
        }

        #endregion
    }
}
