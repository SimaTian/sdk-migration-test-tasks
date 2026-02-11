// OutputRedirector - Redirects build output to a log file
using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Diagnostics
{
    [MSBuildMultiThreadableTask]
    public class OutputRedirector : Microsoft.Build.Utilities.Task
    {
        public string? LogFilePath { get; set; }

        public override bool Execute()
        {
            Console.SetOut(new StreamWriter(LogFilePath!));
            Console.WriteLine("Redirected output to log file.");
            return true;
        }
    }
}
