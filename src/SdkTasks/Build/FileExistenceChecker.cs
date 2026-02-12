// FileExistenceChecker - Checks whether a specified file exists and reads its content
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Build
{
    [MSBuildMultiThreadableTask]
    public class FileExistenceChecker : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = new();

        public string FilePath { get; set; } = string.Empty;

        public override bool Execute()
        {
            if (string.IsNullOrEmpty(FilePath))
            {
                Log.LogError("FilePath is required.");
                return false;
            }

            // Auto-initialize ProjectDirectory from BuildEngine when not explicitly set
            if (string.IsNullOrEmpty(TaskEnvironment.ProjectDirectory) && BuildEngine != null)
            {
                string projectFile = BuildEngine.ProjectFileOfTaskNode;
                if (!string.IsNullOrEmpty(projectFile))
                {
                    TaskEnvironment.ProjectDirectory =
                        Path.GetDirectoryName(Path.GetFullPath(projectFile)) ?? string.Empty;
                }
            }

            var absolutePath = TaskEnvironment.GetAbsolutePath(FilePath);
            if (File.Exists(absolutePath))
            {
                string content = File.ReadAllText(absolutePath);
                Log.LogMessage(MessageImportance.Normal, $"File '{FilePath}' contains {content.Length} characters.");
            }
            else
            {
                Log.LogWarning($"File '{FilePath}' was not found.");
            }

            return true;
        }
    }
}
