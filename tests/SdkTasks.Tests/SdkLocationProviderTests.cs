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
        public void ImplementsIMultiThreadableTask()
        {
            var task = new SdkTasks.Configuration.SdkLocationProvider();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Configuration.SdkLocationProvider),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
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
