// DiagnosticLogger - Logs diagnostic messages during the build process
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Diagnostics
{
    [MSBuildMultiThreadableTask]
    public class DiagnosticLogger : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = new();

        public string? Message { get; set; }

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.Normal, Message ?? string.Empty);
            return true;
        }
    }
}
