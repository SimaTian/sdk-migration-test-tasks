// BatchItemProcessor - Converts relative paths to absolute paths in batch
using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Compilation
{
    [MSBuildMultiThreadableTask]
    public class BatchItemProcessor : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = new();

        [Required]
        public string[] RelativePaths { get; set; } = Array.Empty<string>();

        [Output]
        public string[] AbsolutePaths { get; set; } = Array.Empty<string>();

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

            AbsolutePaths = RelativePaths
                .Select(p => (string)TaskEnvironment.GetAbsolutePath(p))
                .ToArray();

            foreach (var path in AbsolutePaths)
            {
                Log.LogMessage(MessageImportance.Normal, $"Resolved path: {path}");
            }

            Log.LogMessage(MessageImportance.Normal,
                $"Resolved {AbsolutePaths.Length} paths.");

            return true;
        }
    }
}
