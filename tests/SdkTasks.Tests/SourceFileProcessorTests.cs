using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class SourceFileProcessorTests : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public SourceFileProcessorTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ShouldResolveToProjectDirectory()
        {
            File.WriteAllText(Path.Combine(_projectDir, "source.cs"), "// src");

            var task = new SdkTasks.Compilation.SourceFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                Sources = new ITaskItem[] { new TaskItem("source.cs") }
            };

            task.Execute();

            Assert.NotEmpty(task.ResolvedSources);
            string resolved = task.ResolvedSources[0].ItemSpec;
            Assert.StartsWith(_projectDir, resolved, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldCallTaskEnvironmentGetAbsolutePathViaBaseClass()
        {
            File.WriteAllText(Path.Combine(_projectDir, "base-check.cs"), "// base");

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new SdkTasks.Compilation.SourceFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                Sources = new ITaskItem[] { new TaskItem("base-check.cs") }
            };

            task.Execute();

            SharedTestHelpers.AssertGetAbsolutePathCalled(trackingEnv);
        }

        [Fact]
        public void ShouldNotResolveRelativeToCwd()
        {
            File.WriteAllText(Path.Combine(_projectDir, "not-cwd.cs"), "// code");

            var task = new SdkTasks.Compilation.SourceFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                Sources = new ITaskItem[] { new TaskItem("not-cwd.cs") }
            };

            task.Execute();

            Assert.NotEmpty(task.ResolvedSources);
            string resolved = task.ResolvedSources[0].ItemSpec;
            Assert.StartsWith(_projectDir, resolved, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldResolveMultipleSources()
        {
            File.WriteAllText(Path.Combine(_projectDir, "a.cs"), "// a");
            File.WriteAllText(Path.Combine(_projectDir, "b.cs"), "// b");

            var task = new SdkTasks.Compilation.SourceFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                Sources = new ITaskItem[]
                {
                    new TaskItem("a.cs"),
                    new TaskItem("b.cs")
                }
            };

            task.Execute();

            Assert.Equal(2, task.ResolvedSources.Length);
            foreach (var source in task.ResolvedSources)
            {
                Assert.StartsWith(_projectDir, source.ItemSpec, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void ShouldPreserveOriginalIdentityMetadata()
        {
            File.WriteAllText(Path.Combine(_projectDir, "identity.cs"), "// id");

            var task = new SdkTasks.Compilation.SourceFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                Sources = new ITaskItem[] { new TaskItem("identity.cs") }
            };

            task.Execute();

            Assert.NotEmpty(task.ResolvedSources);
            string originalIdentity = task.ResolvedSources[0].GetMetadata("OriginalIdentity");
            Assert.Equal("identity.cs", originalIdentity);
        }

        [Fact]
        public void ShouldHandleEmptySources()
        {
            var task = new SdkTasks.Compilation.SourceFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                Sources = Array.Empty<ITaskItem>()
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Empty(task.ResolvedSources);
        }

        [Fact]
        public void ShouldSetResolvedRoot()
        {
            File.WriteAllText(Path.Combine(_projectDir, "root-test.cs"), "// root");

            var task = new SdkTasks.Compilation.SourceFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                Sources = new ITaskItem[] { new TaskItem("root-test.cs") }
            };

            task.Execute();

            Assert.NotNull(task.ResolvedRoot);
            Assert.NotEmpty(task.ResolvedRoot!);
        }
    }
}
