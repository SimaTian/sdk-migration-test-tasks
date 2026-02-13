// EnvironmentConfigReader - Reads configuration values from environment variables
using System;
using System.IO;
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
            // Auto-initialize ProjectDirectory from BuildEngine when not set
            if (string.IsNullOrEmpty(TaskEnvironment.ProjectDirectory) && BuildEngine != null)
            {
                string projectFile = BuildEngine.ProjectFileOfTaskNode;
                if (!string.IsNullOrEmpty(projectFile))
                {
                    TaskEnvironment.ProjectDirectory =
                        Path.GetDirectoryName(Path.GetFullPath(projectFile)) ?? string.Empty;
                }
            }

            VariableValue = TaskEnvironment.GetEnvironmentVariable(VariableName!);
            return true;
        }
    }
}
