// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
using TaskItem = Microsoft.Build.Utilities.TaskItem;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace SdkTasks.Build
{
    [MSBuildMultiThreadableTask]
    public class PathResolutionCache : MSBuildTask, IMultiThreadableTask
    {
        // Static cache shared across all task instances, keyed by (projectDir, relativePath).
        private static readonly Dictionary<(string, string), string> _pathCache = new();
        private static readonly object _cacheLock = new object();
        private static int _cacheHits;
        private static int _cacheMisses;

        public TaskEnvironment TaskEnvironment { get; set; } = new();

        [Required]
        public string[] InputPaths { get; set; } = Array.Empty<string>();

        [Output]
        public ITaskItem[] ResolvedPaths { get; set; } = Array.Empty<ITaskItem>();

        public override bool Execute()
        {
            // Auto-initialize ProjectDirectory from BuildEngine when not set
            if (string.IsNullOrEmpty(TaskEnvironment.ProjectDirectory) && BuildEngine != null)
            {
                string projectFile = BuildEngine.ProjectFileOfTaskNode;
                if (!string.IsNullOrEmpty(projectFile))
                {
                    TaskEnvironment.ProjectDirectory =
                        Path.GetDirectoryName(Path.GetFullPath(projectFile)) ?? string.Empty;
                }
            }

            if (InputPaths.Length == 0)
            {
                Log.LogMessage(MessageImportance.Low, "No input paths to resolve.");
                ResolvedPaths = Array.Empty<ITaskItem>();
                return true;
            }

            try
            {
                Log.LogMessage(
                    MessageImportance.Normal,
                    "Resolving {0} path(s) for project '{1}'.",
                    InputPaths.Length,
                    TaskEnvironment.ProjectDirectory);

                var results = new List<ITaskItem>(InputPaths.Length);

                foreach (var inputPath in InputPaths)
                {
                    if (string.IsNullOrWhiteSpace(inputPath))
                    {
                        Log.LogWarning("Skipping empty input path.");
                        continue;
                    }

                    var resolved = GetOrResolve(inputPath);
                    if (!ValidateResolvedPath(resolved, inputPath))
                        continue;

                    var item = new TaskItem(resolved);
                    item.SetMetadata("OriginalPath", inputPath);
                    item.SetMetadata("ResolvedExtension", Path.GetExtension(resolved));
                    item.SetMetadata("ResolvedDirectory", Path.GetDirectoryName(resolved) ?? string.Empty);

                    results.Add(item);
                }

                ResolvedPaths = results.ToArray();

                LogCacheStatistics();

                Log.LogMessage(
                    MessageImportance.Normal,
                    "Resolved {0} of {1} paths.",
                    results.Count,
                    InputPaths.Length);

                return true;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, showStackTrace: true);
                return false;
            }
        }

        /// <summary>
        /// Returns the cached absolute path for <paramref name="relativePath"/>, or resolves,
        /// caches, and returns it.
        /// </summary>
        private string GetOrResolve(string relativePath)
        {
            var cacheKey = (TaskEnvironment.ProjectDirectory, relativePath);

            lock (_cacheLock)
            {
                if (_pathCache.TryGetValue(cacheKey, out var cached))
                {
                    Interlocked.Increment(ref _cacheHits);
                    Log.LogMessage(MessageImportance.Low, "  Cache hit: '{0}' -> '{1}'", relativePath, cached);
                    return cached;
                }
            }

            // Resolve the path relative to the project directory.
            var absolutePath = TaskEnvironment.GetCanonicalForm(relativePath);

            lock (_cacheLock)
            {
                // Double-check after acquiring the lock.
                if (!_pathCache.ContainsKey(cacheKey))
                {
                    _pathCache[cacheKey] = absolutePath;
                    Interlocked.Increment(ref _cacheMisses);
                    Log.LogMessage(MessageImportance.Low, "  Cached:    '{0}' -> '{1}'", relativePath, absolutePath);
                }
                else
                {
                    // Another task already cached it â€” use the existing value.
                    absolutePath = _pathCache[cacheKey];
                    Interlocked.Increment(ref _cacheHits);
                    Log.LogMessage(MessageImportance.Low, "  Cache resolved: '{0}' -> '{1}'", relativePath, absolutePath);
                }
            }

            return absolutePath;
        }

        private bool ValidateResolvedPath(string resolvedPath, string originalPath)
        {
            try
            {
                // Basic validation: ensure the path is well-formed.
                _ = Path.GetFileName(resolvedPath);

                if (resolvedPath.Length > 260)
                {
                    Log.LogWarning("Resolved path exceeds MAX_PATH for '{0}'.", originalPath);
                    return false;
                }

                return true;
            }
            catch (ArgumentException ex)
            {
                Log.LogWarning("Invalid resolved path for '{0}': {1}", originalPath, ex.Message);
                return false;
            }
        }

        private void LogCacheStatistics()
        {
            var hits = Interlocked.CompareExchange(ref _cacheHits, 0, 0);
            var misses = Interlocked.CompareExchange(ref _cacheMisses, 0, 0);
            var total = hits + misses;

            if (total > 0)
            {
                var hitRate = (double)hits / total * 100.0;
                Log.LogMessage(
                    MessageImportance.Low,
                    "Path cache: {0} hits, {1} misses ({2:F1}% hit rate), {3} entries.",
                    hits,
                    misses,
                    hitRate,
                    _pathCache.Count);
            }
        }
    }
}
