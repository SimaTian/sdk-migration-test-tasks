using System.Collections;
using System.Reflection;
using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class WorkingDirectoryResolverMultiThreadingTests : IDisposable
    {
        private readonly string _projectDir;

        public WorkingDirectoryResolverMultiThreadingTests()
        {
            _projectDir = Path.Combine(Path.GetTempPath(), "wdr-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_projectDir);
        }

        [Fact]
        public void CurrentDir_IsSetToProjectDirectory_NotCwd()
        {
            var task = new SdkTasks.Build.WorkingDirectoryResolver();
            task.BuildEngine = new MockBuildEngine();
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir);

            var result = task.Execute();

            Assert.True(result);
            Assert.Equal(_projectDir, task.CurrentDir);
            Assert.NotEqual(Environment.CurrentDirectory, task.CurrentDir);
        }

        [Fact]
        public void LogMessage_ContainsProjectRelativePath()
        {
            var task = new SdkTasks.Build.WorkingDirectoryResolver();
            var engine = new MockBuildEngine();
            task.BuildEngine = engine;
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir);

            task.Execute();

            var expectedPath = Path.Combine(_projectDir, "output");
            Assert.Contains(engine.Messages, m => m.Message != null && m.Message.Contains(expectedPath));
        }

        [Fact]
        public void FallsBackToBuildEngineProjectFile_WhenProjectDirectoryEmpty()
        {
            var task = new SdkTasks.Build.WorkingDirectoryResolver();
            var projectFile = Path.Combine(_projectDir, "test.csproj");
            File.WriteAllText(projectFile, "<Project />");
            var engine = new ConfigurableMockBuildEngine(projectFile);
            task.BuildEngine = engine;
            task.TaskEnvironment = new TaskEnvironment();

            var result = task.Execute();

            Assert.True(result);
            Assert.Equal(_projectDir, task.CurrentDir);
        }

        [Fact]
        public void CurrentDir_Output_StartsWithProjectDirectory()
        {
            var task = new SdkTasks.Build.WorkingDirectoryResolver();
            task.BuildEngine = new MockBuildEngine();
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir);

            var result = task.Execute();
            Assert.True(result);

            // Reflection-based: all [Output] string properties must start with ProjectDirectory
            var outputProps = typeof(SdkTasks.Build.WorkingDirectoryResolver)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<OutputAttribute>() != null && p.PropertyType == typeof(string));

            foreach (var prop in outputProps)
            {
                var value = (string?)prop.GetValue(task);
                Assert.NotNull(value);
                Assert.StartsWith(_projectDir, value!);
            }
        }

        public void Dispose()
        {
            try { Directory.Delete(_projectDir, true); } catch { }
        }

        /// <summary>
        /// Minimal IBuildEngine4 with settable ProjectFileOfTaskNode for fallback tests.
        /// </summary>
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
