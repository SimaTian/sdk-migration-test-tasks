using System;
using System.IO;
using FluentAssertions;
using Microsoft.Build.Framework;
using SdkTasks.Compatibility;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests.Compatibility
{
    public class GivenALegacyPathResolverMultiThreading : IDisposable
    {
        private readonly string _tempDir;

        public GivenALegacyPathResolverMultiThreading()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"legacypath-{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                try
                {
                    Directory.Delete(_tempDir, true);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }

        [Fact]
        public void ItImplementsIMultiThreadableTask()
        {
            var task = new LegacyPathResolver();
            task.Should().BeAssignableTo<IMultiThreadableTask>();
        }

        [Fact]
        public void ItHasMSBuildMultiThreadableTaskAttribute()
        {
            typeof(LegacyPathResolver).Should().BeDecoratedWith<MSBuildMultiThreadableTaskAttribute>();
        }

        [Fact]
        public void ItResolvesRelativePathsViaTaskEnvironment()
        {
            // Create a project directory different from CWD
            var projectDir = Path.Combine(_tempDir, "project");
            Directory.CreateDirectory(projectDir);

            // Create a test file under the project directory
            var relativeInputPath = "config/settings.json";
            var absoluteExpectedPath = Path.Combine(projectDir, relativeInputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteExpectedPath)!);
            File.WriteAllText(absoluteExpectedPath, "{}");

            // Set up task with TaskEnvironment pointing to projectDir
            var engine = new MockBuildEngine();
            var task = new LegacyPathResolver
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                InputPath = relativeInputPath,
                EnvVarName = "TEST_VAR"
            };

            // Execute
            var result = task.Execute();

            // Verify
            result.Should().BeTrue();
            engine.Messages.Should().ContainSingle(m =>
                m.Message != null && m.Message.Contains(absoluteExpectedPath));
        }

        [Fact]
        public void ItAutoInitializesProjectDirectoryFromBuildEngine()
        {
            // Create a project directory
            var projectDir = Path.Combine(_tempDir, "autoproject");
            Directory.CreateDirectory(projectDir);
            var projectFile = Path.Combine(projectDir, "test.csproj");
            File.WriteAllText(projectFile, "<Project />");

            // Create a test file under the project directory
            var relativeInputPath = "src/App.cs";
            var absoluteExpectedPath = Path.Combine(projectDir, relativeInputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteExpectedPath)!);
            File.WriteAllText(absoluteExpectedPath, "// test");

            // Set up task with MockBuildEngine but WITHOUT explicitly setting TaskEnvironment.ProjectDirectory
            var engine = new MockBuildEngine
            {
                ProjectFileOfTaskNode = projectFile
            };
            var task = new LegacyPathResolver
            {
                BuildEngine = engine,
                InputPath = relativeInputPath,
                EnvVarName = "TEST_VAR"
            };

            // TaskEnvironment property should exist but ProjectDirectory will be empty initially
            task.TaskEnvironment.Should().NotBeNull();

            // Execute - should auto-initialize ProjectDirectory from BuildEngine.ProjectFileOfTaskNode
            var result = task.Execute();

            // Verify
            result.Should().BeTrue();
            task.TaskEnvironment.ProjectDirectory.Should().Be(projectDir);
            engine.Messages.Should().ContainSingle(m =>
                m.Message != null && m.Message.Contains(absoluteExpectedPath));
        }

        [Fact]
        public void ItResolvesEnvironmentVariablesViaTaskEnvironment()
        {
            // Create a project directory
            var projectDir = Path.Combine(_tempDir, "envproject");
            Directory.CreateDirectory(projectDir);

            // Set up a TaskEnvironment with a custom environment variable
            var taskEnv = TaskEnvironmentHelper.CreateForTest(projectDir);
            taskEnv.SetEnvironmentVariable("CUSTOM_CONFIG", "production");

            // Set up task
            var engine = new MockBuildEngine();
            var task = new LegacyPathResolver
            {
                BuildEngine = engine,
                TaskEnvironment = taskEnv,
                InputPath = "dummy.txt",
                EnvVarName = "CUSTOM_CONFIG"
            };

            // Execute
            var result = task.Execute();

            // Verify the log message contains the environment variable value
            result.Should().BeTrue();
            engine.Messages.Should().ContainSingle(m =>
                m.Message != null && m.Message.Contains("CUSTOM_CONFIG=production"));
        }

        [Fact]
        public void ItHandlesMissingEnvironmentVariable()
        {
            // Create a project directory
            var projectDir = Path.Combine(_tempDir, "missingenv");
            Directory.CreateDirectory(projectDir);

            // Set up task with an environment variable that doesn't exist
            var engine = new MockBuildEngine();
            var task = new LegacyPathResolver
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                InputPath = "test.txt",
                EnvVarName = "NONEXISTENT_VAR"
            };

            // Execute
            var result = task.Execute();

            // Verify the log message indicates the variable is not set
            result.Should().BeTrue();
            engine.Messages.Should().ContainSingle(m =>
                m.Message != null &&
                m.Message.Contains("Environment variable 'NONEXISTENT_VAR' is not set") &&
                m.Importance == MessageImportance.Low);
        }

        [Fact]
        public void ItPreservesPublicProperties()
        {
            // Verify all expected public properties exist with correct types
            var taskType = typeof(LegacyPathResolver);

            var taskEnvProp = taskType.GetProperty("TaskEnvironment");
            taskEnvProp.Should().NotBeNull();
            taskEnvProp!.PropertyType.Should().Be(typeof(TaskEnvironment));

            var inputPathProp = taskType.GetProperty("InputPath");
            inputPathProp.Should().NotBeNull();
            inputPathProp!.PropertyType.Should().Be(typeof(string));
            inputPathProp.GetCustomAttributes(typeof(RequiredAttribute), false).Should().NotBeEmpty();

            var envVarProp = taskType.GetProperty("EnvVarName");
            envVarProp.Should().NotBeNull();
            envVarProp!.PropertyType.Should().Be(typeof(string));
            envVarProp.GetCustomAttributes(typeof(RequiredAttribute), false).Should().NotBeEmpty();
        }
    }
}
