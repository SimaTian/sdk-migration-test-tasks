using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.CodeAnalysis
{
    [MSBuildMultiThreadableTask]
    public class SourceFileNormalizer : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = null!;

        [Required]
        public string RootDirectory { get; set; } = string.Empty;

        public string IncludedExtensions { get; set; } = ".cs;.xml;.json";

        public string DesiredEncoding { get; set; } = "utf-8-bom";

        public bool FixLineEndings { get; set; } = true;

        public bool GenerateBackups { get; set; } = true;

        [Output]
        public ITaskItem[] NormalizedFiles { get; set; } = Array.Empty<ITaskItem>();

        [Output]
        public string BackupLocation { get; set; } = string.Empty;

        public override bool Execute()
        {
            try
            {
                var extensions = ParseIncludedExtensions(IncludedExtensions);
                var targetEnc = MapEncoding(DesiredEncoding);
                var candidateFiles = DiscoverSourceFiles(extensions);

                if (candidateFiles.Count == 0)
                {
                    Log.LogMessage(MessageImportance.Normal, "No source files found to normalize.");
                    return true;
                }

                Log.LogMessage(MessageImportance.Normal,
                    $"Scanning {candidateFiles.Count} files for encoding inconsistencies...");

                if (GenerateBackups)
                {
                    PrepareBackupLocation();
                }

                var normalized = new List<ITaskItem>();

                foreach (var file in candidateFiles)
                {
                    var currentEnc = InspectEncoding(file);
                    bool encodingMismatch = !EncodingsAreEquivalent(currentEnc, targetEnc);
                    bool lineEndingIssue = FixLineEndings && ContainsMixedLineEndings(file);

                    if (encodingMismatch || lineEndingIssue)
                    {
                        if (GenerateBackups)
                        {
                            PreserveOriginalFile(file);
                        }

                        ApplyNormalization(file, targetEnc, lineEndingIssue);

                        var item = new TaskItem(file);
                        item.SetMetadata("PreviousEncoding", currentEnc.EncodingName);
                        item.SetMetadata("AppliedEncoding", targetEnc.EncodingName);
                        item.SetMetadata("LineEndingsFixed", lineEndingIssue.ToString());
                        normalized.Add(item);

                        Log.LogMessage(MessageImportance.Normal,
                            $"Normalized: {file} ({currentEnc.EncodingName} -> {targetEnc.EncodingName})");
                    }
                }

                NormalizedFiles = normalized.ToArray();
                Log.LogMessage(MessageImportance.High,
                    $"Normalization complete. {NormalizedFiles.Length} of {candidateFiles.Count} files updated.");

                return true;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, showStackTrace: true);
                return false;
            }
        }

        private HashSet<string> ParseIncludedExtensions(string extensions)
        {
            return new HashSet<string>(
                extensions.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                          .Select(e => e.StartsWith(".") ? e : "." + e),
                StringComparer.OrdinalIgnoreCase);
        }

        private Encoding MapEncoding(string name)
        {
            return name.ToLowerInvariant() switch
            {
                "utf-8-bom" => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
                "utf-8" => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                "utf-16" => Encoding.Unicode,
                "ascii" => Encoding.ASCII,
                _ => Encoding.GetEncoding(name),
            };
        }

        // BUG: Path.GetFullPath depends on process-wide current directory
        private List<string> DiscoverSourceFiles(HashSet<string> extensions)
        {
            var rootDir = Path.GetFullPath(RootDirectory);
            return Directory.EnumerateFiles(rootDir, "*.*", SearchOption.AllDirectories)
                            .Where(f => extensions.Contains(Path.GetExtension(f)))
                            .ToList();
        }

        // BUG: Hidden — File.ReadAllBytes with Path.GetFullPath
        private Encoding InspectEncoding(string filePath)
        {
            var resolvedPath = MakeAbsolute(filePath);
            var bytes = File.ReadAllBytes(resolvedPath);

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return Encoding.UTF8;
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode;
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode;

            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }

        private bool ContainsMixedLineEndings(string filePath)
        {
            var resolved = MakeAbsolute(filePath);
            var content = File.ReadAllText(resolved);
            bool hasCrLf = content.Contains("\r\n");
            var withoutCrLf = content.Replace("\r\n", "");
            bool hasBareLf = withoutCrLf.Contains("\n");
            bool hasBareCr = withoutCrLf.Contains("\r");
            return (hasCrLf && hasBareLf) || (hasCrLf && hasBareCr) || (hasBareLf && hasBareCr);
        }

        // BUG: Uses Path.GetTempFileName (process-wide temp dir) and File.Move
        private void ApplyNormalization(string filePath, Encoding targetEncoding, bool correctLineEndings)
        {
            var resolved = MakeAbsolute(filePath);
            var content = File.ReadAllText(resolved);

            if (correctLineEndings)
            {
                content = content.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);
            }

            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, content, targetEncoding);
            File.Delete(resolved);
            File.Move(tempFile, resolved);
        }

        // BUG: Backup directory resolved via Path.GetFullPath
        private void PrepareBackupLocation()
        {
            var rootDir = Path.GetFullPath(RootDirectory);
            BackupLocation = Path.Combine(rootDir, ".normalization-backups",
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(BackupLocation);
        }

        // BUG: File.Copy with destination resolved through Path.GetFullPath
        private void PreserveOriginalFile(string originalFile)
        {
            var resolvedOriginal = MakeAbsolute(originalFile);
            var rootDir = Path.GetFullPath(RootDirectory);
            var relativePath = Path.GetRelativePath(rootDir, resolvedOriginal);
            var preservedPath = Path.Combine(Path.GetFullPath(BackupLocation), relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(preservedPath)!);
            File.Copy(resolvedOriginal, preservedPath, overwrite: true);
        }

        // BUG: Hidden helper — Path.GetFullPath depends on process-wide current directory
        private string MakeAbsolute(string path)
        {
            return Path.GetFullPath(path);
        }

        private static bool EncodingsAreEquivalent(Encoding a, Encoding b)
        {
            return string.Equals(a.WebName, b.WebName, StringComparison.OrdinalIgnoreCase)
                && Equals(a.GetPreamble().Length > 0, b.GetPreamble().Length > 0);
        }
    }
}
