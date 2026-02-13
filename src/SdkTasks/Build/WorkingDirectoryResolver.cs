// WorkingDirectoryResolver - Resolves paths relative to the current working directory
using System;
using System.IO;
using Microsoft.Build.Framework;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace SdkTasks.Build
{
    [MSBuildMultiThreadableTask]
    public class WorkingDirectoryResolver : MSBuildTask, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = null!;

        [Output]
        public string? CurrentDir { get; set; }

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

            CurrentDir = TaskEnvironment.ProjectDirectory;
            string resolvedPath = Path.Combine(CurrentDir, "output");
            Log.LogMessage(MessageImportance.Normal, "Resolved path: {0}", resolvedPath);
            return true;
        }
    }
}
