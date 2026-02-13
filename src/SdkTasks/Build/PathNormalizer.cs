// PathNormalizer - Normalizes input paths to their full form
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Build
{
    [MSBuildMultiThreadableTask]
    public class PathNormalizer : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = new();

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

            if (string.IsNullOrEmpty(InputPath))
            {
                Log.LogError("InputPath is required.");
                return false;
            }

            string resolvedPath = TaskEnvironment.GetAbsolutePath(InputPath);
            Log.LogMessage(MessageImportance.Normal, $"Resolved path: {resolvedPath}");

            if (File.Exists(resolvedPath))
            {
                Log.LogMessage(MessageImportance.Normal, $"File found at '{resolvedPath}'.");
            }
            else
            {
                Log.LogMessage(MessageImportance.Normal, $"File not found at '{resolvedPath}'.");
            }

            return true;
        }
    }
}
