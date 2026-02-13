using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.Build.Framework;
using SdkTasks.Configuration;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests
{
    public class GivenASdkLocationProviderMultiThreading
    {
        [Fact]
        public void ItResolvesRelativeDotNetRootAgainstProjectDirectory()
        {
            // Arrange
            string projectDir = TestHelper.CreateNonCwdTempDirectory();
            string relativeSdkPath = "localSdk";
            string absoluteSdkPath = Path.Combine(projectDir, relativeSdkPath);
            
            Directory.CreateDirectory(absoluteSdkPath); 

            // Setup a fake structure so ProbeFrameworkDirectory finds something
            string tfm = "net8.0";
            string packsDir = Path.Combine(absoluteSdkPath, "packs", "Microsoft.NETCore.App.Ref");
            string versionDir = Path.Combine(packsDir, "8.0.0");
            string refDir = Path.Combine(versionDir, "ref", tfm);
            Directory.CreateDirectory(refDir);
            File.WriteAllText(Path.Combine(refDir, "System.Runtime.dll"), "fake assembly");

            var engine = new MockBuildEngine();
            var taskEnv = new TaskEnvironment
            {
                ProjectDirectory = projectDir
            };
            taskEnv.SetEnvironmentVariable("DOTNET_ROOT", relativeSdkPath);

            var task = new SdkLocationProvider
            {
                BuildEngine = engine,
                TargetFramework = tfm,
                TaskEnvironment = taskEnv
            };

            try
            {
                // Act
                bool result = task.Execute();

                // Assert
                result.Should().BeTrue("Task should succeed when SDK is in ProjectDirectory");
                task.FrameworkAssemblies.Should().NotBeEmpty();
                
                // Check log message to verify it used the absolute path
                engine.Messages.Should().Contain(m => m.Message.Contains(absoluteSdkPath));
            }
            finally
            {
                TestHelper.CleanupTempDirectory(projectDir);
            }
        }
    }
}
