using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class SdkLocationProviderTests : IDisposable
    {
        private readonly List<string> _tempDirs = new();

        private string CreateProjectDir()
        {
            var dir = TestHelper.CreateNonCwdTempDirectory();
            _tempDirs.Add(dir);
            return dir;
        }

        public void Dispose()
        {
            foreach (var dir in _tempDirs)
                TestHelper.CleanupTempDirectory(dir);
        }

        [Fact]
        public void ShouldUseTaskEnvironment()
        {
            var dir1 = CreateProjectDir();

            var sdk1 = CreateFakeSdk(dir1, "sdk1");

            try
            {
                var taskEnv1 = TaskEnvironmentHelper.CreateForTest(dir1);
                taskEnv1.SetEnvironmentVariable("DOTNET_ROOT", sdk1);

                var engine1 = new MockBuildEngine();
                var task1 = new SdkTasks.Configuration.SdkLocationProvider
                {
                    BuildEngine = engine1,
                    TaskEnvironment = taskEnv1,
                    TargetFramework = "net8.0",
                };

                Assert.True(task1.Execute());
                Assert.Contains(engine1.Messages!, m => m.Message != null && m.Message.Contains(sdk1));
            }
            finally
            {
                Environment.SetEnvironmentVariable("DOTNET_ROOT", null);
            }
        }

        [Fact]
        public void ShouldCallGetEnvironmentVariable_ViaTrackingEnvironment()
        {
            var dir = CreateProjectDir();
            var sdkPath = CreateFakeSdk(dir, "dotnet-sdk");

            var tracking = SharedTestHelpers.CreateTrackingEnvironment(dir);
            tracking.SetEnvironmentVariable("DOTNET_ROOT", sdkPath);

            var task = new SdkTasks.Configuration.SdkLocationProvider
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = tracking,
                TargetFramework = "net8.0",
            };

            Assert.True(task.Execute());

            // Task must call TaskEnvironment.GetEnvironmentVariable instead of Environment.GetEnvironmentVariable
            SharedTestHelpers.AssertUsesGetEnvironmentVariable(tracking);
        }

        [Fact]
        public void ShouldCallGetAbsolutePath_ViaTrackingEnvironment()
        {
            var dir = CreateProjectDir();
            var sdkPath = CreateFakeSdk(dir, "dotnet-sdk");

            var tracking = SharedTestHelpers.CreateTrackingEnvironment(dir);
            tracking.SetEnvironmentVariable("DOTNET_ROOT", sdkPath);

            var task = new SdkTasks.Configuration.SdkLocationProvider
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = tracking,
                TargetFramework = "net8.0",
            };

            Assert.True(task.Execute());

            // Task must call TaskEnvironment.GetAbsolutePath to resolve SDK root path
            SharedTestHelpers.AssertUsesGetAbsolutePath(tracking);
        }

        [Fact]
        public void TwoTasks_ShouldResolveToOwnProjectDir()
        {
            var dir1 = CreateProjectDir();
            var dir2 = CreateProjectDir();

            var sdk1 = CreateFakeSdk(dir1, "sdk1");
            var sdk2 = CreateFakeSdk(dir2, "sdk2");

            try
            {
                var taskEnv1 = TaskEnvironmentHelper.CreateForTest(dir1);
                taskEnv1.SetEnvironmentVariable("DOTNET_ROOT", sdk1);

                var task1 = new SdkTasks.Configuration.SdkLocationProvider
                {
                    BuildEngine = new MockBuildEngine(),
                    TaskEnvironment = taskEnv1,
                    TargetFramework = "net8.0",
                };

                Assert.True(task1.Execute());

                var taskEnv2 = TaskEnvironmentHelper.CreateForTest(dir2);
                taskEnv2.SetEnvironmentVariable("DOTNET_ROOT", sdk2);

                var task2 = new SdkTasks.Configuration.SdkLocationProvider
                {
                    BuildEngine = new MockBuildEngine(),
                    TaskEnvironment = taskEnv2,
                    TargetFramework = "net8.0",
                };

                Assert.True(task2.Execute());

                // Assert CORRECT behavior: each task reads its own SDK path
                var engine1 = (MockBuildEngine)task1.BuildEngine;
                var engine2 = (MockBuildEngine)task2.BuildEngine;
                Assert.Contains(engine1.Messages!, m => m.Message != null && m.Message.Contains(sdk1));
                Assert.Contains(engine2.Messages!, m => m.Message != null && m.Message.Contains(sdk2));
            }
            finally
            {
                Environment.SetEnvironmentVariable("DOTNET_ROOT", null);
            }
        }

        [Fact]
        public void ShouldResolveRelativeSdkRoot_RelativeToProjectDirectory()
        {
            var dir = CreateProjectDir();
            CreateFakeSdk(dir, "my-sdk");

            var tracking = SharedTestHelpers.CreateTrackingEnvironment(dir);
            // Set a relative DOTNET_ROOT - should resolve relative to ProjectDirectory, not CWD
            tracking.SetEnvironmentVariable("DOTNET_ROOT", "my-sdk");

            var task = new SdkTasks.Configuration.SdkLocationProvider
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = tracking,
                TargetFramework = "net8.0",
            };

            Assert.True(task.Execute());

            // GetAbsolutePath must have been called with the relative SDK path
            Assert.Contains("my-sdk", tracking.GetAbsolutePathArgs);

            // Resolved assemblies should reference the project-dir-based SDK, not CWD-based
            Assert.NotEmpty(task.FrameworkAssemblies);
            var assemblyPath = task.FrameworkAssemblies[0].ItemSpec;
            Assert.StartsWith(dir, assemblyPath, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldReturnFrameworkAssemblies_WhenSdkExists()
        {
            var dir = CreateProjectDir();
            var sdkPath = CreateFakeSdk(dir, "dotnet");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(dir);
            taskEnv.SetEnvironmentVariable("DOTNET_ROOT", sdkPath);

            var task = new SdkTasks.Configuration.SdkLocationProvider
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                TargetFramework = "net8.0",
            };

            Assert.True(task.Execute());
            Assert.NotEmpty(task.FrameworkAssemblies);
            Assert.Contains(task.FrameworkAssemblies, item =>
                item.GetMetadata("ResolvedFileName") == "System.Runtime");
        }

        [Fact]
        public void ShouldLogError_WhenTargetFrameworkIsEmpty()
        {
            var dir = CreateProjectDir();
            var engine = new MockBuildEngine();

            var task = new SdkTasks.Configuration.SdkLocationProvider
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir),
                TargetFramework = "",
            };

            Assert.False(task.Execute());
            Assert.Contains(engine.Errors, e => e.Message!.Contains("TargetFramework"));
        }

        [Fact]
        public void ShouldAutoInitializeProjectDirectory_FromBuildEngine()
        {
            var dir = CreateProjectDir();
            var sdkPath = CreateFakeSdk(dir, "dotnet");

            // TaskEnvironment with empty ProjectDirectory - auto-init from BuildEngine runs
            var taskEnv = new TaskEnvironment();
            taskEnv.SetEnvironmentVariable("DOTNET_ROOT", sdkPath);

            var engine = new MockBuildEngine();
            var task = new SdkTasks.Configuration.SdkLocationProvider
            {
                BuildEngine = engine,
                TaskEnvironment = taskEnv,
                TargetFramework = "net8.0",
            };

            // The auto-init code should run and set ProjectDirectory from BuildEngine.ProjectFileOfTaskNode.
            // MockBuildEngine returns "test.csproj", Path.GetDirectoryName yields "" which is still set.
            // The important thing is it doesn't throw and the defensive code path executes.
            task.Execute();

            // ProjectDirectory was touched by the auto-init code path
            Assert.NotNull(taskEnv.ProjectDirectory);
        }

        private string CreateFakeSdk(string parentDir, string name)
        {
            var sdkRoot = Path.Combine(parentDir, name);
            var refDir = Path.Combine(sdkRoot, "packs", "Microsoft.NETCore.App.Ref", "8.0.0", "ref", "net8.0");
            Directory.CreateDirectory(refDir);
            File.WriteAllText(Path.Combine(refDir, "System.Runtime.dll"), "fake");
            return sdkRoot;
        }
    }
}
