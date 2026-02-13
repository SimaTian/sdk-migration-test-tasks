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
    public class DirectoryContextSwitcherTests : IDisposable
    {
        private readonly string _projectDir;

        public DirectoryContextSwitcherTests()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
        }

        public void Dispose()
        {
            TestHelper.CleanupTempDirectory(_projectDir);
        }

        [Fact]
        public void ItHasMultiThreadableAttribute()
        {
            typeof(DirectoryContextSwitcher).Should().BeDecoratedWith<MSBuildMultiThreadableTaskAttribute>();
        }

        [Fact]
        public void ItImplementsIMultiThreadableTask()
        {
            typeof(DirectoryContextSwitcher).Should().Implement<IMultiThreadableTask>();
        }

        [Fact]
        public void ItInitializesProjectDirectoryFromBuildEngine()
        {
            // Arrange
            var task = new DirectoryContextSwitcher();
            var engine = new MockBuildEngine();
            task.BuildEngine = engine;
            string projectFile = Path.Combine(_projectDir, "test.proj");
            engine.ProjectFileOfTaskNode = projectFile;
            
            task.NewDirectory = "output"; // Relative path

            // Act
            task.Execute();

            // Assert
            task.TaskEnvironment.ProjectDirectory.Should().Be(_projectDir);
            
            var logMessage = engine.Messages.Find(e => e.Message != null && e.Message.Contains("resolved from"));
            logMessage.Should().NotBeNull();
            logMessage!.Message.Should().Contain(Path.Combine(_projectDir, "output"));
        }

        [Fact]
        public void ItResolvesRelativeNewDirectoryAgainstProjectDirectory()
        {
            // Arrange
            var task = new DirectoryContextSwitcher();
            var engine = new MockBuildEngine();
            task.BuildEngine = engine;
            
            // Set ProjectDirectory explicitly (simulating what would happen if passed in or initialized)
            task.TaskEnvironment.ProjectDirectory = _projectDir;
            task.NewDirectory = "subdir/output";

            // Act
            task.Execute();

            // Assert
            var logMessage = engine.Messages.Find(e => e.Message != null && e.Message.Contains("resolved from"));
            logMessage.Should().NotBeNull();
            
            string expectedPath = Path.Combine(_projectDir, "subdir", "output");
            logMessage!.Message.Should().Contain(expectedPath);
        }
        
        [Fact]
        public void ItDoesNotDependOnCurrentDirectoryWhenProjectDirectoryIsSet()
        {
            // Arrange
            var task = new DirectoryContextSwitcher();
            var engine = new MockBuildEngine();
            task.BuildEngine = engine;
            task.TaskEnvironment.ProjectDirectory = _projectDir;
            task.NewDirectory = "subdir";
            
            string distractionDir = TestHelper.CreateNonCwdTempDirectory();
            string originalCwd = Directory.GetCurrentDirectory();
            
            try
            {
                Directory.SetCurrentDirectory(distractionDir);
                
                // Act
                task.Execute();
                
                // Assert
                var logMessage = engine.Messages.Find(e => e.Message != null && e.Message.Contains("resolved from"));
                logMessage.Should().NotBeNull();
                logMessage!.Message.Should().Contain(Path.Combine(_projectDir, "subdir"));
                logMessage.Message.Should().NotContain(distractionDir);
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCwd);
                TestHelper.CleanupTempDirectory(distractionDir);
            }
        }
    }
}
