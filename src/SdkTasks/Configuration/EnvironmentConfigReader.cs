// EnvironmentConfigReader - Reads configuration values from environment variables
using System;
using Microsoft.Build.Framework;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace SdkTasks.Configuration
{
    [MSBuildMultiThreadableTask]
    public class EnvironmentConfigReader : MSBuildTask, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = null!;

        public string? VariableName { get; set; }

        [Output]
        public string? VariableValue { get; set; }

        public override bool Execute()
        {
            VariableValue = Environment.GetEnvironmentVariable(VariableName!);
            return true;
        }
    }
}
