using FluentAssertions;
using Microsoft.Build.Framework;
using SdkTasks.Build;
using SdkTasks.Tests.Infrastructure;
using System;
using System.IO;
using Xunit;

namespace SdkTasks.Tests
{
    public class GivenASharedBuildStateManagerMultiThreading : IDisposable
    {
        private readonly string _projectDir;
        private readonly MockBuildEngine _engine;

        public GivenASharedBuildStateManagerMultiThreading()
        {
            _projectDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_projectDir);

        [Fact]
        public void ItResolvesConfigFileRelativeToProjectDirectory()
        {
            var configFileName = "app.config";
            var expectedAbsPath = Path.Combine(_projectDir, configFileName);
            File.WriteAllText(expectedAbsPath, "test-config-content");

            var task = new SharedBuildStateManager
            {
                BuildEngine = _engine,
                ConfigFileName = configFileName,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
            };

            bool result = task.Execute();

            result.Should().BeTrue();
            task.ConfigFilePath.Should().NotBeNullOrEmpty();
            task.ConfigFilePath.Should().StartWith(_projectDir, 
                "Config file path should be rooted in ProjectDirectory");
            task.ConfigLoaded.Should().BeTrue();
        }

        [Fact]
        public void ItUsesTaskEnvironmentGetAbsolutePath()
        {
            var configFileName = "nested/folder/config.xml";
            var configDir = Path.Combine(_projectDir, "nested", "folder");
            Directory.CreateDirectory(configDir);
            var expectedAbsPath = Path.Combine(configDir, "config.xml");
            File.WriteAllText(expectedAbsPath, "nested-config");

            var trackingEnv = new TrackingTaskEnvironment(_projectDir);
            var task = new SharedBuildStateManager
            {
                BuildEngine = _engine,
                ConfigFileName = configFileName,
                TaskEnvironment = trackingEnv
            };

            bool result = task.Execute();

            result.Should().BeTrue();
            trackingEnv.GetAbsolutePathCallCount.Should().BeGreaterThan(0, 
                "Task must call TaskEnvironment.GetAbsolutePath for path resolution");
        }

        [Fact]
        public void ItCachesResolvedPathsAcrossInvocations()
        {
            var configFileName = "shared.config";
            var configPath = Path.Combine(_projectDir, configFileName);
            File.WriteAllText(configPath, "shared-content");

            var task1 = new SharedBuildStateManager
            {
                BuildEngine = _engine,
                ConfigFileName = configFileName,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
            };

            var task2 = new SharedBuildStateManager
            {
                BuildEngine = _engine,
                ConfigFileName = configFileName,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir)
            };

            task1.Execute().Should().BeTrue();
            task2.Execute().Should().BeTrue();

            task1.ConfigFilePath.Should().Be(task2.ConfigFilePath, 
                "Both tasks should resolve to the same cached path");
        }

        [Fact]
        public void ItAutoInitializesProjectDirectoryFromBuildEngine()
        {
            var configFileName = "auto-init.config";
            var configPath = Path.Combine(_projectDir, configFileName);
            File.WriteAllText(configPath, "content");

            var projectFilePath = Path.Combine(_projectDir, "test.proj");
            _engine.ProjectFileOfTaskNode = projectFilePath;

            var task = new SharedBuildStateManager
            {
                BuildEngine = _engine,
                ConfigFileName = configFileName,
                TaskEnvironment = new TaskEnvironment() // Empty TaskEnvironment
            };

            bool result = task.Execute();

            result.Should().BeTrue();
            task.TaskEnvironment.ProjectDirectory.Should().NotBeNullOrEmpty(
                "ProjectDirectory should be auto-initialized from BuildEngine");
            task.ConfigFilePath.Should().StartWith(_projectDir);
        }
    }
}
