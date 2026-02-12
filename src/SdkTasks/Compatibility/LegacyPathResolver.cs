// LegacyPathResolver - Resolves file paths and reads environment configuration
using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Compatibility
{
    [MSBuildMultiThreadableTask]
    public class LegacyPathResolver : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = new();

        [Required]
        public string InputPath { get; set; } = string.Empty;

        [Required]
        public string EnvVarName { get; set; } = string.Empty;

        public override bool Execute()
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

            string resolvedPath = TaskEnvironment.GetAbsolutePath(InputPath);

            string? envValue = TaskEnvironment.GetEnvironmentVariable(EnvVarName);

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
