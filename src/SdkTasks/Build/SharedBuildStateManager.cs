// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Build
{
    /// <summary>
    /// Caches resolved configuration file paths across task invocations using
    /// IBuildEngine4 registered task objects. The first invocation resolves ConfigFileName
    /// with Path.GetFullPath and stores the result. Subsequent invocations reuse the cached path.
    /// </summary>
    [MSBuildMultiThreadableTask]
    public class SharedBuildStateManager : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = new();

        private const string CacheKeyPrefix = "SharedBuildStateManager_ConfigCache";

        [Required]
        public string ConfigFileName { get; set; } = string.Empty;

        [Output]
        public string ConfigFilePath { get; set; } = string.Empty;

        [Output]
        public bool ConfigLoaded { get; set; }

        public override bool Execute()
        {
            if (string.IsNullOrWhiteSpace(ConfigFileName))
            {
                Log.LogError("ConfigFileName must be specified.");
                return false;
            }

            IBuildEngine4 engine4 = (IBuildEngine4)BuildEngine;
            string cacheKey = BuildCacheKey(ConfigFileName);

            CachedConfigState? state = engine4.GetRegisteredTaskObject(
                cacheKey, RegisteredTaskObjectLifetime.Build) as CachedConfigState;

            if (state != null && state.IsInitialized)
            {
                Log.LogMessage(MessageImportance.Low,
                    "Cache hit for '{0}' — reusing resolved path '{1}'.",
                    ConfigFileName, state.ResolvedPaths[ConfigFileName]);

                ConfigFilePath = state.ResolvedPaths[ConfigFileName];
                ConfigLoaded = ValidateCachedData(state);
                return true;
            }

            state = InitializeState(ConfigFileName);

            engine4.RegisterTaskObject(
                cacheKey, state, RegisteredTaskObjectLifetime.Build, allowEarlyCollection: false);

            ConfigFilePath = state.ResolvedPaths[ConfigFileName];
            ConfigLoaded = state.IsInitialized;

            Log.LogMessage(MessageImportance.Normal,
                "Resolved configuration file '{0}' -> '{1}'.", ConfigFileName, ConfigFilePath);

            return true;
        }

        /// <summary>
        /// Builds a cache key for the configuration file name.
        /// </summary>
        private static string BuildCacheKey(string configFileName)
        {
            return $"{CacheKeyPrefix}_{configFileName}";
        }

        /// <summary>
        /// Resolves the config file name to an absolute path and stores it in the shared state.
        /// </summary>
        private CachedConfigState InitializeState(string configFileName)
        {
            var state = new CachedConfigState();

            string resolvedPath = Path.GetFullPath(configFileName);
            state.ResolvedPaths[configFileName] = resolvedPath;

            if (File.Exists(resolvedPath))
            {
                state.IsInitialized = true;
                state.ConfigContent = File.ReadAllText(resolvedPath);
                Log.LogMessage(MessageImportance.Low,
                    "Loaded config content ({0} chars) from '{1}'.",
                    state.ConfigContent.Length, resolvedPath);
            }
            else
            {
                state.IsInitialized = false;
                Log.LogWarning("Configuration file '{0}' not found at resolved path '{1}'.",
                    configFileName, resolvedPath);
            }

            return state;
        }

        private bool ValidateCachedData(CachedConfigState state)
        {
            if (!state.IsInitialized)
                return false;

            string cachedPath = state.ResolvedPaths[ConfigFileName];
            if (!File.Exists(cachedPath))
            {
                Log.LogWarning(
                    "Cached config path '{0}' no longer exists on disk; cache may be stale.",
                    cachedPath);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Shared state cached via IBuildEngine4.RegisterTaskObject.
        /// </summary>
        internal sealed class CachedConfigState
        {
            public Dictionary<string, string> ResolvedPaths { get; } =
                new(StringComparer.OrdinalIgnoreCase);

            public bool IsInitialized { get; set; }

            public string ConfigContent { get; set; } = string.Empty;
        }
    }
}
