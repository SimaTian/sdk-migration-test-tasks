// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.Build.Framework;
using Xunit;

namespace SdkTasks.Tests.Infrastructure
{
    /// <summary>
    /// Shared assertion and setup helpers to reduce boilerplate across task tests.
    /// </summary>
    public static class SharedTestHelpers
    {
        // --- Tracking assertions ---

        /// <summary>
        /// Asserts that the task called TaskEnvironment.GetAbsolutePath at least once.
        /// </summary>
        public static void AssertUsesGetAbsolutePath(TrackingTaskEnvironment tracking)
        {
            Assert.True(tracking.GetAbsolutePathCallCount > 0,
                "Task should call TaskEnvironment.GetAbsolutePath instead of resolving paths directly");
        }

        /// <summary>
        /// Asserts that the task called TaskEnvironment.GetEnvironmentVariable at least once.
        /// </summary>
        public static void AssertUsesGetEnvironmentVariable(TrackingTaskEnvironment tracking)
        {
            Assert.True(tracking.GetEnvironmentVariableCallCount > 0,
                "Task should call TaskEnvironment.GetEnvironmentVariable instead of Environment.GetEnvironmentVariable");
        }

        /// <summary>
        /// Asserts that the task called TaskEnvironment.GetCanonicalForm at least once.
        /// </summary>
        public static void AssertUsesGetCanonicalForm(TrackingTaskEnvironment tracking)
        {
            Assert.True(tracking.GetCanonicalFormCallCount > 0,
                "Task should call TaskEnvironment.GetCanonicalForm for path canonicalization");
        }

        /// <summary>
        /// Asserts that GetAbsolutePath was called at least <paramref name="minCalls"/> times.
        /// </summary>
        public static void AssertMinimumGetAbsolutePathCalls(TrackingTaskEnvironment tracking, int minCalls)
        {
            Assert.True(tracking.GetAbsolutePathCallCount >= minCalls,
                $"Expected >= {minCalls} GetAbsolutePath calls, got {tracking.GetAbsolutePathCallCount}");
        }

        /// <summary>
        /// Asserts that GetCanonicalForm was called at least <paramref name="minCalls"/> times.
        /// </summary>
        public static void AssertMinimumGetCanonicalFormCalls(TrackingTaskEnvironment tracking, int minCalls)
        {
            Assert.True(tracking.GetCanonicalFormCallCount >= minCalls,
                $"Expected >= {minCalls} GetCanonicalForm calls, got {tracking.GetCanonicalFormCallCount}");
        }

        /// <summary>
        /// Asserts that GetEnvironmentVariable was called at least <paramref name="minCalls"/> times.
        /// </summary>
        public static void AssertMinimumGetEnvironmentVariableCalls(TrackingTaskEnvironment tracking, int minCalls)
        {
            Assert.True(tracking.GetEnvironmentVariableCallCount >= minCalls,
                $"Expected >= {minCalls} GetEnvironmentVariable calls, got {tracking.GetEnvironmentVariableCallCount}");
        }

        // --- Path assertions ---

        /// <summary>
        /// Asserts that the resolved path starts with the given project directory.
        /// </summary>
        public static void AssertPathUnderProjectDir(string projectDir, string resolvedPath)
        {
            Assert.StartsWith(projectDir, resolvedPath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Asserts that a resolved path is under projectDir and NOT under the process CWD
        /// (when they differ).
        /// </summary>
        public static void AssertNotResolvedToCwd(string resolvedPath, string projectDir)
        {
            AssertPathUnderProjectDir(projectDir, resolvedPath);
            string cwd = Directory.GetCurrentDirectory();
            if (!cwd.Equals(projectDir, StringComparison.OrdinalIgnoreCase))
            {
                Assert.False(resolvedPath.StartsWith(cwd, StringComparison.OrdinalIgnoreCase),
                    "Resolved path must not be relative to process CWD");
            }
        }

        /// <summary>
        /// Uses reflection to enumerate all [Output] string properties on a task and asserts
        /// each non-empty value starts with the given project directory.
        /// </summary>
        public static void AssertAllStringOutputsUnderProjectDir(object task, string projectDir)
        {
            var outputProps = task.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<OutputAttribute>() != null && p.PropertyType == typeof(string));

            foreach (var prop in outputProps)
            {
                var value = (string?)prop.GetValue(task);
                if (!string.IsNullOrEmpty(value))
                {
                    Assert.True(value!.StartsWith(projectDir, StringComparison.OrdinalIgnoreCase),
                        $"[Output] property '{prop.Name}' value '{value}' should start with ProjectDirectory '{projectDir}'");
                }
            }
        }

        // --- Setup helpers ---

        /// <summary>
        /// Creates a TrackingTaskEnvironment with the given project directory.
        /// </summary>
        public static TrackingTaskEnvironment CreateTrackingEnvironment(string projectDir)
        {
            return new TrackingTaskEnvironment { ProjectDirectory = projectDir };
        }
    }

    /// <summary>
    /// Disposable test context that manages temp directories and a MockBuildEngine.
    /// Use this to eliminate constructor/Dispose boilerplate in test classes.
    /// </summary>
    public sealed class TaskTestContext : IDisposable
    {
        private readonly List<string> _tempDirs = new();

        /// <summary>Primary project directory (created automatically).</summary>
        public string ProjectDir { get; }

        /// <summary>Shared MockBuildEngine instance.</summary>
        public MockBuildEngine Engine { get; }

        public TaskTestContext()
        {
            ProjectDir = TestHelper.CreateNonCwdTempDirectory();
            _tempDirs.Add(ProjectDir);
            Engine = new MockBuildEngine();
        }

        /// <summary>Creates an additional temp directory tracked for cleanup.</summary>
        public string CreateAdditionalProjectDir()
        {
            var dir = TestHelper.CreateNonCwdTempDirectory();
            _tempDirs.Add(dir);
            return dir;
        }

        public void Dispose()
        {
            foreach (var dir in _tempDirs)
                TestHelper.CleanupTempDirectory(dir);
        }
    }
}
