// BuildAbortHandler - Handles build abort scenarios based on exit codes
using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Build
{
    [MSBuildMultiThreadableTask]
    public class BuildAbortHandler : Microsoft.Build.Utilities.Task
    {
        public int ExitCode { get; set; }

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.Normal, "Validating build result...");

            if (ExitCode != 0)
            {
                Log.LogError($"Build failed with exit code {ExitCode}.");

                Environment.Exit(ExitCode);
            }

            Log.LogMessage(MessageImportance.Normal, "Build validation passed.");
            return true;
        }
    }
}
