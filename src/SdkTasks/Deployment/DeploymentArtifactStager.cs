using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Deployment
{
    [MSBuildMultiThreadableTask]
    public class DeploymentArtifactStager : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = null!;

        [Required]
        public ITaskItem[] InputDirectories { get; set; } = Array.Empty<ITaskItem>();

        [Required]
        public string StagingDirectory { get; set; } = string.Empty;

        public string AllowedExtensions { get; set; } = ".dll;.pdb;.xml;.config";

        public int MaxAttempts { get; set; } = 3;

        [Required]
        public string InventoryPath { get; set; } = string.Empty;

        [Output]
        public ITaskItem[] StagedFiles { get; set; } = Array.Empty<ITaskItem>();

        [Output]
        public int TotalFilesStaged { get; set; }

        public override bool Execute()
        {
            try
            {
                Log.LogMessage(MessageImportance.Normal,
                    "Staging deployment artifacts from {0} input directories.", InputDirectories.Length);

                HashSet<string> validExtensions = ParseAllowedExtensions(AllowedExtensions);

                // BUG: uses Path.GetFullPath instead of TaskEnvironment.GetAbsolutePath
                string outputDir = Path.GetFullPath(StagingDirectory);

                // BUG: creates directory using global CWD-resolved path
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                    Log.LogMessage(MessageImportance.Low, "Created staging directory: {0}", outputDir);
                }

                var inventory = new List<InventoryRecord>();
                int stagedCount = 0;

                foreach (ITaskItem inputDir in InputDirectories)
                {
                    string resolvedInput = ResolveArtifactPath(inputDir.ItemSpec);

                    if (!Directory.Exists(resolvedInput))
                    {
                        Log.LogWarning("Input directory does not exist: {0}", resolvedInput);
                        continue;
                    }

                    string componentName = inputDir.GetMetadata("ProjectName");
                    if (string.IsNullOrEmpty(componentName))
                        componentName = Path.GetFileName(resolvedInput.TrimEnd(Path.DirectorySeparatorChar));

                    Log.LogMessage(MessageImportance.Low,
                        "Scanning {0} for deployment artifacts (component: {1})...", resolvedInput, componentName);

                    string[] candidates = Directory.GetFiles(resolvedInput, "*.*", SearchOption.TopDirectoryOnly);

                    foreach (string candidate in candidates)
                    {
                        string ext = Path.GetExtension(candidate);
                        if (!validExtensions.Contains(ext))
                            continue;

                        string artifactName = Path.GetFileName(candidate);
                        string targetPath = Path.Combine(outputDir, artifactName);

                        // BUG: uses File.Exists with CWD-relative resolved path
                        if (File.Exists(targetPath))
                        {
                            FileInfo srcInfo = new FileInfo(candidate);
                            FileInfo destInfo = new FileInfo(targetPath);

                            if (srcInfo.LastWriteTimeUtc <= destInfo.LastWriteTimeUtc
                                && srcInfo.Length == destInfo.Length)
                            {
                                Log.LogMessage(MessageImportance.Low,
                                    "Skipping (up-to-date): {0}", artifactName);
                                inventory.Add(new InventoryRecord(artifactName, targetPath, componentName, "skipped"));
                                continue;
                            }
                        }

                        bool staged = StageWithRetry(candidate, targetPath);
                        if (staged)
                        {
                            stagedCount++;
                            inventory.Add(new InventoryRecord(artifactName, targetPath, componentName, "staged"));
                            Log.LogMessage(MessageImportance.Low,
                                "Staged: {0} -> {1}", candidate, targetPath);
                        }
                        else
                        {
                            inventory.Add(new InventoryRecord(artifactName, targetPath, componentName, "failed"));
                            Log.LogWarning("Failed to stage after {0} attempts: {1}", MaxAttempts, artifactName);
                        }
                    }
                }

                TotalFilesStaged = stagedCount;
                StagedFiles = inventory
                    .Where(r => r.Status == "staged" || r.Status == "skipped")
                    .Select(r =>
                    {
                        var item = new TaskItem(r.ArtifactName);
                        item.SetMetadata("TargetPath", r.TargetPath);
                        item.SetMetadata("Component", r.Component);
                        item.SetMetadata("Status", r.Status);
                        return (ITaskItem)item;
                    })
                    .ToArray();

                WriteInventory(inventory);

                Log.LogMessage(MessageImportance.Normal,
                    "Staging complete. {0} files staged, {1} total entries in inventory.",
                    stagedCount, inventory.Count);

                return !Log.HasLoggedErrors;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, showStackTrace: true);
                return false;
            }
        }

        private string ResolveArtifactPath(string dir)
        {
            // BUG: uses Path.GetFullPath which depends on process-global CWD
            return Path.GetFullPath(dir);
        }

        private HashSet<string> ParseAllowedExtensions(string extensionList)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(extensionList))
                return result;

            foreach (string ext in extensionList.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = ext.Trim();
                if (!trimmed.StartsWith("."))
                    trimmed = "." + trimmed;
                result.Add(trimmed);
            }

            return result;
        }

        private bool StageWithRetry(string source, string destination)
        {
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    // BUG: uses File.Copy directly with paths resolved via Path.GetFullPath
                    File.Copy(source, destination, overwrite: true);
                    return true;
                }
                catch (IOException ex) when (attempt < MaxAttempts)
                {
                    Log.LogMessage(MessageImportance.Low,
                        "Stage attempt {0}/{1} failed for {2}: {3}",
                        attempt, MaxAttempts, Path.GetFileName(source), ex.Message);

                    // BUG: uses Thread.Sleep which blocks the thread in a potentially shared threadpool
                    Thread.Sleep(500 * attempt);
                }
            }

            return false;
        }

        private void WriteInventory(List<InventoryRecord> entries)
        {
            if (string.IsNullOrEmpty(InventoryPath))
                return;

            // BUG: uses Path.GetFullPath instead of TaskEnvironment.GetAbsolutePath
            string inventoryFullPath = Path.GetFullPath(InventoryPath);

            string? inventoryDir = Path.GetDirectoryName(inventoryFullPath);
            if (!string.IsNullOrEmpty(inventoryDir) && !Directory.Exists(inventoryDir))
                Directory.CreateDirectory(inventoryDir);

            var sb = new StringBuilder();
            sb.AppendLine("[");

            for (int i = 0; i < entries.Count; i++)
            {
                InventoryRecord entry = entries[i];
                sb.AppendLine("  {");
                sb.AppendFormat("    \"artifactName\": \"{0}\",", SanitizeJsonValue(entry.ArtifactName));
                sb.AppendLine();
                sb.AppendFormat("    \"targetPath\": \"{0}\",", SanitizeJsonValue(entry.TargetPath));
                sb.AppendLine();
                sb.AppendFormat("    \"component\": \"{0}\",", SanitizeJsonValue(entry.Component));
                sb.AppendLine();
                sb.AppendFormat("    \"status\": \"{0}\"", entry.Status);
                sb.AppendLine();
                sb.Append("  }");
                if (i < entries.Count - 1) sb.Append(",");
                sb.AppendLine();
            }

            sb.AppendLine("]");

            // BUG: uses File.WriteAllText with path resolved via Path.GetFullPath
            File.WriteAllText(inventoryFullPath, sb.ToString(), Encoding.UTF8);

            Log.LogMessage(MessageImportance.Low,
                "Inventory written to {0} ({1} entries).", inventoryFullPath, entries.Count);
        }

        private static string SanitizeJsonValue(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        private record InventoryRecord(string ArtifactName, string TargetPath, string Component, string Status);
    }
}
