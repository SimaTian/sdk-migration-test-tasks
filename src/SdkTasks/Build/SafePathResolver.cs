// SafePathResolver - Resolves input paths with fallback logic
using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Build
{
    [MSBuildMultiThreadableTask]
    public class SafePathResolver : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = new();

        [Required]
        public string InputPath { get; set; } = string.Empty;

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

            string resolved = TaskEnvironment.GetAbsolutePath(InputPath);

            if (File.Exists(resolved))
            {
                long size = new FileInfo(resolved).Length;
                Log.LogMessage(MessageImportance.Normal,
                    $"Resolved input '{InputPath}' to '{resolved}' ({size} bytes)");
            }
            else
            {
                Log.LogMessage(MessageImportance.High,
                    $"Resolved input '{InputPath}' to '{resolved}' but file does not exist");
            }

            return true;
        }
    }
}
