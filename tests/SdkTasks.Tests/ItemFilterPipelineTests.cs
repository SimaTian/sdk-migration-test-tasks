using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests
{
    public class ItemFilterPipelineTests : IDisposable
    {
        private readonly TaskTestContext _ctx;
        private string _tempDir => _ctx.ProjectDir;
        private MockBuildEngine _engine => _ctx.Engine;

        public ItemFilterPipelineTests() => _ctx = new TaskTestContext();
        public void Dispose() => _ctx.Dispose();

        [Fact]
        public void Execute_ExternalReference_ShouldResolveToProjectDir()
        {
            var item = new TaskItem("ext-ref.dll");
            item.SetMetadata("Category", "ExternalReference");

            var taskEnv = SharedTestHelpers.CreateTrackingEnvironment(_tempDir);
            var task = new SdkTasks.Compilation.ItemFilterPipeline
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                InputItems = new ITaskItem[] { item }
            };

            bool result = task.Execute();

            Assert.True(result, $"Execute failed. Errors: {string.Join("; ", _engine.Errors.Select(e => e.Message))}");
            Assert.NotEmpty(task.FilteredItems);
            var resolvedItem = task.FilteredItems[0];
            Assert.StartsWith(_tempDir, resolvedItem.ItemSpec, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Execute_ExternalReference_UsesGetAbsolutePath()
        {
            var item = new TaskItem("ext-ref.dll");
            item.SetMetadata("Category", "ExternalReference");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_tempDir);
            var task = new SdkTasks.Compilation.ItemFilterPipeline
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                InputItems = new ITaskItem[] { item }
            };

            task.Execute();

            SharedTestHelpers.AssertMinimumGetAbsolutePathCalls(trackingEnv, 2);
        }

        [Fact]
        public void Execute_DefaultCategory_UsesGetCanonicalForm()
        {
            var item = new TaskItem("source.cs");
            item.SetMetadata("Category", "Default");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_tempDir);
            var task = new SdkTasks.Compilation.ItemFilterPipeline
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                InputItems = new ITaskItem[] { item }
            };

            task.Execute();

            SharedTestHelpers.AssertUsesGetCanonicalForm(trackingEnv);
            SharedTestHelpers.AssertUsesGetAbsolutePath(trackingEnv);
        }

        [Fact]
        public void Execute_RelativeOutputDirectory_ResolvesToProjectDir()
        {
            var item = new TaskItem("lib.dll");
            item.SetMetadata("Category", "ExternalReference");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_tempDir);
            var task = new SdkTasks.Compilation.ItemFilterPipeline
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                InputItems = new ITaskItem[] { item },
                OutputDirectory = "bin"
            };

            task.Execute();

            Assert.Contains("bin", trackingEnv.GetAbsolutePathArgs);
        }

        [Fact]
        public void Execute_ExcludedItem_IsFiltered()
        {
            var included = new TaskItem("keep.dll");
            included.SetMetadata("Category", "ExternalReference");

            var excluded = new TaskItem("skip.dll");
            excluded.SetMetadata("Exclude", "true");
            excluded.SetMetadata("Category", "ExternalReference");

            var taskEnv = SharedTestHelpers.CreateTrackingEnvironment(_tempDir);
            var task = new SdkTasks.Compilation.ItemFilterPipeline
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                InputItems = new ITaskItem[] { included, excluded }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Single(task.FilteredItems);
            Assert.Contains(_tempDir, task.FilteredItems[0].ItemSpec, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Execute_FilterPattern_MatchesCorrectItems()
        {
            var match = new TaskItem("helper.cs");
            match.SetMetadata("Category", "Default");

            var noMatch = new TaskItem("program.dll");
            noMatch.SetMetadata("Category", "ExternalReference");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_tempDir);
            var task = new SdkTasks.Compilation.ItemFilterPipeline
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                InputItems = new ITaskItem[] { match, noMatch },
                FilterPattern = "helper"
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Single(task.FilteredItems);
        }

        [Fact]
        public void Execute_EmptyInputItems_ReturnsEmpty()
        {
            var taskEnv = SharedTestHelpers.CreateTrackingEnvironment(_tempDir);
            var task = new SdkTasks.Compilation.ItemFilterPipeline
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                InputItems = Array.Empty<ITaskItem>()
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Empty(task.FilteredItems);
        }

        [Fact]
        public void Execute_TmpExtension_IsFilteredOut()
        {
            var item = new TaskItem("temp.tmp");
            item.SetMetadata("Category", "Default");

            var taskEnv = SharedTestHelpers.CreateTrackingEnvironment(_tempDir);
            var task = new SdkTasks.Compilation.ItemFilterPipeline
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                InputItems = new ITaskItem[] { item }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Empty(task.FilteredItems);
        }

        [Fact]
        public void Execute_MultipleItems_AllResolvedPathsContainProjectDir()
        {
            var item1 = new TaskItem("ref1.dll");
            item1.SetMetadata("Category", "ExternalReference");

            var item2 = new TaskItem("ref2.dll");
            item2.SetMetadata("Category", "ExternalReference");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_tempDir);
            var task = new SdkTasks.Compilation.ItemFilterPipeline
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                InputItems = new ITaskItem[] { item1, item2 }
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(2, task.FilteredItems.Length);
            foreach (var filtered in task.FilteredItems)
            {
                Assert.StartsWith(_tempDir, filtered.ItemSpec, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
