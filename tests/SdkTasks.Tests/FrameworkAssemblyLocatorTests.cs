using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class FrameworkAssemblyLocatorTests : IDisposable
    {
        private readonly TaskTestContext _ctx;
        private string _projectDir => _ctx.ProjectDir;
        private MockBuildEngine _engine => _ctx.Engine;

        public FrameworkAssemblyLocatorTests() => _ctx = new TaskTestContext();
        public void Dispose() => _ctx.Dispose();

        [Fact]
        public void ShouldUseTaskEnvironmentForRuntimePackResolution()
        {
            string binDir = Path.Combine(_projectDir, "bin");
            Directory.CreateDirectory(binDir);
            File.WriteAllText(Path.Combine(binDir, "TestLib.dll"), "fake");

            var taskEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            taskEnv.SetEnvironmentVariable("DOTNET_ROOT", _projectDir);

            var task = new SdkTasks.Build.FrameworkAssemblyLocator
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                References = new ITaskItem[] { new TaskItem("TestLib") },
                TargetFramework = "net8.0",
                RuntimeIdentifier = "win-x64"
            };

            task.Execute();

            Assert.NotEmpty(task.ResolvedReferences);
            bool runtimePackResolvedViaTaskEnv = taskEnv.GetAbsolutePathArgs.Any(arg =>
                arg.Contains("packs") && arg.Contains("Microsoft.NETCore.App.Runtime"));
            Assert.True(runtimePackResolvedViaTaskEnv,
                "Task should use TaskEnvironment.GetAbsolutePath() for runtime pack path resolution");
        }

        [Fact]
        public void HintPath_ShouldResolveRelativeToProjectDir()
        {
            string libDir = Path.Combine(_projectDir, "libs");
            Directory.CreateDirectory(libDir);
            File.WriteAllText(Path.Combine(libDir, "MyLib.dll"), "fake-dll");

            var reference = new TaskItem("MyLib");
            reference.SetMetadata("HintPath", Path.Combine("libs", "MyLib.dll"));

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);

            var task = new SdkTasks.Build.FrameworkAssemblyLocator
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                References = new ITaskItem[] { reference },
                TargetFramework = "net8.0",
                RuntimeIdentifier = "win-x64"
            };

            bool result = task.Execute();

            Assert.True(result, $"Execute failed. Errors: {string.Join("; ", _engine.Errors.Select(e => e.Message))}");
            Assert.NotEmpty(task.ResolvedReferences);
            string resolvedPath = task.ResolvedReferences[0].GetMetadata("ResolvedPath");
            SharedTestHelpers.AssertPathUnderProjectDir(_projectDir, resolvedPath);
            SharedTestHelpers.AssertUsesGetAbsolutePath(trackingEnv);
        }

        [Fact]
        public void FrameworkDirectories_ShouldResolveViaGetAbsolutePath()
        {
            string fxDir = Path.Combine(_projectDir, "ref", "net8.0");
            Directory.CreateDirectory(fxDir);
            File.WriteAllText(Path.Combine(fxDir, "System.Runtime.dll"), "fake-dll");

            var fxDirItem = new TaskItem(Path.Combine("ref", "net8.0"));
            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);

            var task = new SdkTasks.Build.FrameworkAssemblyLocator
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                References = new ITaskItem[] { new TaskItem("System.Runtime") },
                TargetFramework = "net8.0",
                RuntimeIdentifier = "win-x64",
                FrameworkDirectories = new ITaskItem[] { fxDirItem }
            };

            bool result = task.Execute();

            Assert.True(result, $"Execute failed. Errors: {string.Join("; ", _engine.Errors.Select(e => e.Message))}");
            Assert.NotEmpty(task.ResolvedReferences);
            string resolvedPath = task.ResolvedReferences[0].GetMetadata("ResolvedPath");
            Assert.Contains(_projectDir, resolvedPath, StringComparison.OrdinalIgnoreCase);
            SharedTestHelpers.AssertMinimumGetAbsolutePathCalls(trackingEnv, 2);
        }

        [Fact]
        public void DotnetRoot_ShouldResolveViaTaskEnvironmentEnvVar()
        {
            string runtimePackDir = Path.Combine(_projectDir, "packs",
                "Microsoft.NETCore.App.Runtime.win-x64", "net8.0", "runtimes", "win-x64", "lib", "net8.0");
            Directory.CreateDirectory(runtimePackDir);
            File.WriteAllText(Path.Combine(runtimePackDir, "TestLib.dll"), "fake");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            trackingEnv.SetEnvironmentVariable("DOTNET_ROOT", _projectDir);

            var task = new SdkTasks.Build.FrameworkAssemblyLocator
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                References = new ITaskItem[] { new TaskItem("TestLib") },
                TargetFramework = "net8.0",
                RuntimeIdentifier = "win-x64"
            };

            task.Execute();

            Assert.NotEmpty(task.ResolvedReferences);
            var anyResolved = task.ResolvedReferences.Any(r =>
            {
                var rp = r.GetMetadata("ResolvedPath") ?? r.ItemSpec;
                return rp.Contains(_projectDir, StringComparison.OrdinalIgnoreCase);
            });
            Assert.True(anyResolved,
                "References should be resolved relative to TaskEnvironment's DOTNET_ROOT");
            SharedTestHelpers.AssertUsesGetEnvironmentVariable(trackingEnv);
        }

        [Fact]
        public void SearchPaths_ShouldUseProjectDirectory_NotCwd()
        {
            string binDir = Path.Combine(_projectDir, "bin");
            Directory.CreateDirectory(binDir);
            File.WriteAllText(Path.Combine(binDir, "BinAssembly.dll"), "fake");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);

            var task = new SdkTasks.Build.FrameworkAssemblyLocator
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                References = new ITaskItem[] { new TaskItem("BinAssembly") },
                TargetFramework = "net8.0",
                RuntimeIdentifier = "win-x64"
            };

            bool result = task.Execute();

            Assert.True(result, $"Execute failed. Errors: {string.Join("; ", _engine.Errors.Select(e => e.Message))}");
            Assert.NotEmpty(task.ResolvedReferences);
            string resolvedPath = task.ResolvedReferences[0].GetMetadata("ResolvedPath");
            SharedTestHelpers.AssertPathUnderProjectDir(_projectDir, resolvedPath);
        }

        [Fact]
        public void UnresolvedReferences_ShouldBeReportedCorrectly()
        {
            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);

            var task = new SdkTasks.Build.FrameworkAssemblyLocator
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                References = new ITaskItem[] { new TaskItem("NonExistentAssembly") },
                TargetFramework = "net8.0",
                RuntimeIdentifier = "win-x64"
            };

            bool result = task.Execute();

            Assert.False(result);
            Assert.NotEmpty(task.UnresolvedReferences);
            Assert.Equal("NonExistentAssembly", task.UnresolvedReferences[0].ItemSpec);
            Assert.Equal("net8.0", task.UnresolvedReferences[0].GetMetadata("TargetFramework"));
        }

        [Fact]
        public void FrameworkReferencePath_ShouldResolveViaTaskEnvironmentEnvVar()
        {
            string fxRefDir = Path.Combine(_projectDir, "fxref");
            Directory.CreateDirectory(fxRefDir);
            File.WriteAllText(Path.Combine(fxRefDir, "FxRefLib.dll"), "fake");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            trackingEnv.SetEnvironmentVariable("FRAMEWORK_REFERENCE_PATH", fxRefDir);

            var task = new SdkTasks.Build.FrameworkAssemblyLocator
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                References = new ITaskItem[] { new TaskItem("FxRefLib") },
                TargetFramework = "net8.0",
                RuntimeIdentifier = "win-x64"
            };

            bool result = task.Execute();

            Assert.True(result, $"Execute failed. Errors: {string.Join("; ", _engine.Errors.Select(e => e.Message))}");
            Assert.NotEmpty(task.ResolvedReferences);
            string resolvedPath = task.ResolvedReferences[0].GetMetadata("ResolvedPath");
            Assert.Contains(_projectDir, resolvedPath, StringComparison.OrdinalIgnoreCase);
            SharedTestHelpers.AssertMinimumGetEnvironmentVariableCalls(trackingEnv, 2);
        }
    }
}
