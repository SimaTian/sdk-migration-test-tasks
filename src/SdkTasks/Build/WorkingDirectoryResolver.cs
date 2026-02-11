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
            CurrentDir = Environment.CurrentDirectory;
            string resolvedPath = Path.Combine(CurrentDir, "output");
            Log.LogMessage(MessageImportance.Normal, "Resolved path: {0}", resolvedPath);
            return true;
        }
    }
}
