// DiagnosticLogger - Logs diagnostic messages during the build process
using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Diagnostics
{
    [MSBuildMultiThreadableTask]
    public class DiagnosticLogger : Microsoft.Build.Utilities.Task
    {
        public string? Message { get; set; }

        public override bool Execute()
        {
            Console.WriteLine(Message);
            return true;
        }
    }
}
