// IncrementalBuildTracker - Tracks build execution count and last processed file
using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Build
{
    [MSBuildMultiThreadableTask]
    public class IncrementalBuildTracker : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = null!;

        private int _executionCount = 0;
        private string? _lastProcessedFile = null;

        [Required]
        public string InputFile { get; set; } = string.Empty;

        [Output]
        public int ExecutionNumber { get; set; }

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

            var resolvedPath = TaskEnvironment.GetAbsolutePath(InputFile);

            int currentCount = Interlocked.Increment(ref _executionCount);
            ExecutionNumber = currentCount;
            Interlocked.Exchange(ref _lastProcessedFile, resolvedPath);

            Log.LogMessage(MessageImportance.Normal,
                $"Execution #{currentCount}: processing '{resolvedPath}'");

            if (File.Exists(resolvedPath))
            {
                var size = new FileInfo(resolvedPath).Length;
                string lastProcessed = Interlocked.CompareExchange(ref _lastProcessedFile, null!, null!);
                Log.LogMessage(MessageImportance.Low,
                    $"File size: {size} bytes (last processed: {lastProcessed})");
            }

            return true;
        }
    }
}
