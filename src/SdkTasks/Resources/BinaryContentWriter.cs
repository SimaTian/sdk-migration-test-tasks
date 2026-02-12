// BinaryContentWriter - Writes generated content to an output file
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Resources
{
    [MSBuildMultiThreadableTask]
    public class BinaryContentWriter : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = new();

        public string OutputPath { get; set; } = string.Empty;

        public override bool Execute()
        {
            if (string.IsNullOrEmpty(OutputPath))
            {
                Log.LogError("OutputPath is required.");
                return false;
            }

            // Auto-initialize ProjectDirectory from BuildEngine when not set
            if (string.IsNullOrEmpty(TaskEnvironment.ProjectDirectory) && BuildEngine != null)
            {
                string projectFile = BuildEngine.ProjectFileOfTaskNode;
                if (!string.IsNullOrEmpty(projectFile))
                {
                    TaskEnvironment.ProjectDirectory =
                        Path.GetDirectoryName(projectFile) ?? string.Empty;
                }
            }

            var absolutePath = TaskEnvironment.GetAbsolutePath(OutputPath);
            byte[] data = Encoding.UTF8.GetBytes("Generated output content.");

            using (var stream = new FileStream(absolutePath, FileMode.Create))
            {
                stream.Write(data, 0, data.Length);
            }

            Log.LogMessage(MessageImportance.Normal, $"Wrote {data.Length} bytes to '{OutputPath}'.");
            return true;
        }
    }
}
