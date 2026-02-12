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
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Packaging.PackageIntegrityChecker();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Packaging.PackageIntegrityChecker),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldResolveToProjectDirectory()
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
    }
}
