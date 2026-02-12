// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Framework;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace SdkTasks.Configuration
{
    [MSBuildMultiThreadableTask]
    public class ConfigurationValidator : MSBuildTask, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = new();

        [Required]
        public string ConfigKey { get; set; } = string.Empty;

        public string FallbackValue { get; set; } = string.Empty;

        [Output]
        public string ResolvedConfig { get; set; } = string.Empty;

        public override bool Execute()
        {
            try
            {
                if (string.IsNullOrEmpty(TaskEnvironment.ProjectDirectory) && BuildEngine != null)
                {
                    string projectFile = BuildEngine.ProjectFileOfTaskNode;
                    if (!string.IsNullOrEmpty(projectFile))
                    {
                        TaskEnvironment.ProjectDirectory =
                            Path.GetDirectoryName(Path.GetFullPath(projectFile)) ?? string.Empty;
                    }
                }

                // Read configuration from process environment.
                var configValue = TaskEnvironment.GetEnvironmentVariable(ConfigKey);

                if (!ValidateConfig(configValue))
                {
                    Log.LogMessage(
                        MessageImportance.Normal,
                        "Config key '{0}' is not set or empty; using fallback '{1}'.",
                        ConfigKey,
                        FallbackValue);
                    configValue = FallbackValue;
                }

                Log.LogMessage(
                    MessageImportance.Normal,
                    "Initial config value for '{0}': '{1}'",
                    ConfigKey,
                    configValue);

                var resolvedPath = ResolveConfigPath(configValue!);
                var configHash = ComputeChecksum(configValue!);

                Log.LogMessage(
                    MessageImportance.Low,
                    "Config path resolved to '{0}', hash: {1}",
                    resolvedPath,
                    configHash);

                // Re-read the configuration to pick up any updates during execution.
                var finalValue = TaskEnvironment.GetEnvironmentVariable(ConfigKey);

                // Apply the configuration using the current value.
                ResolvedConfig = ApplyConfiguration(finalValue ?? configValue!, resolvedPath);

                if (!string.Equals(configValue, finalValue, StringComparison.Ordinal))
                {
                    Log.LogMessage(
                        MessageImportance.Low,
                        "Config value was refreshed from '{0}' to '{1}' during execution.",
                        configValue,
                        finalValue);
                }

                Log.LogMessage(
                    MessageImportance.Normal,
                    "Resolved configuration for '{0}': '{1}'",
                    ConfigKey,
                    ResolvedConfig);

                return true;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, showStackTrace: true);
                return false;
            }
        }

        private bool ValidateConfig(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            // Reject values that contain invalid path characters as a basic sanity check.
            foreach (var ch in Path.GetInvalidPathChars())
            {
                if (value.Contains(ch))
                {
                    Log.LogWarning("Config value for '{0}' contains invalid character U+{1:X4}.", ConfigKey, (int)ch);
                    return false;
                }
            }

            return true;
        }

        private string ResolveConfigPath(string configValue)
        {
            // Build a path from the config value relative to the project directory.
            string candidate = TaskEnvironment.GetAbsolutePath(configValue);

            if (Directory.Exists(candidate))
            {
                Log.LogMessage(MessageImportance.Low, "Config directory exists: '{0}'", candidate);
                return candidate;
            }

            var parentDir = Path.GetDirectoryName(candidate);
            if (parentDir != null && Directory.Exists(parentDir))
            {
                Log.LogMessage(MessageImportance.Low, "Parent directory exists: '{0}'", parentDir);
                return candidate;
            }

            // Return the raw value when we can't resolve it to an existing location.
            return configValue;
        }

        private string ApplyConfiguration(string value, string resolvedPath)
        {
            // Combine the resolved path context with the value to produce a final config string.
            var combined = $"{resolvedPath}|{value}";
            var hash = ComputeChecksum(combined);

            return $"{value} (context={Path.GetFileName(resolvedPath)}, integrity={hash})";
        }

        private static string ComputeChecksum(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = SHA256.HashData(bytes);
            return Convert.ToHexString(hashBytes)[..12];
        }
    }
}
