using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class PackageIntegrityCheckerTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public PackageIntegrityCheckerTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldUseTaskEnvironmentForPackageResolution()
        {
            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);

            taskEnv.SetEnvironmentVariable("NUGET_PACKAGES", null);
            taskEnv.SetEnvironmentVariable("USERPROFILE", _projectDir);
            taskEnv.SetEnvironmentVariable("HOME", _projectDir);

            string globalPkgDir = Path.Combine(_projectDir, ".nuget", "packages");
            string packageDir = Path.Combine(globalPkgDir, "fakepackage", "1.0.0");
            Directory.CreateDirectory(packageDir);
            string libDir = Path.Combine(packageDir, "lib");
            Directory.CreateDirectory(libDir);

            var pkg = new TaskItem("FakePackage");
            pkg.SetMetadata("Version", "1.0.0");

            var task = new SdkTasks.Packaging.PackageIntegrityChecker
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                PackagesDirectory = "nonexistent-packages",
                PackagesToValidate = new ITaskItem[] { pkg }
            };

            task.Execute();

            Assert.NotEmpty(task.ValidatedPackages);
        }

        [Fact]
        public void ShouldCallTaskEnvironmentGetEnvironmentVariable()
        {
            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            trackingEnv.SetEnvironmentVariable("NUGET_PACKAGES", null);
            trackingEnv.SetEnvironmentVariable("USERPROFILE", _projectDir);
            trackingEnv.SetEnvironmentVariable("HOME", _projectDir);

            string globalPkgDir = Path.Combine(_projectDir, ".nuget", "packages");
            string packageDir = Path.Combine(globalPkgDir, "fakepackage", "1.0.0");
            Directory.CreateDirectory(packageDir);
            Directory.CreateDirectory(Path.Combine(packageDir, "lib"));

            var pkg = new TaskItem("FakePackage");
            pkg.SetMetadata("Version", "1.0.0");

            var task = new SdkTasks.Packaging.PackageIntegrityChecker
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                PackagesDirectory = "nonexistent-packages",
                PackagesToValidate = new ITaskItem[] { pkg }
            };

            task.Execute();

            SharedTestHelpers.AssertGetEnvironmentVariableCalled(trackingEnv);
        }

        [Fact]
        public void ShouldCallTaskEnvironmentGetAbsolutePath()
        {
            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            trackingEnv.SetEnvironmentVariable("NUGET_PACKAGES", null);
            trackingEnv.SetEnvironmentVariable("USERPROFILE", _projectDir);
            trackingEnv.SetEnvironmentVariable("HOME", _projectDir);

            string globalPkgDir = Path.Combine(_projectDir, ".nuget", "packages");
            string packageDir = Path.Combine(globalPkgDir, "fakepackage", "1.0.0");
            Directory.CreateDirectory(packageDir);
            Directory.CreateDirectory(Path.Combine(packageDir, "lib"));

            var pkg = new TaskItem("FakePackage");
            pkg.SetMetadata("Version", "1.0.0");

            var task = new SdkTasks.Packaging.PackageIntegrityChecker
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                PackagesDirectory = "nonexistent-packages",
                PackagesToValidate = new ITaskItem[] { pkg }
            };

            task.Execute();

            SharedTestHelpers.AssertGetAbsolutePathCalled(trackingEnv);
        }

        [Fact]
        public void ShouldValidatePackageFromLocalDirectory()
        {
            string localPkgDir = Path.Combine(_projectDir, "local-packages");
            string packageDir = Path.Combine(localPkgDir, "localpackage", "2.0.0");
            Directory.CreateDirectory(packageDir);
            Directory.CreateDirectory(Path.Combine(packageDir, "lib"));

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);

            var pkg = new TaskItem("LocalPackage");
            pkg.SetMetadata("Version", "2.0.0");

            var task = new SdkTasks.Packaging.PackageIntegrityChecker
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                PackagesDirectory = "local-packages",
                PackagesToValidate = new ITaskItem[] { pkg }
            };

            task.Execute();

            Assert.NotEmpty(task.ValidatedPackages);
        }
    }
}
