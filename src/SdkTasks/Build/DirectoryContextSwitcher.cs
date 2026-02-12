// DirectoryContextSwitcher - Switches the working directory context for build operations
using System;
using System.IO;
using Microsoft.Build.Framework;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace SdkTasks.Build
{
    [MSBuildMultiThreadableTask]
    public class DirectoryContextSwitcher : MSBuildTask, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = new();

        public string? NewDirectory { get; set; }

        public override bool Execute()
        {
            if (string.IsNullOrEmpty(TaskEnvironment.ProjectDirectory) && BuildEngine != null)
            {
                string projectFile = BuildEngine.ProjectFileOfTaskNode;
                if (!string.IsNullOrEmpty(projectFile))
                {
                    TaskEnvironment.ProjectDirectory =
                        Path.GetDirectoryName(TaskEnvironment.GetCanonicalForm(projectFile)) ?? string.Empty;
                }
            }

            var resolvedDirectory = TaskEnvironment.GetAbsolutePath(NewDirectory!);
            Log.LogMessage(MessageImportance.Normal, "Directory context for build operations: {0} (resolved from: {1})", resolvedDirectory, NewDirectory);
            return true;
        }
    }
}
