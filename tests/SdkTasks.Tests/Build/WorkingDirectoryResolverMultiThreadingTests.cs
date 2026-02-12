using System;
using System.IO;
using Microsoft.Build.Framework;
using SdkTasks.Build;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests.Build
{
    public class WorkingDirectoryResolverMultiThreadingTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly string _cwdBefore;

        public WorkingDirectoryResolverMultiThreadingTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _cwdBefore = Directory.GetCurrentDirectory();
        }

        [Fact]
        public void Execute_CurrentDirIsProjectDirectory_NotCwd()
        {
            var task = new WorkingDirectoryResolver();
            task.BuildEngine = new MockBuildEngine();
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir);

            var result = task.Execute();

            Assert.True(result, "Task should succeed");
            Assert.Equal(_projectDir, task.CurrentDir);
        }

        [Fact]
        public void Execute_CurrentDirDoesNotMatchCwd()
        {
            var task = new WorkingDirectoryResolver();
            task.BuildEngine = new MockBuildEngine();
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir);

            task.Execute();

            if (_projectDir != _cwdBefore)
            {
                Assert.NotEqual(_cwdBefore, task.CurrentDir);
            }
        }

        [Fact]
        public void Execute_ResolvedPathLogContainsProjectDirectory()
        {
            var task = new WorkingDirectoryResolver();
            var engine = new MockBuildEngine();
            task.BuildEngine = engine;
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir);

            task.Execute();

            Assert.Contains(engine.Messages, m =>
                m.Message!.Contains("Resolved path:") && m.Message!.Contains(_projectDir));
        }

        [Fact]
        public void Execute_ResolvedPathLogDoesNotContainCwd()
        {
            var task = new WorkingDirectoryResolver();
            var engine = new MockBuildEngine();
            task.BuildEngine = engine;
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir);

            task.Execute();

            if (_projectDir != _cwdBefore)
            {
                Assert.Contains(engine.Messages, m =>
                    m.Message!.Contains("Resolved path:") && !m.Message!.Contains(_cwdBefore));
            }
        }

        [Fact]
        public void Execute_ResolvedPathCombinesProjectDirWithOutput()
        {
            var task = new WorkingDirectoryResolver();
            var engine = new MockBuildEngine();
            task.BuildEngine = engine;
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir);

            task.Execute();

            var expectedPath = Path.Combine(_projectDir, "output");
            Assert.Contains(engine.Messages, m =>
                m.Message!.Contains(expectedPath));
        }

        [Fact]
        public void Execute_DefensiveInit_SetsProjectDirectoryFromBuildEngine()
        {
            // MockBuildEngine.ProjectFileOfTaskNode returns "test.csproj"
            // Defensive init resolves that relative to CWD via Path.GetFullPath
            var task = new WorkingDirectoryResolver();
            task.BuildEngine = new MockBuildEngine();
            task.TaskEnvironment = new TaskEnvironment(); // empty ProjectDirectory

            var result = task.Execute();

            Assert.True(result, "Task should succeed");
            // ProjectDirectory should have been auto-initialized from BuildEngine
            Assert.False(string.IsNullOrEmpty(task.TaskEnvironment.ProjectDirectory),
                "Defensive init should set ProjectDirectory from BuildEngine");
            Assert.Equal(task.TaskEnvironment.ProjectDirectory, task.CurrentDir);
        }

        [Fact]
        public void CwdIsNotModifiedByExecution()
        {
            var task = new WorkingDirectoryResolver();
            task.BuildEngine = new MockBuildEngine();
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir);

            task.Execute();

            Assert.Equal(_cwdBefore, Directory.GetCurrentDirectory());
        }

        public void Dispose()
        {
            TestHelper.CleanupTempDirectory(_projectDir);
        }
    }
}
