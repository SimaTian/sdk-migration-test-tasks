// ProjectConfigReader - Reads and updates XML project configuration files
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Compilation
{
    [MSBuildMultiThreadableTask]
    public class ProjectConfigReader : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = new();

        public string XmlPath { get; set; } = string.Empty;

        public override bool Execute()
        {
            if (string.IsNullOrEmpty(XmlPath))
            {
                Log.LogError("XmlPath is required.");
                return false;
            }

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

            var absolutePath = TaskEnvironment.GetAbsolutePath(XmlPath);
            XDocument doc = XDocument.Load((string)absolutePath);

            int elementCount = doc.Descendants().Count();
            Log.LogMessage(MessageImportance.Normal, $"Loaded XML with {elementCount} elements from '{XmlPath}'.");

            doc.Root?.SetAttributeValue("processed", "true");
            doc.Save((string)absolutePath);

            Log.LogMessage(MessageImportance.Normal, $"Saved updated XML back to '{XmlPath}'.");
            return true;
        }
    }
}
