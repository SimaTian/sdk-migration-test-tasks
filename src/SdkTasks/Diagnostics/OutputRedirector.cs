// OutputRedirector - Redirects build output to a log file
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Diagnostics
{
    [MSBuildMultiThreadableTask]
    public class OutputRedirector : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = new();

        public string? LogFilePath { get; set; }

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

            var absolutePath = TaskEnvironment.GetAbsolutePath(LogFilePath!);
            using var writer = new StreamWriter((string)absolutePath);
            writer.WriteLine("Redirected output to log file.");
            Log.LogMessage(MessageImportance.Normal, "Redirected output to log file.");
            return true;
        }
    }
}
