using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Versioning
{
    [MSBuildMultiThreadableTask]
    public class VersionMetadataUpdater : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = null!;

        [Required]
        public ITaskItem[] TargetFiles { get; set; } = Array.Empty<ITaskItem>();

        [Required]
        public string VersionPrefix { get; set; } = string.Empty;

        public string VersionSuffix { get; set; } = string.Empty;

        public string BuildLabel { get; set; } = string.Empty;

        public bool PreserveOriginals { get; set; } = true;

        public string PreservationDirectory { get; set; } = string.Empty;

        [Output]
        public ITaskItem[] UpdatedFiles { get; set; } = Array.Empty<ITaskItem>();

        private static readonly Regex ProjVersionTag = new(
            @"<Version>([^<]*)</Version>",
            RegexOptions.Compiled);
        private static readonly Regex ProjAssemblyVersionTag = new(
            @"<AssemblyVersion>([^<]*)</AssemblyVersion>",
            RegexOptions.Compiled);
        private static readonly Regex ProjFileVersionTag = new(
            @"<FileVersion>([^<]*)</FileVersion>",
            RegexOptions.Compiled);
        private static readonly Regex AsmInfoVersionAttr = new(
            @"\[assembly:\s*AssemblyVersion\(""([^""]*)""\)\]",
            RegexOptions.Compiled);
        private static readonly Regex AsmInfoFileVersionAttr = new(
            @"\[assembly:\s*AssemblyFileVersion\(""([^""]*)""\)\]",
            RegexOptions.Compiled);
        private static readonly Regex AsmInfoInformationalAttr = new(
            @"\[assembly:\s*AssemblyInformationalVersion\(""([^""]*)""\)\]",
            RegexOptions.Compiled);

        public override bool Execute()
        {
            try
            {
                string composedVersion = ComposeVersionString();
                string assemblyVersion = ComposeAssemblyVersion();

                Log.LogMessage(MessageImportance.High,
                    "Updating version metadata: {0} (AssemblyVersion: {1})", composedVersion, assemblyVersion);

                var updatedItems = new List<ITaskItem>();

                foreach (ITaskItem targetFile in TargetFiles)
                {
                    // CORRECT: Execute() uses TaskEnvironment for path resolution
                    string absolutePath = TaskEnvironment.GetAbsolutePath(targetFile.ItemSpec);

                    if (!File.Exists(absolutePath))
                    {
                        Log.LogWarning("Target file not found: {0}", absolutePath);
                        continue;
                    }

                    if (PreserveOriginals)
                    {
                        BackupOriginalVersion(targetFile.ItemSpec);
                    }

                    // BUG: passes raw ItemSpec to LoadFileContent which doesn't resolve it
                    string content = LoadFileContent(targetFile.ItemSpec);
                    bool isProjectFile = absolutePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);

                    string updated = isProjectFile
                        ? UpdateProjectFileContent(content, composedVersion, assemblyVersion)
                        : UpdateAssemblyInfoContent(content, composedVersion, assemblyVersion);

                    if (updated != content)
                    {
                        PersistFileContent(targetFile.ItemSpec, updated);

                        var item = new TaskItem(absolutePath);
                        item.SetMetadata("PreviousVersion", ExtractExistingVersion(content, isProjectFile));
                        item.SetMetadata("NewVersion", composedVersion);
                        item.SetMetadata("FileFormat", isProjectFile ? "csproj" : "AssemblyInfo");
                        updatedItems.Add(item);

                        Log.LogMessage(MessageImportance.Normal, "Updated: {0}", absolutePath);
                    }
                    else
                    {
                        Log.LogMessage(MessageImportance.Low,
                            "No version tokens found in: {0}", absolutePath);
                    }
                }

                UpdatedFiles = updatedItems.ToArray();
                Log.LogMessage(MessageImportance.High,
                    "Version metadata update complete. {0} of {1} files modified.",
                    updatedItems.Count, TargetFiles.Length);

                return true;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, showStackTrace: true);
                return false;
            }
        }

        // BUG: Uses Path.GetFullPath() instead of TaskEnvironment.GetAbsolutePath()
        private void BackupOriginalVersion(string filePath)
        {
            string backupDir = string.IsNullOrEmpty(PreservationDirectory)
                ? Path.GetDirectoryName(filePath) ?? string.Empty
                : PreservationDirectory;

            string fileName = Path.GetFileName(filePath);
            string backupPath = Path.Combine(backupDir, fileName + ".bak");

            File.Copy(Path.GetFullPath(filePath), Path.GetFullPath(backupPath), overwrite: true);

            Log.LogMessage(MessageImportance.Low, "Preserved original: {0}", backupPath);
        }

        // BUG: Reads file using unresolved path directly
        private string LoadFileContent(string path)
        {
            return File.ReadAllText(path);
        }

        // BUG: Uses Path.GetFullPath() instead of TaskEnvironment.GetAbsolutePath()
        private void PersistFileContent(string path, string content)
        {
            File.WriteAllText(Path.GetFullPath(path), content);
        }

        // BUG: Falls back to Environment.GetEnvironmentVariable() directly
        private string ComposeVersionString()
        {
            string version = VersionPrefix;

            if (!string.IsNullOrEmpty(VersionSuffix))
            {
                version = $"{version}-{VersionSuffix}";
            }

            string label = BuildLabel;
            if (string.IsNullOrEmpty(label))
            {
                label = Environment.GetEnvironmentVariable("BUILD_BUILDNUMBER") ?? string.Empty;
            }

            if (!string.IsNullOrEmpty(label))
            {
                version = string.IsNullOrEmpty(VersionSuffix)
                    ? $"{version}+{label}"
                    : $"{version}.{label}";
            }

            return version;
        }

        private string ComposeAssemblyVersion()
        {
            string[] parts = VersionPrefix.Split('.');
            return parts.Length >= 2
                ? $"{parts[0]}.{parts[1]}.0.0"
                : $"{VersionPrefix}.0.0.0";
        }

        private string UpdateProjectFileContent(string content, string fullVersion, string assemblyVersion)
        {
            content = ProjVersionTag.Replace(content,
                $"<Version>{fullVersion}</Version>");
            content = ProjAssemblyVersionTag.Replace(content,
                $"<AssemblyVersion>{assemblyVersion}</AssemblyVersion>");
            content = ProjFileVersionTag.Replace(content,
                $"<FileVersion>{fullVersion}</FileVersion>");
            return content;
        }

        private string UpdateAssemblyInfoContent(string content, string fullVersion, string assemblyVersion)
        {
            content = AsmInfoVersionAttr.Replace(content,
                $"[assembly: AssemblyVersion(\"{assemblyVersion}\")]");
            content = AsmInfoFileVersionAttr.Replace(content,
                $"[assembly: AssemblyFileVersion(\"{fullVersion}\")]");
            content = AsmInfoInformationalAttr.Replace(content,
                $"[assembly: AssemblyInformationalVersion(\"{fullVersion}\")]");
            return content;
        }

        private string ExtractExistingVersion(string content, bool isProjectFile)
        {
            Regex regex = isProjectFile ? ProjVersionTag : AsmInfoVersionAttr;
            Match match = regex.Match(content);
            return match.Success ? match.Groups[1].Value : "unknown";
        }
    }
}
