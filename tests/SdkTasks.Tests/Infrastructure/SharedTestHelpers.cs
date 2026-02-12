// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

        // --- Path assertions ---

        /// <summary>
        /// Asserts that the resolved path starts with the given project directory.
        /// </summary>
        public static void AssertPathUnderProjectDir(string projectDir, string resolvedPath)
        {
            Assert.StartsWith(projectDir, resolvedPath, StringComparison.OrdinalIgnoreCase);
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
}
