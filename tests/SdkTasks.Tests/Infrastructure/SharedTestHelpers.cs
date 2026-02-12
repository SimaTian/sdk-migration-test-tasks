using Microsoft.Build.Framework;
using Xunit;

namespace SdkTasks.Tests.Infrastructure
{
    /// <summary>
    /// Shared helpers to reduce boilerplate across MSBuild task migration tests.
    /// </summary>
    public static class SharedTestHelpers
    {
        // ── Setup helpers ──

        /// <summary>
        /// Creates a standard test context: non-CWD temp directory + MockBuildEngine.
        /// Caller is responsible for disposing via CleanupTempDirectory.
        /// </summary>
        public static (string projectDir, MockBuildEngine engine) CreateTestContext()
        {
            return (TestHelper.CreateNonCwdTempDirectory(), new MockBuildEngine());
        }

        /// <summary>
        /// Creates a TrackingTaskEnvironment bound to the given project directory.
        /// </summary>
        public static TrackingTaskEnvironment CreateTrackingEnv(string projectDir)
        {
            return new TrackingTaskEnvironment { ProjectDirectory = projectDir };
        }

        // ── Path-resolution assertions ──

        /// <summary>
        /// Asserts that the given path starts with the expected project directory.
        /// </summary>
        public static void AssertPathUnderProjectDir(string projectDir, string actualPath)
        {
            Assert.StartsWith(projectDir, actualPath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Asserts that all ITaskItem ItemSpecs start with the expected project directory.
        /// </summary>
        public static void AssertAllItemsUnderProjectDir(string projectDir, ITaskItem[] items)
        {
            Assert.NotEmpty(items);
            foreach (var item in items)
                Assert.StartsWith(projectDir, item.ItemSpec, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Asserts that the resolved path does NOT accidentally fall under the process CWD
        /// (only meaningful when projectDir != CWD).
        /// </summary>
        public static void AssertPathNotUnderCwd(string projectDir, string actualPath)
        {
            string cwd = Directory.GetCurrentDirectory();
            if (!projectDir.StartsWith(cwd, StringComparison.OrdinalIgnoreCase))
                Assert.DoesNotContain(cwd, actualPath, StringComparison.OrdinalIgnoreCase);
        }

        // ── Forbidden-API detection (TrackingTaskEnvironment) ──

        /// <summary>
        /// Asserts that GetAbsolutePath was called at least <paramref name="minCalls"/> times.
        /// </summary>
        public static void AssertGetAbsolutePathCalled(TrackingTaskEnvironment env, int minCalls = 1)
        {
            Assert.True(env.GetAbsolutePathCallCount >= minCalls,
                $"Expected GetAbsolutePath to be called >= {minCalls} time(s), " +
                $"but was called {env.GetAbsolutePathCallCount} time(s).");
        }

        /// <summary>
        /// Asserts that GetCanonicalForm was called at least <paramref name="minCalls"/> times.
        /// </summary>
        public static void AssertGetCanonicalFormCalled(TrackingTaskEnvironment env, int minCalls = 1)
        {
            Assert.True(env.GetCanonicalFormCallCount >= minCalls,
                $"Expected GetCanonicalForm to be called >= {minCalls} time(s), " +
                $"but was called {env.GetCanonicalFormCallCount} time(s).");
        }

        /// <summary>
        /// Asserts that GetEnvironmentVariable was called at least <paramref name="minCalls"/> times.
        /// </summary>
        public static void AssertGetEnvironmentVariableCalled(TrackingTaskEnvironment env, int minCalls = 1)
        {
            Assert.True(env.GetEnvironmentVariableCallCount >= minCalls,
                $"Expected GetEnvironmentVariable to be called >= {minCalls} time(s), " +
                $"but was called {env.GetEnvironmentVariableCallCount} time(s).");
        }
    }
}
