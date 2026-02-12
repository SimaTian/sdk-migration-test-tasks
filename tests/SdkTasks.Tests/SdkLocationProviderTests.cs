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
        public void ShouldCallGetEnvironmentVariableOnTaskEnvironment()
        {
            var projectDir = CreateProjectDir();
            var sdkRoot = CreateFakeSdk(projectDir, "tracked-sdk");

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = projectDir };
            trackingEnv.SetEnvironmentVariable("DOTNET_ROOT", sdkRoot);

            var task = new SdkTasks.Configuration.SdkLocationProvider
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = trackingEnv,
                TargetFramework = "net8.0",
            };

            Assert.True(task.Execute());

            SharedTestHelpers.AssertGetEnvironmentVariableCalled(trackingEnv);
        }

        [Fact]
        public void ShouldResolveDifferentSdkLocationsPerTaskEnvironment()
        {
            var dir1 = CreateProjectDir();
            var dir2 = CreateProjectDir();

            var sdk1 = CreateFakeSdk(dir1, "sdk1");
            var sdk2 = CreateFakeSdk(dir2, "sdk2");

            try
            {
                var taskEnv1 = TaskEnvironmentHelper.CreateForTest(dir1);
                taskEnv1.SetEnvironmentVariable("DOTNET_ROOT", sdk1);

                var taskEnv2 = TaskEnvironmentHelper.CreateForTest(dir2);
                taskEnv2.SetEnvironmentVariable("DOTNET_ROOT", sdk2);

                var task1 = new SdkTasks.Configuration.SdkLocationProvider
                {
                    BuildEngine = new MockBuildEngine(),
                    TaskEnvironment = taskEnv1,
                    TargetFramework = "net8.0",
                };

                var task2 = new SdkTasks.Configuration.SdkLocationProvider
                {
                    BuildEngine = new MockBuildEngine(),
                    TaskEnvironment = taskEnv2,
                    TargetFramework = "net8.0",
                };

                Assert.True(task1.Execute());
                Assert.True(task2.Execute());

                // Each task should resolve assemblies from its own SDK path
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
        public void ShouldResolveFrameworkAssembliesFromTaskScopedSdk()
        {
            var projectDir = CreateProjectDir();
            var sdkRoot = CreateFakeSdk(projectDir, "my-sdk");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(projectDir);
            taskEnv.SetEnvironmentVariable("DOTNET_ROOT", sdkRoot);

            var task = new SdkTasks.Configuration.SdkLocationProvider
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                TargetFramework = "net8.0",
            };

            Assert.True(task.Execute());

            // Should find at least the fake assembly we created
            Assert.NotEmpty(task.FrameworkAssemblies);
            var anyInSdk = task.FrameworkAssemblies.Any(a =>
                a.ItemSpec.Contains(sdkRoot, StringComparison.OrdinalIgnoreCase));
            Assert.True(anyInSdk, "Framework assemblies should be located under the task-scoped SDK root");
        }

        [Fact]
        public void ShouldNotReadDotnetRootFromGlobalEnvironment()
        {
            var projectDir = CreateProjectDir();
            var sdkRoot = CreateFakeSdk(projectDir, "isolated-sdk");

            // Set DOTNET_ROOT ONLY in TaskEnvironment (not in the global process environment)
            var taskEnv = TaskEnvironmentHelper.CreateForTest(projectDir);
            taskEnv.SetEnvironmentVariable("DOTNET_ROOT", sdkRoot);

            var task = new SdkTasks.Configuration.SdkLocationProvider
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = taskEnv,
                TargetFramework = "net8.0",
            };

            Assert.True(task.Execute());
            Assert.NotEmpty(task.FrameworkAssemblies);
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
