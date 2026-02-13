// CriticalErrorHandler - Handles critical build errors that require immediate termination
using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Build
{
    [MSBuildMultiThreadableTask]
    public class CriticalErrorHandler : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = new();

        public string ErrorMessage { get; set; } = string.Empty;

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.Normal, "Checking for critical errors...");

            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                Log.LogError($"Critical error detected: {ErrorMessage}");

                return false;
            }

            Log.LogMessage(MessageImportance.Normal, "No critical errors found.");
            return true;
        }
    }
}
