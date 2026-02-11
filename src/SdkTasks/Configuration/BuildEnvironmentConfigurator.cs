// BuildEnvironmentConfigurator - Sets environment variables for the build process
using System;
using Microsoft.Build.Framework;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace SdkTasks.Configuration
{
    [MSBuildMultiThreadableTask]
    public class BuildEnvironmentConfigurator : MSBuildTask, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = null!;

        public string? VariableName { get; set; }

        public string? VariableValue { get; set; }

        public override bool Execute()
        {
            Environment.SetEnvironmentVariable(VariableName!, VariableValue);
            return true;
        }
    }
}
