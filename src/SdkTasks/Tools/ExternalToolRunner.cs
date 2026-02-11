// ExternalToolRunner - Runs external command-line tools during the build
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Tools
{
    public class ExternalToolRunner : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = new();

        public string Command { get; set; } = string.Empty;

        public string Arguments { get; set; } = string.Empty;

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.Normal, $"Running command: {Command} {Arguments}");

            var psi = new ProcessStartInfo(Command, Arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

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
