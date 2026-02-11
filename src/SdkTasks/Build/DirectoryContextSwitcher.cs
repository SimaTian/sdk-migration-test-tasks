// DirectoryContextSwitcher - Switches the working directory context for build operations
using System;
using Microsoft.Build.Framework;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace SdkTasks.Build
{
    [MSBuildMultiThreadableTask]
    public class DirectoryContextSwitcher : MSBuildTask, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = null!;

        public string? NewDirectory { get; set; }

        public override bool Execute()
        {
            Environment.CurrentDirectory = NewDirectory!;
            Log.LogMessage(MessageImportance.Normal, "Changed working directory to: {0}", NewDirectory);
            return true;
        }
    }
}
