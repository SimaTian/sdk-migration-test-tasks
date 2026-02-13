using System;
using System.IO;
using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Build;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests
{
    public class GivenAOutputPathConfiguratorMultiThreading : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public GivenAOutputPathConfiguratorMultiThreading()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ItHasMultiThreadableAttribute()
        {
            typeof(OutputPathConfigurator).Should().BeDecoratedWith<MSBuildMultiThreadableTaskAttribute>();
        }

        [Fact]
        public void ItImplementsIMultiThreadableTask()
        {
            typeof(OutputPathConfigurator).Should().Implement<IMultiThreadableTask>();
        }

        [Fact]
        public void ItHasDefaultTaskEnvironment()
        {
            // This test verifies that TaskEnvironment is initialized to a non-null value by default.
            // This prevents NRE if the task is used without explicit dependency injection.
            var task = new OutputPathConfigurator();
            task.TaskEnvironment.Should().NotBeNull();
        }

        [Fact]
        public void ItResolvesPathsUsingTaskEnvironment()
        {
            var taskEnv = new TrackingTaskEnvironment { ProjectDirectory = _projectDir };
            var task = new OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                OutputDirectory = "bin"
            };

            task.Execute();

            // Verify GetAbsolutePath was called
            taskEnv.GetAbsolutePathCallCount.Should().BeGreaterThan(0);
            taskEnv.GetAbsolutePathArgs.Should().Contain("bin");
            
            // Verify output is correct (absolute path)
            var expected = Path.Combine(_projectDir, "bin") + Path.DirectorySeparatorChar;
            task.ResolvedOutputDirectory.Should().Be(expected);
        }

        [Fact]
        public void ItInitializesProjectDirectoryFromBuildEngine()
        {
             // Do NOT set ProjectDirectory in TaskEnvironment
            var env = new TrackingTaskEnvironment(); 
            var task = new OutputPathConfigurator
            {
                BuildEngine = _engine,
                TaskEnvironment = env,
                OutputDirectory = "bin"
            };

            // Mock engine returns absolute path
            var projectFile = Path.Combine(_projectDir, "test.csproj");
            _engine.ProjectFileOfTaskNode = projectFile;

            task.Execute();

            env.ProjectDirectory.Should().Be(_projectDir);
        }
    }
}
