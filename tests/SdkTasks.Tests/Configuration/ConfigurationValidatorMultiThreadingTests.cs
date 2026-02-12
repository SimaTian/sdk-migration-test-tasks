using System;
using System.IO;
using Microsoft.Build.Framework;
using SdkTasks.Configuration;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests.Configuration
{
    public class ConfigurationValidatorMultiThreadingTests : IDisposable
    {
        private readonly string _projectDir;

        public ConfigurationValidatorMultiThreadingTests()
        {
            // Create a temp dir that is NOT the process CWD
            _projectDir = Path.Combine(Path.GetTempPath(), "cv-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_projectDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_projectDir, true); } catch { }
        }

        /// <summary>
        /// Verifies that Directory.Exists in ResolveConfigPath resolves against
        /// TaskEnvironment.ProjectDirectory, not the process CWD.
        /// 
        /// Test setup: create a subdirectory under _projectDir (NOT CWD).
        /// Set TaskEnvironment.ProjectDirectory = _projectDir.
        /// Set ConfigKey to an env var whose value is the subdirectory name.
        /// 
        /// If the task resolves paths against ProjectDirectory, it finds the directory.
        /// If it resolves against CWD (because ProjectDirectory is empty/uninitialized),
        /// it does NOT find it (the subdirectory doesn't exist under CWD).
        /// </summary>
        [Fact]
        public void ItResolvesConfigPathRelativeToProjectDirectory()
        {
            // Arrange: create a subdirectory only under _projectDir
            var subDirName = "config-target";
            var subDirPath = Path.Combine(_projectDir, subDirName);
            Directory.CreateDirectory(subDirPath);

            var task = new ConfigurationValidator();
            task.BuildEngine = new MockBuildEngine();
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir);
            task.ConfigKey = "TEST_CONFIG_KEY_CV";
            task.TaskEnvironment.SetEnvironmentVariable("TEST_CONFIG_KEY_CV", subDirName);

            // Act
            var result = task.Execute();

            // Assert: task should succeed and ResolvedConfig should reference
            // the resolved path under _projectDir, not under CWD
            Assert.True(result, "Task should succeed");
            Assert.Contains(subDirName, task.ResolvedConfig);
            // The resolved path in the output should reference the project dir context
            Assert.Contains("context=", task.ResolvedConfig);
        }

        /// <summary>
        /// Verifies the task uses fallback value when env var is not set,
        /// and resolves the fallback path relative to ProjectDirectory.
        /// </summary>
        [Fact]
        public void ItUsesFallbackValueWhenEnvVarNotSet()
        {
            var subDirName = "fallback-dir";
            var subDirPath = Path.Combine(_projectDir, subDirName);
            Directory.CreateDirectory(subDirPath);

            var task = new ConfigurationValidator();
            task.BuildEngine = new MockBuildEngine();
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir);
            task.ConfigKey = "NONEXISTENT_CONFIG_KEY_CV_" + Guid.NewGuid().ToString("N");
            task.FallbackValue = subDirName;

            var result = task.Execute();

            Assert.True(result, "Task should succeed with fallback");
            Assert.Contains(subDirName, task.ResolvedConfig);
        }

        /// <summary>
        /// Verifies that when BuildEngine is set but TaskEnvironment.ProjectDirectory 
        /// is empty, the task auto-initializes ProjectDirectory from 
        /// BuildEngine.ProjectFileOfTaskNode before resolving paths.
        /// 
        /// EXPECTED TO FAIL before migration: Without the defensive initialization,
        /// ProjectDirectory stays empty and paths resolve against CWD.
        /// </summary>
        [Fact]
        public void ItAutoInitializesProjectDirectoryFromBuildEngine()
        {
            var subDirName = "auto-init-dir";
            var subDirPath = Path.Combine(_projectDir, subDirName);
            Directory.CreateDirectory(subDirPath);

            // Create a fake project file under _projectDir so BuildEngine returns it
            var fakeProjectFile = Path.Combine(_projectDir, "test.csproj");
            File.WriteAllText(fakeProjectFile, "<Project/>");

            var mockEngine = new MockBuildEngine();
            // MockBuildEngine.ProjectFileOfTaskNode should return the fake project file path

            var task = new ConfigurationValidator();
            task.BuildEngine = mockEngine;
            // Intentionally use default TaskEnvironment (ProjectDirectory is empty)
            // The task should auto-initialize from BuildEngine.ProjectFileOfTaskNode
            task.TaskEnvironment = new TaskEnvironment();
            task.ConfigKey = "TEST_AUTO_INIT_CV";
            task.TaskEnvironment.SetEnvironmentVariable("TEST_AUTO_INIT_CV", subDirName);

            var result = task.Execute();

            // Without defensive init, ProjectDirectory is empty, Path.Combine("", subDirName) = subDirName (relative),
            // and Directory.Exists resolves against CWD (where subDirName doesn't exist).
            Assert.True(result, "Task should succeed");
        }
    }
}
