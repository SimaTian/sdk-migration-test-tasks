// DependencyPathResolver - Resolves dependency paths and validates existence
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Analysis
{
    [MSBuildMultiThreadableTask]
    public class DependencyPathResolver : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = null!;

        [Required]
        public string InputPath { get; set; } = string.Empty;

        [Output]
        public string? ResolvedPath { get; set; }

        public override bool Execute()
        {
            if (string.IsNullOrEmpty(TaskEnvironment.ProjectDirectory) && BuildEngine != null)
            {
                string projectFile = BuildEngine.ProjectFileOfTaskNode;
                if (!string.IsNullOrEmpty(projectFile))
                {
                    TaskEnvironment.ProjectDirectory =
                        Path.GetDirectoryName(Path.GetFullPath(projectFile)) ?? string.Empty;
                }
            }

            ResolvedPath = ResolvePath(InputPath);

            Log.LogMessage(MessageImportance.Normal, $"Resolved '{InputPath}' to '{ResolvedPath}'");

            if (!File.Exists(ResolvedPath))
            {
                Log.LogWarning($"File not found: {ResolvedPath}");
            }

            return true;
        }

        private string ResolvePath(string path)
        {
            return TaskEnvironment.GetAbsolutePath(path);
        }
    }
}
