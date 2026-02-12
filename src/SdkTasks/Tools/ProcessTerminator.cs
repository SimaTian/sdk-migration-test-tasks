// ProcessTerminator - Terminates the current process during cleanup
using System.Diagnostics;
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
            Log.LogMessage(MessageImportance.Normal, "Performing cleanup operations...");

            var currentProcess = Process.GetCurrentProcess();
            Log.LogMessage(MessageImportance.Normal, $"Current process: {currentProcess.ProcessName} (PID: {currentProcess.Id})");

            Log.LogError("Cannot kill the current process in an MSBuild task. This operation is forbidden in multi-threaded builds.");

            return false;
        }
    }
}
