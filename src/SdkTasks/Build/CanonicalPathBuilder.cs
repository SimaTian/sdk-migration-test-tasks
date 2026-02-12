// CanonicalPathBuilder - Resolves and canonicalizes input paths
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Build
{
    [MSBuildMultiThreadableTask]
    public class CanonicalPathBuilder : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = new();

        [Required]
        public string InputPath { get; set; } = string.Empty;

        [Output]
        public string? CanonicalPath { get; set; }

        public override bool Execute()
        {
            // Defensive ProjectDirectory initialization
            if (string.IsNullOrEmpty(TaskEnvironment.ProjectDirectory) && BuildEngine != null)
            {
                string projectFile = BuildEngine.ProjectFileOfTaskNode;
                if (!string.IsNullOrEmpty(projectFile))
                {
                    TaskEnvironment.ProjectDirectory =
                        Path.GetDirectoryName(Path.GetFullPath(projectFile)) ?? string.Empty;
                }
            }

            // Resolve input path via TaskEnvironment (project-relative, not CWD-relative)
            var resolved = TaskEnvironment.GetAbsolutePath(InputPath);
            var canonical = TaskEnvironment.GetCanonicalForm(resolved);

            CanonicalPath = canonical;

            Log.LogMessage(MessageImportance.Normal,
                $"Input:     {InputPath}");
            Log.LogMessage(MessageImportance.Normal,
                $"Canonical: {canonical}");

            return true;
        }
    }
}
