using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Cleanup
{
    [MSBuildMultiThreadableTask]
    public class BuildOutputSanitizer : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = null!;

        [Required]
        public ITaskItem[] TargetDirectories { get; set; } = Array.Empty<ITaskItem>();

        public string RetainPatterns { get; set; } = string.Empty;

        public bool ManageLockedFiles { get; set; } = true;

        public bool AggressiveClean { get; set; } = false;

        [Output]
        public int RemovedFiles { get; set; }

        [Output]
        public int RetainedFiles { get; set; }

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

            try
            {
                RemovedFiles = 0;
                RetainedFiles = 0;

                string[] retainGlobs = string.IsNullOrWhiteSpace(RetainPatterns)
                    ? Array.Empty<string>()
                    : RetainPatterns.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                Log.LogMessage(MessageImportance.Normal,
                    "BuildOutputSanitizer: sanitizing {0} directories, retaining patterns: [{1}]",
                    TargetDirectories.Length, string.Join(", ", retainGlobs));

                foreach (ITaskItem dirItem in TargetDirectories)
                {
                    string dir = dirItem.ItemSpec;

                    string resolvedDir = TaskEnvironment.GetAbsolutePath(dir);

                    if (!Directory.Exists(resolvedDir))
                    {
                        Log.LogMessage(MessageImportance.Low,
                            "Directory does not exist, skipping: {0}", resolvedDir);
                        continue;
                    }

                    SanitizeDirectory(dir, retainGlobs);
                }

                Log.LogMessage(MessageImportance.Normal,
                    "BuildOutputSanitizer complete. Removed: {0}, Retained: {1}",
                    RemovedFiles, RetainedFiles);

                return true;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, showStackTrace: true);
                return false;
            }
        }

        private void SanitizeDirectory(string dir, string[] retainGlobs)
        {
            string canonicalDir = TaskEnvironment.GetCanonicalForm(dir);

            Log.LogMessage(MessageImportance.Low, "Scanning directory: {0}", canonicalDir);

            IEnumerable<string> files = Directory.EnumerateFiles(
                canonicalDir, "*", SearchOption.AllDirectories);

            foreach (string filePath in files)
            {
                string fileName = Path.GetFileName(filePath);

                if (MatchesRetainPattern(fileName, retainGlobs))
                {
                    Log.LogMessage(MessageImportance.Low,
                        "Retaining file: {0}", filePath);
                    RetainedFiles++;
                    continue;
                }

                bool deleted = AttemptRemoval(filePath);
                if (deleted)
                {
                    RemovedFiles++;
                }
                else
                {
                    RetainedFiles++;
                }
            }

            PurgeEmptyDirectories(canonicalDir);
        }

        private bool AttemptRemoval(string filePath)
        {
            try
            {
                FileAttributes attrs = File.GetAttributes(filePath);
                if ((attrs & FileAttributes.ReadOnly) != 0)
                {
                    if (!AggressiveClean)
                    {
                        Log.LogMessage(MessageImportance.Low,
                            "Skipping readonly file: {0}", filePath);
                        return false;
                    }

                    File.SetAttributes(filePath, attrs & ~FileAttributes.ReadOnly);
                }

                File.Delete(filePath);
                return true;
            }
            catch (IOException) when (ManageLockedFiles)
            {
                Log.LogMessage(MessageImportance.Normal,
                    "File appears locked, inspecting: {0}", filePath);

                bool isLocked = InspectFileLock(filePath);
                if (isLocked && AggressiveClean)
                {
                    Log.LogMessage(MessageImportance.Normal,
                        "Attempting forced removal: {0}", filePath);
                    return ForceRemoval(filePath);
                }

                if (isLocked)
                {
                    Log.LogWarning("File is locked and AggressiveClean is disabled: {0}", filePath);
                }

                return false;
            }
            catch (UnauthorizedAccessException)
            {
                Log.LogWarning("Access denied for file: {0}", filePath);
                return false;
            }
        }

        private bool InspectFileLock(string filePath)
        {
            try
            {
                var psi = TaskEnvironment.GetProcessStartInfo();
                psi.FileName = "handle.exe";
                psi.Arguments = filePath;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;

                using var process = Process.Start(psi);
                if (process == null)
                {
                    Log.LogMessage(MessageImportance.Low,
                        "Could not start handle.exe to inspect lock on: {0}", filePath);
                    return false;
                }

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(10000);

                bool locked = output.Contains("pid:", StringComparison.OrdinalIgnoreCase);
                if (locked)
                {
                    Log.LogMessage(MessageImportance.Normal,
                        "Lock detected on {0}: {1}", filePath, output.Trim());
                }

                return locked;
            }
            catch (Exception ex)
            {
                Log.LogMessage(MessageImportance.Low,
                    "Failed to inspect file lock for {0}: {1}", filePath, ex.Message);
                return false;
            }
        }

        private bool ForceRemoval(string filePath)
        {
            try
            {
                var psi = TaskEnvironment.GetProcessStartInfo();
                psi.FileName = "cmd";
                psi.Arguments = $"/c del /f /q \"{filePath}\"";
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;

                using var process = Process.Start(psi);
                if (process == null)
                {
                    Log.LogWarning("Could not start cmd.exe for forced removal: {0}", filePath);
                    return false;
                }

                process.WaitForExit(10000);

                if (process.ExitCode == 0 && !File.Exists(filePath))
                {
                    Log.LogMessage(MessageImportance.Normal,
                        "Force removed: {0}", filePath);
                    return true;
                }

                string stderr = process.StandardError.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    Log.LogWarning("Forced removal failed for {0}: {1}", filePath, stderr.Trim());
                }

                return false;
            }
            catch (Exception ex)
            {
                Log.LogWarning("Forced removal threw for {0}: {1}", filePath, ex.Message);
                return false;
            }
        }

        private void PurgeEmptyDirectories(string rootDir)
        {
            try
            {
                foreach (string subDir in Directory.EnumerateDirectories(rootDir, "*", SearchOption.AllDirectories)
                    .OrderByDescending(d => d.Length))
                {
                    if (!Directory.EnumerateFileSystemEntries(subDir).Any())
                    {
                        Directory.Delete(subDir, false);
                        Log.LogMessage(MessageImportance.Low,
                            "Removed empty directory: {0}", subDir);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogMessage(MessageImportance.Low,
                    "Error purging empty directories under {0}: {1}", rootDir, ex.Message);
            }
        }

        private static bool MatchesRetainPattern(string fileName, string[] patterns)
        {
            foreach (string pattern in patterns)
            {
                string trimmed = pattern.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                if (trimmed.StartsWith("*.", StringComparison.Ordinal))
                {
                    string extension = trimmed.Substring(1);
                    if (fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                else if (fileName.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
