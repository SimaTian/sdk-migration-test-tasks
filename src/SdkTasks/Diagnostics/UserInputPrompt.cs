// UserInputPrompt - Prompts for user input during the build process
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Diagnostics
{
    [MSBuildMultiThreadableTask]
    public class UserInputPrompt : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = new();

        [Output]
        public string? UserInput { get; set; }

        public override bool Execute()
        {
            Log.LogWarning("Interactive console input is not supported in multithreaded builds.");
            UserInput = string.Empty;
            return true;
        }
    }
}
