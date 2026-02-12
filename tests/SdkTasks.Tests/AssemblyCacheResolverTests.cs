// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Build;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests
{
    public class AssemblyCacheResolverTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly MockBuildEngine _engine;

        public AssemblyCacheResolverTests()
        {
            _tempDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose()
        {
            TestHelper.CleanupTempDirectory(_tempDir);
        }

        [Fact]
        public void Execute_ResolvesAssemblyRelativeToProjectDirectory()
        {
            // Place a fake assembly DLL in lib/net8.0 under the temp dir (not CWD)
            string libDir = Path.Combine(_tempDir, "lib", "net8.0");
            Directory.CreateDirectory(libDir);
            File.WriteAllText(Path.Combine(libDir, "TestAssemblyA.dll"), "fake-dll");

            var task = new AssemblyCacheResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_tempDir),
                AssemblyReferences = new ITaskItem[] { new TaskItem("TestAssemblyA") }
            };

            bool result = task.Execute();

            // Assert CORRECT behavior: assembly should be found and resolved under tempDir
            Assert.True(result);
            Assert.NotEmpty(task.ResolvedReferences);
            string resolvedPath = task.ResolvedReferences[0].ItemSpec;
            SharedTestHelpers.AssertPathUnderProjectDir(_tempDir, resolvedPath);
        }

        [Fact]
        public void Execute_UsesGetAbsolutePath_ForProbePaths()
        {
            // Place a fake assembly in bin/ under the temp dir
            string binDir = Path.Combine(_tempDir, "bin");
            Directory.CreateDirectory(binDir);
            File.WriteAllText(Path.Combine(binDir, "TestAssemblyB.dll"), "fake-dll");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_tempDir);

            var task = new AssemblyCacheResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                AssemblyReferences = new ITaskItem[] { new TaskItem("TestAssemblyB") }
            };

            bool result = task.Execute();

            Assert.True(result);
            // Verify TaskEnvironment.GetAbsolutePath was called for probe paths
            SharedTestHelpers.AssertUsesGetAbsolutePath(trackingEnv);
            Assert.Contains("bin", trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void Execute_WithTargetDirectory_ResolvesViaGetAbsolutePath()
        {
            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_tempDir);

            var task = new AssemblyCacheResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                AssemblyReferences = new ITaskItem[] { new TaskItem("TestAssemblyC") },
                TargetDirectory = "output"
            };

            task.Execute();

            // TargetDirectory should be resolved through GetAbsolutePath
            Assert.Contains("output", trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void Execute_EmptyReferences_ReturnsSuccessWithNoOutput()
        {
            var task = new AssemblyCacheResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_tempDir),
                AssemblyReferences = Array.Empty<ITaskItem>()
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Empty(task.ResolvedReferences);
        }

        [Fact]
        public void Execute_UnresolvableAssembly_LogsWarningAndSucceeds()
        {
            var task = new AssemblyCacheResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_tempDir),
                AssemblyReferences = new ITaskItem[] { new TaskItem("NonExistentAssembly") }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Empty(task.ResolvedReferences);
            Assert.Contains(_engine.Warnings,
                w => w.Message!.Contains("NonExistentAssembly"));
        }

        [Fact]
        public void Execute_ProbesMultipleDirectories_AllRelativeToProjectDir()
        {
            // Create an assembly in obj/refs under temp dir
            string objRefsDir = Path.Combine(_tempDir, "obj", "refs");
            Directory.CreateDirectory(objRefsDir);
            File.WriteAllText(Path.Combine(objRefsDir, "RefAssemblyD.dll"), "fake-dll");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_tempDir);

            var task = new AssemblyCacheResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                AssemblyReferences = new ITaskItem[] { new TaskItem("RefAssemblyD") }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.NotEmpty(task.ResolvedReferences);
            string resolvedPath = task.ResolvedReferences[0].ItemSpec;
            SharedTestHelpers.AssertPathUnderProjectDir(_tempDir, resolvedPath);

            // Verify all probe paths are resolved via GetAbsolutePath
            Assert.Contains("bin", trackingEnv.GetAbsolutePathArgs);
            Assert.Contains(Path.Combine("obj", "refs"), trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void Execute_ResolvedReferences_HaveCorrectMetadata()
        {
            string libDir = Path.Combine(_tempDir, "lib", "net8.0");
            Directory.CreateDirectory(libDir);
            File.WriteAllText(Path.Combine(libDir, "MetadataAssemblyE.dll"), "fake-dll");

            var task = new AssemblyCacheResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_tempDir),
                AssemblyReferences = new ITaskItem[] { new TaskItem("MetadataAssemblyE") }
            };

            task.Execute();

            Assert.NotEmpty(task.ResolvedReferences);
            var resolved = task.ResolvedReferences[0];
            Assert.Equal("MetadataAssemblyE", resolved.GetMetadata("AssemblyName"));
            Assert.Equal(".dll", resolved.GetMetadata("FileExtension"));
        }
    }
}
