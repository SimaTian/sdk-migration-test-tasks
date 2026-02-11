// PathCanonicalizationTask - Converts paths to their canonical form
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Build
{
    [MSBuildMultiThreadableTask]
    public class PathCanonicalizationTask : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = new();

        public string InputPath { get; set; } = string.Empty;

        public override bool Execute()
        {
            if (string.IsNullOrEmpty(InputPath))
            {
                Log.LogError("InputPath is required.");
                return false;
            }

            // Resolve relative path via TaskEnvironment
            string absolutePath = TaskEnvironment.GetAbsolutePath(InputPath);

            // Normalize the path to canonical form
            string canonicalPath = Path.GetFullPath(absolutePath);
            Log.LogMessage(MessageImportance.Normal, $"Canonical path: {canonicalPath}");

            if (File.Exists(canonicalPath))
            {
                string content = File.ReadAllText(canonicalPath);
                Log.LogMessage(MessageImportance.Normal, $"Read {content.Length} characters from '{canonicalPath}'.");
            }

            return true;
        }
    }
}
