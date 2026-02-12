// ProcessTerminator - Terminates the current process during cleanup
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Tools
{
    [MSBuildMultiThreadableTask]
    public class ProcessTerminator : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = new();

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

            Log.LogMessage(MessageImportance.Normal, "Performing cleanup operations...");

            var currentProcess = Process.GetCurrentProcess();
            Log.LogMessage(MessageImportance.Normal, $"Current process: {currentProcess.ProcessName} (PID: {currentProcess.Id})");

            Log.LogError("Cannot kill the current process in an MSBuild task. This operation is forbidden in multi-threaded builds.");

            return false;
        }
    }
}
