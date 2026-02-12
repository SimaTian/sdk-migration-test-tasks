using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;
using SdkTasks.Tools;
using Xunit;

#nullable disable

namespace SdkTasks.Tests
{
    public class ProcessTerminatorTests : IDisposable
    {
        private readonly TaskTestContext _ctx;

        public ProcessTerminatorTests() => _ctx = new TaskTestContext();

        private string CreateProjectDir() => _ctx.CreateAdditionalProjectDir();

        public void Dispose() => _ctx.Dispose();

        // ── Correct error behavior (no process kill) ────────────────────

        [Fact]
        public void Execute_ShouldReturnFalseAndLogError()
        {
            var engine = new MockBuildEngine();
            var task = new ProcessTerminator
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest()
            };

            bool result = task.Execute();

            Assert.False(result);
            Assert.Single(engine.Errors);
        }

        [Fact]
        public void Execute_ShouldLogForbiddenOperationError()
        {
            var engine = new MockBuildEngine();
            var task = new ProcessTerminator
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest()
            };

            task.Execute();

            Assert.Contains(engine.Errors, e => e.Message.Contains("kill"));
        }

        [Fact]
        public void Execute_ShouldLogCleanupMessage()
        {
            var engine = new MockBuildEngine();
            var task = new ProcessTerminator
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest()
            };

            task.Execute();

            Assert.Contains(engine.Messages, m =>
                m.Message.Contains("cleanup", System.StringComparison.OrdinalIgnoreCase));
        }

        // ── ProjectDirectory-aware behavior ─────────────────────────────

        [Fact]
        public void Execute_WithNonCwdProjectDirectory_ShouldStillReturnFalseAndLogError()
        {
            var projectDir = CreateProjectDir();
            var engine = new MockBuildEngine();

            var task = new ProcessTerminator
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir)
            };

            bool result = task.Execute();

            Assert.False(result);
            Assert.Single(engine.Errors);
            Assert.Contains(engine.Errors, e =>
                e.Message.Contains("forbidden", System.StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ShouldAcceptTrackingTaskEnvironment()
        {
            var projectDir = CreateProjectDir();
            var tracking = SharedTestHelpers.CreateTrackingEnvironment(projectDir);
            var engine = new MockBuildEngine();

            var task = new ProcessTerminator
            {
                BuildEngine = engine,
                TaskEnvironment = tracking
            };

            bool result = task.Execute();

            // Task should work correctly with a TrackingTaskEnvironment (polymorphism)
            Assert.False(result);
            Assert.Single(engine.Errors);
        }

        [Fact]
        public void TwoTasksWithDifferentProjectDirs_BothReturnFalseAndLogError()
        {
            var dir1 = CreateProjectDir();
            var dir2 = CreateProjectDir();
            var engine1 = new MockBuildEngine();
            var engine2 = new MockBuildEngine();

            var task1 = new ProcessTerminator
            {
                BuildEngine = engine1,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir1)
            };

            var task2 = new ProcessTerminator
            {
                BuildEngine = engine2,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir2)
            };

            Assert.False(task1.Execute());
            Assert.False(task2.Execute());

            Assert.Single(engine1.Errors);
            Assert.Single(engine2.Errors);
        }
    }
}
