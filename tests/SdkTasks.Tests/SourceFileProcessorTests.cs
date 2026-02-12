using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class SourceFileProcessorTests : IDisposable
    {
        private readonly TaskTestContext _ctx;
        private string _projectDir => _ctx.ProjectDir;
        private MockBuildEngine _engine => _ctx.Engine;

        public SourceFileProcessorTests() => _ctx = new TaskTestContext();
        public void Dispose() => _ctx.Dispose();

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
            SharedTestHelpers.AssertPathUnderProjectDir(_projectDir, resolved);
        }

        [Fact]
        public void ShouldCallGetAbsolutePathForRelativeSources()
        {
            File.WriteAllText(Path.Combine(_projectDir, "app.cs"), "// app");

            var tracking = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new SdkTasks.Compilation.SourceFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = tracking,
                Sources = new ITaskItem[] { new TaskItem("app.cs") }
            };

            bool result = task.Execute();

            Assert.True(result);
            SharedTestHelpers.AssertUsesGetAbsolutePath(tracking);
            Assert.Contains("app.cs", tracking.GetAbsolutePathArgs);
        }

        [Fact]
        public void ShouldNotCallGetAbsolutePathForRootedPaths()
        {
            string absoluteSource = Path.Combine(_projectDir, "rooted.cs");
            File.WriteAllText(absoluteSource, "// rooted");

            var tracking = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new SdkTasks.Compilation.SourceFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = tracking,
                Sources = new ITaskItem[] { new TaskItem(absoluteSource) }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.DoesNotContain(absoluteSource, tracking.GetAbsolutePathArgs);
            Assert.NotEmpty(task.ResolvedSources);
            Assert.Equal(absoluteSource, task.ResolvedSources[0].ItemSpec);
        }

        [Fact]
        public void ShouldResolveMultipleSourcesToProjectDirectory()
        {
            File.WriteAllText(Path.Combine(_projectDir, "a.cs"), "// a");
            File.WriteAllText(Path.Combine(_projectDir, "b.cs"), "// b");

            var tracking = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new SdkTasks.Compilation.SourceFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = tracking,
                Sources = new ITaskItem[] { new TaskItem("a.cs"), new TaskItem("b.cs") }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(2, task.ResolvedSources.Length);
            foreach (var item in task.ResolvedSources)
            {
                Assert.StartsWith(_projectDir, item.ItemSpec, StringComparison.OrdinalIgnoreCase);
            }
            Assert.Equal(2, tracking.GetAbsolutePathCallCount);
        }

        [Fact]
        public void ShouldSetOriginalIdentityMetadata()
        {
            File.WriteAllText(Path.Combine(_projectDir, "file.cs"), "// file");

            var task = new SdkTasks.Compilation.SourceFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                Sources = new ITaskItem[] { new TaskItem("file.cs") }
            };

            task.Execute();

            Assert.NotEmpty(task.ResolvedSources);
            Assert.Equal("file.cs", task.ResolvedSources[0].GetMetadata("OriginalIdentity"));
        }

        [Fact]
        public void ShouldComputeResolvedRootFromProjectDir()
        {
            File.WriteAllText(Path.Combine(_projectDir, "x.cs"), "// x");

            var task = new SdkTasks.Compilation.SourceFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                Sources = new ITaskItem[] { new TaskItem("x.cs") }
            };

            task.Execute();

            Assert.False(string.IsNullOrEmpty(task.ResolvedRoot));
            Assert.StartsWith(_projectDir, task.ResolvedRoot!, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldReturnEmptyForNoSources()
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
            Assert.Equal(string.Empty, task.ResolvedRoot);
        }

        [Fact]
        public void ShouldResolveSubdirectorySourcesToProjectDir()
        {
            string subDir = Path.Combine(_projectDir, "sub");
            Directory.CreateDirectory(subDir);
            File.WriteAllText(Path.Combine(subDir, "nested.cs"), "// nested");

            var tracking = SharedTestHelpers.CreateTrackingEnvironment(_projectDir);
            var task = new SdkTasks.Compilation.SourceFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = tracking,
                Sources = new ITaskItem[] { new TaskItem(Path.Combine("sub", "nested.cs")) }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.NotEmpty(task.ResolvedSources);
            string resolved = task.ResolvedSources[0].ItemSpec;
            SharedTestHelpers.AssertPathUnderProjectDir(_projectDir, resolved);
            Assert.Contains("nested.cs", resolved);
            Assert.True(tracking.GetAbsolutePathCallCount > 0);
        }

        [Fact]
        public void ShouldCopyStandardMetadata()
        {
            File.WriteAllText(Path.Combine(_projectDir, "meta.cs"), "// meta");

            var source = new TaskItem("meta.cs");
            source.SetMetadata("Link", "Linked\\meta.cs");
            source.SetMetadata("CopyToOutputDirectory", "PreserveNewest");

            var task = new SdkTasks.Compilation.SourceFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                Sources = new ITaskItem[] { source }
            };

            task.Execute();

            Assert.NotEmpty(task.ResolvedSources);
            Assert.Equal("Linked\\meta.cs", task.ResolvedSources[0].GetMetadata("Link"));
            Assert.Equal("PreserveNewest", task.ResolvedSources[0].GetMetadata("CopyToOutputDirectory"));
        }

        [Fact]
        public void ShouldNotResolveRelativePathsToCwd()
        {
            // Verify the task resolves to ProjectDirectory, not process CWD
            string cwd = Directory.GetCurrentDirectory();
            Assert.NotEqual(cwd, _projectDir);

            File.WriteAllText(Path.Combine(_projectDir, "check.cs"), "// check");

            var task = new SdkTasks.Compilation.SourceFileProcessor
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir),
                Sources = new ITaskItem[] { new TaskItem("check.cs") }
            };

            task.Execute();

            Assert.NotEmpty(task.ResolvedSources);
            string resolved = task.ResolvedSources[0].ItemSpec;
            SharedTestHelpers.AssertPathUnderProjectDir(_projectDir, resolved);
            Assert.DoesNotContain(cwd, resolved, StringComparison.OrdinalIgnoreCase);
        }
    }
}
