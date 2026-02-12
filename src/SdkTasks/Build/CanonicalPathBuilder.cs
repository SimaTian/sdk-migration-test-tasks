// CanonicalPathBuilder - Resolves and canonicalizes input paths
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Build
{
    [MSBuildMultiThreadableTask]
    public class CanonicalPathBuilder : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = null!;

        [Required]
        public string InputPath { get; set; } = string.Empty;

        [Output]
        public string? CanonicalPath { get; set; }

        public override bool Execute()
        {
            var canonical = TaskEnvironment.GetCanonicalForm(InputPath);

            CanonicalPath = canonical;

            Log.LogMessage(MessageImportance.Normal,
                $"Input:     {InputPath}");
            Log.LogMessage(MessageImportance.Normal,
                $"Canonical: {canonical}");

            return true;
        }
    }
}
