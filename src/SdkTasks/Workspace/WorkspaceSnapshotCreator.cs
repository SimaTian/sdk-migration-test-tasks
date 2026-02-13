using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Workspace
{
    [MSBuildMultiThreadableTask]
    public class WorkspaceSnapshotCreator : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = null!;

        [Required]
        public string WorkspacePath { get; set; } = string.Empty;

        public string SnapshotRootDirectory { get; set; } = string.Empty;

        public int MaxSnapshotCount { get; set; } = 5;

        [Required]
        public string Mode { get; set; } = "snapshot";

        public string IncludePatterns { get; set; } = "*.csproj;*.props;*.targets";

        [Output]
        public string SnapshotId { get; set; } = string.Empty;

        public override bool Execute()
        {
            try
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

                string resolvedWorkspacePath = TaskEnvironment.GetAbsolutePath(WorkspacePath);
                Log.LogMessage(MessageImportance.Normal,
                    "WorkspaceSnapshotCreator running in '{0}' mode for: {1}", Mode, resolvedWorkspacePath);

                if (!File.Exists(resolvedWorkspacePath))
                {
                    Log.LogError("Workspace file not found: {0}", resolvedWorkspacePath);
                    return false;
                }

                string snapshotRoot = ResolveSnapshotRoot();

                if (string.Equals(Mode, "snapshot", StringComparison.OrdinalIgnoreCase))
                {
                    return ExecuteSnapshot(resolvedWorkspacePath, snapshotRoot);
                }
                else if (string.Equals(Mode, "rollback", StringComparison.OrdinalIgnoreCase))
                {
                    return ExecuteRollback(resolvedWorkspacePath, snapshotRoot);
                }
                else
                {
                    Log.LogError("Unknown mode '{0}'. Expected 'snapshot' or 'rollback'.", Mode);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, showStackTrace: true);
                return false;
            }
        }

        private string ResolveSnapshotRoot()
        {
            if (!string.IsNullOrEmpty(SnapshotRootDirectory))
            {
                return TaskEnvironment.GetAbsolutePath(SnapshotRootDirectory);
            }

            string localAppData = TaskEnvironment.GetEnvironmentVariable("LOCALAPPDATA")
                ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string fallbackDir = Path.Combine(localAppData, "WorkspaceSnapshots");
            Log.LogMessage(MessageImportance.Low,
                "No SnapshotRootDirectory specified, falling back to: {0}", fallbackDir);
            return fallbackDir;
        }

        private bool ExecuteSnapshot(string workspacePath, string snapshotRoot)
        {
            string workspaceDir = Path.GetDirectoryName(workspacePath) ?? TaskEnvironment.ProjectDirectory;
            string timestamp = GenerateTimestamp();
            string workspaceName = Path.GetFileNameWithoutExtension(workspacePath);
            string snapshotLabel = $"{workspaceName}_{timestamp}";

            // BUG: uses Directory.CreateDirectory (filesystem side-effect) directly
            string tempDir = Path.Combine(snapshotRoot, $".pending_{snapshotLabel}");
            Directory.CreateDirectory(tempDir);
            Log.LogMessage(MessageImportance.Low, "Created pending directory: {0}", tempDir);

            var filesToCapture = ScanWorkspaceFiles(workspaceDir);
            filesToCapture.Add(workspacePath);

            int capturedCount = 0;
            foreach (string sourceFile in filesToCapture)
            {
                string relativePath = ComputeRelativePath(workspaceDir, sourceFile);
                string destFile = Path.Combine(tempDir, relativePath);

                string destDir = Path.GetDirectoryName(destFile) ?? tempDir;
                // BUG: uses Directory.CreateDirectory (filesystem side-effect) directly
                Directory.CreateDirectory(destDir);

                // BUG: uses File.Copy (filesystem side-effect) directly in a loop
                File.Copy(sourceFile, destFile, overwrite: true);
                capturedCount++;

                Log.LogMessage(MessageImportance.Low, "Captured: {0}", relativePath);
            }

            // BUG: uses Directory.Move (filesystem side-effect) for atomic commit
            string finalDir = Path.Combine(snapshotRoot, snapshotLabel);
            Directory.Move(tempDir, finalDir);
            Log.LogMessage(MessageImportance.Normal,
                "Snapshot committed: {0} ({1} files)", finalDir, capturedCount);

            SnapshotId = snapshotLabel;

            PruneOldSnapshots(snapshotRoot, workspaceName);

            return true;
        }

        private bool ExecuteRollback(string workspacePath, string snapshotRoot)
        {
            string workspaceName = Path.GetFileNameWithoutExtension(workspacePath);
            string workspaceDir = Path.GetDirectoryName(workspacePath) ?? TaskEnvironment.ProjectDirectory;

            string? latestSnapshot = FindLatestSnapshot(snapshotRoot, workspaceName);
            if (latestSnapshot == null)
            {
                Log.LogError("No snapshots found for workspace '{0}' in: {1}", workspaceName, snapshotRoot);
                return false;
            }

            Log.LogMessage(MessageImportance.Normal,
                "Rolling back from snapshot: {0}", latestSnapshot);

            // BUG: uses Directory.EnumerateFiles (filesystem enumeration) directly
            var capturedFiles = Directory.EnumerateFiles(latestSnapshot, "*.*", SearchOption.AllDirectories);
            int restoredCount = 0;

            foreach (string capturedFile in capturedFiles)
            {
                string relativePath = ComputeRelativePath(latestSnapshot, capturedFile);
                string rollbackDest = Path.Combine(workspaceDir, relativePath);

                string rollbackDir = Path.GetDirectoryName(rollbackDest) ?? workspaceDir;
                Directory.CreateDirectory(rollbackDir);

                File.Copy(capturedFile, rollbackDest, overwrite: true);
                restoredCount++;

                Log.LogMessage(MessageImportance.Low, "Rolled back: {0}", relativePath);
            }

            SnapshotId = Path.GetFileName(latestSnapshot);
            Log.LogMessage(MessageImportance.Normal,
                "Rollback complete. {0} files restored from: {1}", restoredCount, SnapshotId);

            return true;
        }

        private List<string> ScanWorkspaceFiles(string workspaceDir)
        {
            var files = new List<string>();
            string[] patterns = IncludePatterns.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string pattern in patterns)
            {
                string trimmedPattern = pattern.Trim();
                Log.LogMessage(MessageImportance.Low,
                    "Scanning for pattern '{0}' in: {1}", trimmedPattern, workspaceDir);

                // BUG: uses Directory.EnumerateFiles (filesystem enumeration) directly
                var matched = Directory.EnumerateFiles(workspaceDir, trimmedPattern, SearchOption.AllDirectories);

                foreach (string file in matched)
                {
                    string canonicalPath = TaskEnvironment.GetCanonicalForm(file);
                    if (!files.Contains(canonicalPath, StringComparer.OrdinalIgnoreCase))
                    {
                        files.Add(canonicalPath);
                    }
                }
            }

            Log.LogMessage(MessageImportance.Normal,
                "Found {0} files matching patterns: {1}", files.Count, IncludePatterns);
            return files;
        }

        private string? FindLatestSnapshot(string snapshotRoot, string workspaceName)
        {
            if (!Directory.Exists(snapshotRoot))
                return null;

            // BUG: uses Directory.GetDirectories (filesystem enumeration) directly
            string[] allSnapshots = Directory.GetDirectories(snapshotRoot, $"{workspaceName}_*");

            if (allSnapshots.Length == 0)
                return null;

            return allSnapshots
                .OrderByDescending(d => Directory.GetCreationTime(d))
                .First();
        }

        private void PruneOldSnapshots(string snapshotRoot, string workspaceName)
        {
            if (MaxSnapshotCount <= 0)
                return;

            // BUG: uses Directory.GetDirectories (filesystem enumeration) directly
            string[] allSnapshots = Directory.GetDirectories(snapshotRoot, $"{workspaceName}_*");

            if (allSnapshots.Length <= MaxSnapshotCount)
            {
                Log.LogMessage(MessageImportance.Low,
                    "Snapshot count ({0}) within limit ({1}), no pruning needed.",
                    allSnapshots.Length, MaxSnapshotCount);
                return;
            }

            var sorted = allSnapshots
                .OrderByDescending(d => Directory.GetCreationTime(d))
                .ToList();

            var toRemove = sorted.Skip(MaxSnapshotCount).ToList();

            foreach (string oldSnapshot in toRemove)
            {
                Log.LogMessage(MessageImportance.Normal,
                    "Removing old snapshot: {0}", oldSnapshot);
                // BUG: uses Directory.Delete (filesystem side-effect) directly
                Directory.Delete(oldSnapshot, recursive: true);
            }

            Log.LogMessage(MessageImportance.Normal,
                "Pruned {0} old snapshots. {1} remaining.", toRemove.Count, MaxSnapshotCount);
        }

        private string GenerateTimestamp()
        {
            // BUG: uses DateTime.Now (process-global clock) instead of TaskEnvironment
            return DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        }

        private static string ComputeRelativePath(string basePath, string fullPath)
        {
            if (!basePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                basePath += Path.DirectorySeparatorChar;

            Uri baseUri = new Uri(basePath);
            Uri fullUri = new Uri(fullPath);
            Uri relativeUri = baseUri.MakeRelativeUri(fullUri);

            return Uri.UnescapeDataString(relativeUri.ToString())
                .Replace('/', Path.DirectorySeparatorChar);
        }
    }
}
