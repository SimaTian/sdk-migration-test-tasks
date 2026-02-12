// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Reflection;
using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Packaging;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests.Packaging
{
    public class PackageIntegrityCheckerMultiThreadingTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public PackageIntegrityCheckerMultiThreadingTests()
        {
            _projectDir = Path.Combine(Path.GetTempPath(), "pic-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_projectDir);
            _engine = new MockBuildEngine();
        }

        public void Dispose()
        {
            try { Directory.Delete(_projectDir, true); } catch { }
        }

        [Fact]
        public void ItResolvesRelativePackagesDirectoryViaTaskEnvironment()
        {
            string pkgDir = Path.Combine(_projectDir, "packages", "testpkg", "1.0.0", "lib");
            Directory.CreateDirectory(pkgDir);

            var task = new PackageIntegrityChecker
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                PackagesDirectory = "packages",
                PackagesToValidate = new ITaskItem[]
                {
                    new TaskItem("testpkg", new Dictionary<string, string> { ["Version"] = "1.0.0" })
                }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Single(task.ValidatedPackages);
            Assert.Empty(task.InvalidPackages);
        }

        [Fact]
        public void ItReadsNuGetPackagesEnvVarViaTaskEnvironment()
        {
            string customPkgDir = Path.Combine(_projectDir, "custom-nuget");
            string pkgPath = Path.Combine(customPkgDir, "mypkg", "2.0.0", "lib");
            Directory.CreateDirectory(pkgPath);

            var taskEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            taskEnv.SetEnvironmentVariable("NUGET_PACKAGES", customPkgDir);

            var task = new PackageIntegrityChecker
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                PackagesDirectory = "nonexistent",
                PackagesToValidate = new ITaskItem[]
                {
                    new TaskItem("mypkg", new Dictionary<string, string> { ["Version"] = "2.0.0" })
                }
            };

            bool result = task.Execute();

            SharedTestHelpers.AssertUsesGetEnvironmentVariable(taskEnv);
            Assert.True(result);
            Assert.Single(task.ValidatedPackages);
        }

        [Fact]
        public void StrictModeReturnsFalseForInvalidPackages()
        {
            string emptyPkgDir = Path.Combine(_projectDir, "packages", "badpkg", "1.0.0");
            Directory.CreateDirectory(emptyPkgDir);

            var task = new PackageIntegrityChecker
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                PackagesDirectory = "packages",
                StrictMode = true,
                PackagesToValidate = new ITaskItem[]
                {
                    new TaskItem("badpkg", new Dictionary<string, string> { ["Version"] = "1.0.0" })
                }
            };

            bool result = task.Execute();

            Assert.False(result);
            Assert.Single(task.InvalidPackages);
            Assert.NotEmpty(task.InvalidPackages[0].GetMetadata("Error"));
        }

        [Fact]
        public void ItPreservesPublicApiSurface()
        {
            var expectedProperties = new[]
            {
                "TaskEnvironment", "PackagesDirectory", "PackagesToValidate",
                "StrictMode", "NuGetConfigPath", "ValidatedPackages", "InvalidPackages"
            };

            var actualProperties = typeof(PackageIntegrityChecker)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(p => p.Name)
                .OrderBy(n => n)
                .ToArray();

            Assert.Equal(expectedProperties.OrderBy(n => n).ToArray(), actualProperties);
        }

        [Fact]
        public void ItAutoInitializesProjectDirectoryFromBuildEngine()
        {
            string pkgDir = Path.Combine(_projectDir, "packages", "autopkg", "1.0.0", "lib");
            Directory.CreateDirectory(pkgDir);

            string projectFile = Path.Combine(_projectDir, "test.csproj");
            File.WriteAllText(projectFile, "<Project />");

            var taskEnv = new TrackingTaskEnvironment { ProjectDirectory = string.Empty };
            var engine = new ConfigurableMockBuildEngine(projectFile);

            var task = new PackageIntegrityChecker
            {
                BuildEngine = engine,
                TaskEnvironment = taskEnv,
                PackagesDirectory = "packages",
                PackagesToValidate = new ITaskItem[]
                {
                    new TaskItem("autopkg", new Dictionary<string, string> { ["Version"] = "1.0.0" })
                }
            };

            task.Execute();

            Assert.False(string.IsNullOrEmpty(taskEnv.ProjectDirectory),
                "Task should auto-initialize ProjectDirectory from BuildEngine.ProjectFileOfTaskNode");
            Assert.Equal(_projectDir, taskEnv.ProjectDirectory);
        }

        [Fact]
        public void ItTracksGetAbsolutePathCallsForRelativePaths()
        {
            string pkgDir = Path.Combine(_projectDir, "packages", "tracked", "1.0.0", "lib");
            Directory.CreateDirectory(pkgDir);

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);

            var task = new PackageIntegrityChecker
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                PackagesDirectory = "packages",
                PackagesToValidate = new ITaskItem[]
                {
                    new TaskItem("tracked", new Dictionary<string, string> { ["Version"] = "1.0.0" })
                }
            };

            task.Execute();

            SharedTestHelpers.AssertUsesGetAbsolutePath(trackingEnv);
            Assert.Contains("packages", trackingEnv.GetAbsolutePathArgs);
        }

        private class ConfigurableMockBuildEngine : IBuildEngine4
        {
            public ConfigurableMockBuildEngine(string projectFile) => ProjectFileOfTaskNode = projectFile;

            public bool ContinueOnError => false;
            public int LineNumberOfTaskNode => 0;
            public int ColumnNumberOfTaskNode => 0;
            public string ProjectFileOfTaskNode { get; }

            public void LogErrorEvent(BuildErrorEventArgs e) { }
            public void LogWarningEvent(BuildWarningEventArgs e) { }
            public void LogMessageEvent(BuildMessageEventArgs e) { }
            public void LogCustomEvent(CustomBuildEventArgs e) { }
            public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs) => true;

            public bool IsRunningMultipleNodes => true;
            public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs, string toolsVersion) => true;
            public bool BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IDictionary[] targetOutputsPerProject, string[] toolsVersion, bool useResultsCache, bool unloadProjectsOnCompletion) => true;

            public BuildEngineResult BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IList<string>[] removeGlobalProperties, string[] toolsVersion, bool returnTargetOutputs) => new(true, new List<IDictionary<string, ITaskItem[]>>());
            public void Yield() { }
            public void Reacquire() { }

            private readonly Dictionary<object, object> _taskObjects = new();
            public object? GetRegisteredTaskObject(object key, RegisteredTaskObjectLifetime lifetime) { _taskObjects.TryGetValue(key, out var value); return value; }
            public void RegisterTaskObject(object key, object obj, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection) => _taskObjects[key] = obj;
            public object? UnregisterTaskObject(object key, RegisteredTaskObjectLifetime lifetime) { _taskObjects.Remove(key, out var value); return value; }
        }
    }
}
