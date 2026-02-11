// UserInputPrompt - Prompts for user input during the build process
using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Diagnostics
{
    [MSBuildMultiThreadableTask]
    public class UserInputPrompt : Microsoft.Build.Utilities.Task
    {
        [Output]
        public string? UserInput { get; set; }

        public override bool Execute()
        {
            UserInput = Console.ReadLine();
            return true;
        }
    }
}
