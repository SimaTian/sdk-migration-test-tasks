using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class FrameworkAssemblyLocatorTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public FrameworkAssemblyLocatorTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldUseTaskEnvironmentGetAbsolutePathForRuntimePack()
        {
            string binDir = Path.Combine(_projectDir, "bin");
            Directory.CreateDirectory(binDir);
            File.WriteAllText(Path.Combine(binDir, "TestLib.dll"), "fake");

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
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
            bool runtimePackResolvedViaTaskEnv = trackingEnv.GetAbsolutePathArgs.Any(arg =>
                arg.Contains("packs") && arg.Contains("Microsoft.NETCore.App.Runtime"));
            Assert.True(runtimePackResolvedViaTaskEnv,
                "Task should use TaskEnvironment.GetAbsolutePath() for runtime pack path resolution");
        }

        [Fact]
        public void ShouldUseTaskEnvironmentGetEnvironmentVariable()
        {
            string runtimePackDir = Path.Combine(_projectDir, "packs",
                "Microsoft.NETCore.App.Runtime.win-x64", "net8.0", "runtimes", "win-x64", "lib", "net8.0");
            Directory.CreateDirectory(runtimePackDir);
            File.WriteAllText(Path.Combine(runtimePackDir, "TestLib.dll"), "fake");

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
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

            SharedTestHelpers.AssertGetEnvironmentVariableCalled(trackingEnv);
        }

        [Fact]
        public void ShouldResolveFromRuntimePackViaEnvVar()
        {
            string runtimePackDir = Path.Combine(_projectDir, "packs",
                "Microsoft.NETCore.App.Runtime.win-x64", "net8.0", "runtimes", "win-x64", "lib", "net8.0");
            Directory.CreateDirectory(runtimePackDir);
            File.WriteAllText(Path.Combine(runtimePackDir, "RuntimeLib.dll"), "fake");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);
            taskEnv.SetEnvironmentVariable("DOTNET_ROOT", _projectDir);

            var task = new SdkTasks.Build.FrameworkAssemblyLocator
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                References = new ITaskItem[] { new TaskItem("RuntimeLib") },
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
        }

        [Fact]
        public void ShouldResolveViaHintPathUsingTaskEnvironment()
        {
            string hintDir = Path.Combine(_projectDir, "hints");
            Directory.CreateDirectory(hintDir);
            File.WriteAllText(Path.Combine(hintDir, "HintLib.dll"), "fake");

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            trackingEnv.SetEnvironmentVariable("DOTNET_ROOT", _projectDir);

            var reference = new TaskItem("HintLib");
            reference.SetMetadata("HintPath", Path.Combine("hints", "HintLib.dll"));

            var task = new SdkTasks.Build.FrameworkAssemblyLocator
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                References = new ITaskItem[] { reference },
                TargetFramework = "net8.0",
                RuntimeIdentifier = "win-x64"
            };

            task.Execute();

            Assert.NotEmpty(task.ResolvedReferences);
            Assert.Contains(trackingEnv.GetAbsolutePathArgs,
                arg => arg.Contains("hints") && arg.Contains("HintLib.dll"));
        }

        [Fact]
        public void ShouldResolveFrameworkDirectoriesViaGetAbsolutePath()
        {
            string fxDir = Path.Combine(_projectDir, "shared", "Microsoft.NETCore.App");
            Directory.CreateDirectory(fxDir);
            File.WriteAllText(Path.Combine(fxDir, "FxLib.dll"), "fake");

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };

            var fxDirItem = new TaskItem(Path.Combine("shared", "Microsoft.NETCore.App"));

            var task = new SdkTasks.Build.FrameworkAssemblyLocator
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                References = new ITaskItem[] { new TaskItem("FxLib") },
                TargetFramework = "net8.0",
                RuntimeIdentifier = "win-x64",
                FrameworkDirectories = new ITaskItem[] { fxDirItem }
            };

            task.Execute();

            Assert.NotEmpty(task.ResolvedReferences);
            Assert.Contains(trackingEnv.GetAbsolutePathArgs,
                arg => arg.Contains("shared") && arg.Contains("Microsoft.NETCore.App"));
        }

        [Fact]
        public void ShouldUseTaskEnvironmentForFrameworkReferencePath()
        {
            string fxRefDir = Path.Combine(_projectDir, "ref-assemblies");
            Directory.CreateDirectory(fxRefDir);
            File.WriteAllText(Path.Combine(fxRefDir, "RefLib.dll"), "fake");

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            trackingEnv.SetEnvironmentVariable("FRAMEWORK_REFERENCE_PATH", fxRefDir);

            var task = new SdkTasks.Build.FrameworkAssemblyLocator
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                References = new ITaskItem[] { new TaskItem("RefLib") },
                TargetFramework = "net8.0",
                RuntimeIdentifier = "win-x64"
            };

            task.Execute();

            SharedTestHelpers.AssertGetEnvironmentVariableCalled(trackingEnv, 2);
            Assert.NotEmpty(task.ResolvedReferences);
            var resolvedPath = task.ResolvedReferences[0].GetMetadata("ResolvedPath");
            Assert.Contains(_projectDir, resolvedPath, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldBuildSearchPathsRelativeToProjectDirectory()
        {
            // Place DLL in bin/ under project dir; task must use ProjectDirectory (not CWD)
            string binDir = Path.Combine(_projectDir, "bin");
            Directory.CreateDirectory(binDir);
            File.WriteAllText(Path.Combine(binDir, "BinLib.dll"), "fake");

            var taskEnv = TaskEnvironmentHelper.CreateForTest(_projectDir);

            var task = new SdkTasks.Build.FrameworkAssemblyLocator
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                References = new ITaskItem[] { new TaskItem("BinLib") },
                TargetFramework = "net8.0",
                RuntimeIdentifier = "win-x64"
            };

            task.Execute();

            Assert.NotEmpty(task.ResolvedReferences);
            var resolvedPath = task.ResolvedReferences[0].GetMetadata("ResolvedPath");
            Assert.StartsWith(_projectDir, resolvedPath, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldUseGetProcessStartInfoForExternalResolver()
        {
            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };

            var task = new SdkTasks.Build.FrameworkAssemblyLocator
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                References = new ITaskItem[] { new TaskItem("NonExistentAssembly") },
                TargetFramework = "net8.0",
                RuntimeIdentifier = "win-x64"
            };

            // The external resolver will fail but should use TaskEnvironment.GetProcessStartInfo
            task.Execute();

            // Verify the task uses TaskEnvironment's ProjectDirectory for process working dir
            var psi = trackingEnv.GetProcessStartInfo();
            Assert.Equal(_projectDir, psi.WorkingDirectory);
        }
    }
}
