using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Build;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests
{
    public class GivenAFrameworkAssemblyLocatorMultiThreading
    {
        [Fact]
        public void ItUsesTaskEnvironmentForDotNetRoot()
        {
            // Arrange
            var task = new FrameworkAssemblyLocator();
            var engine = new MockBuildEngine();
            task.BuildEngine = engine;
            
            var taskEnv = new TrackingTaskEnvironment();
            // Use a unique fake path to avoid collisions
            var fakeDotNetRoot = Path.Combine(@"C:\FakeDotNetRoot", Guid.NewGuid().ToString());
            taskEnv.SetEnvironmentVariable("DOTNET_ROOT", fakeDotNetRoot);
            taskEnv.ProjectDirectory = Path.GetTempPath(); // Just needs to be valid
            
            task.TaskEnvironment = taskEnv;
            task.References = new ITaskItem[] { new TaskItem("System.Runtime") };
            
            // Act
            task.Execute();
            
            // Assert
            // The task should find DOTNET_ROOT via TaskEnvironment and log the path
            engine.Messages.Should().Contain(m => m.Message.Contains(fakeDotNetRoot), 
                "Should use DOTNET_ROOT from TaskEnvironment to build search paths");
        }

        [Fact]
        public void ItInitializesProjectDirectoryDefensively()
        {
            // Arrange
            var task = new FrameworkAssemblyLocator();
            var engine = new MockBuildEngine();
            string projectDir = Path.Combine(Path.GetTempPath(), "TestProject_" + Guid.NewGuid());
            Directory.CreateDirectory(projectDir);
            string projectFile = Path.Combine(projectDir, "test.csproj");
            
            engine.ProjectFileOfTaskNode = projectFile;
            task.BuildEngine = engine;
            
            // Do NOT set TaskEnvironment.ProjectDirectory explicitly
            task.TaskEnvironment = new TaskEnvironment(); 
            // Note: In real scenarios, the task runner might set it, but we want to ensure 
            // the task handles the case where it's missing (e.g. single-threaded mode fallback logic
            // or just robust initialization)
            
            task.References = new ITaskItem[0];

            // Act
            task.Execute();

            // Assert
            task.TaskEnvironment.ProjectDirectory.Should().Be(projectDir, 
                "Should initialize ProjectDirectory from BuildEngine if missing");
        }

        [Fact]
        public void ItUsesTaskEnvironmentForPathResolution()
        {
            // Arrange
            var task = new FrameworkAssemblyLocator();
            var engine = new MockBuildEngine();
            task.BuildEngine = engine;
            
            var taskEnv = new TrackingTaskEnvironment();
            string projectDir = Path.Combine(Path.GetTempPath(), "Project_" + Guid.NewGuid());
            taskEnv.ProjectDirectory = projectDir;
            
            // Use a relative path for DOTNET_ROOT
            string relativeDotNet = "relative\\dotnet";
            taskEnv.SetEnvironmentVariable("DOTNET_ROOT", relativeDotNet);
            
            task.TaskEnvironment = taskEnv;
            task.References = new ITaskItem[] { new TaskItem("System.Runtime") };
            
            // Act
            task.Execute();
            
            // Assert
            // If migrated correctly, it uses TaskEnvironment.GetAbsolutePath which combines ProjectDir + relative
            // If unmigrated, it uses Path.GetFullPath which combines CWD + relative
            
            string expectedPathFragment = Path.Combine(projectDir, relativeDotNet);
            
            engine.Messages.Should().Contain(m => m.Message.Contains(expectedPathFragment),
                "Should resolve relative DOTNET_ROOT against ProjectDirectory, not CWD");
        }
    }
}
