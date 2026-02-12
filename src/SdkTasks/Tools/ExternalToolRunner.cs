// ExternalToolRunner - Runs external command-line tools during the build
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Tools
{
    [MSBuildMultiThreadableTask]
    public class ExternalToolRunner : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = new();

        public string Command { get; set; } = string.Empty;

        public string Arguments { get; set; } = string.Empty;

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

            Log.LogMessage(MessageImportance.Normal, $"Running command: {Command} {Arguments}");

            var psi = TaskEnvironment.GetProcessStartInfo();
            psi.FileName = Command;
            psi.Arguments = Arguments;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;

            using var process = Process.Start(psi);
            if (process == null)
            {
                Log.LogError("Failed to start process.");
                return false;
            }

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            Log.LogMessage(MessageImportance.Normal, output);
            return process.ExitCode == 0;
        }
    }
}
