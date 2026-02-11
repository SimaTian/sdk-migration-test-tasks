// LegacyPathResolver - Resolves file paths and reads environment configuration
using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Compatibility
{
    [MSBuildMultiThreadableTask]
    public class LegacyPathResolver : Microsoft.Build.Utilities.Task
    {
        [Required]
        public string InputPath { get; set; } = string.Empty;

        [Required]
        public string EnvVarName { get; set; } = string.Empty;

        public override bool Execute()
        {
            string resolvedPath = Path.GetFullPath(InputPath);

            if (!File.Exists(resolvedPath))
            {
                Log.LogMessage(MessageImportance.High, $"File not found: {resolvedPath}");
                return true;
            }

            string? envValue = Environment.GetEnvironmentVariable(EnvVarName);

            if (!string.IsNullOrEmpty(envValue))
            {
                Log.LogMessage(MessageImportance.Normal,
                    $"Processing '{resolvedPath}' with {EnvVarName}={envValue}");
            }
            else
            {
                Log.LogMessage(MessageImportance.Low,
                    $"Environment variable '{EnvVarName}' is not set; using defaults for '{resolvedPath}'");
            }

            return true;
        }
    }
}
