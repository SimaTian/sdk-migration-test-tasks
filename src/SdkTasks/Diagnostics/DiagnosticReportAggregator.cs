using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Diagnostics
{
    internal record DiagnosticEntry(string SourceFile, string Severity, string Code, string Description, int LineNumber);

    [MSBuildMultiThreadableTask]
    public class DiagnosticReportAggregator : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = null!;

        [Required]
        public ITaskItem[] SourceDirectories { get; set; } = Array.Empty<ITaskItem>();

        [Required]
        public string ReportOutputPath { get; set; } = string.Empty;

        public bool IncludeWarnings { get; set; } = true;

        public string MaxEntryAge { get; set; } = string.Empty;

        [Output]
        public int ErrorCount { get; set; }

        [Output]
        public int WarningCount { get; set; }

        private static readonly Regex WarningMatcher =
            new(@": warning (CS\d+|MSB\d+|NU\d+): (.+)$", RegexOptions.Compiled);

        private static readonly Regex ErrorMatcher =
            new(@": error (CS\d+|MSB\d+|NU\d+): (.+)$", RegexOptions.Compiled);

        public override bool Execute()
        {
            try
            {
                // BUG: Path.GetFullPath uses process-global current directory
                string reportDestination = Path.GetFullPath(ReportOutputPath);
                Log.LogMessage(MessageImportance.Normal,
                    "Aggregating diagnostics. Report: {0}", reportDestination);

                TimeSpan? maxAge = ParseAgeLimit(MaxEntryAge);
                var allEntries = new List<DiagnosticEntry>();

                foreach (ITaskItem dirItem in SourceDirectories)
                {
                    string dir = dirItem.ItemSpec;
                    // BUG: Directory.GetFiles with relative path depends on current directory
                    string[] logFiles = Directory.GetFiles(dir, "*.log", SearchOption.AllDirectories);
                    Log.LogMessage(MessageImportance.Low,
                        "Found {0} log files in: {1}", logFiles.Length, dir);

                    foreach (string logFile in logFiles)
                    {
                        TimeSpan age = ComputeFileAge(logFile);
                        if (maxAge.HasValue && age > maxAge.Value)
                        {
                            Log.LogMessage(MessageImportance.Low, "Skipping aged log: {0}", logFile);
                            continue;
                        }

                        var entries = ExtractDiagnostics(logFile);
                        allEntries.AddRange(entries);
                    }
                }

                ErrorCount = allEntries.Count(e => e.Severity == "error");
                WarningCount = allEntries.Count(e => e.Severity == "warning");

                EmitReportHeader(reportDestination);
                EmitReportContent(reportDestination, allEntries);
                EmitReportFooter(reportDestination);

                Log.LogMessage(MessageImportance.Normal,
                    "Diagnostic report generated. Errors: {0}, Warnings: {1}",
                    ErrorCount, WarningCount);

                return true;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, showStackTrace: true);
                return false;
            }
        }

        private List<DiagnosticEntry> ExtractDiagnostics(string path)
        {
            var entries = new List<DiagnosticEntry>();
            // BUG: File.ReadAllLines + Path.GetFullPath uses process-global current directory
            string[] lines = File.ReadAllLines(Path.GetFullPath(path));

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                Match errorMatch = ErrorMatcher.Match(line);
                if (errorMatch.Success)
                {
                    entries.Add(new DiagnosticEntry(path, "error",
                        errorMatch.Groups[1].Value, errorMatch.Groups[2].Value, i + 1));
                    continue;
                }

                if (IncludeWarnings)
                {
                    Match warningMatch = WarningMatcher.Match(line);
                    if (warningMatch.Success)
                    {
                        entries.Add(new DiagnosticEntry(path, "warning",
                            warningMatch.Groups[1].Value, warningMatch.Groups[2].Value, i + 1));
                    }
                }
            }

            return entries;
        }

        private TimeSpan ComputeFileAge(string filePath)
        {
            // BUG: File.GetLastWriteTime with relative path depends on current directory
            DateTime lastModified = File.GetLastWriteTime(filePath);
            return DateTime.Now - lastModified;
        }

        private void EmitReportHeader(string reportPath)
        {
            // BUG: Environment.MachineName is process-global shared state
            string hostName = Environment.MachineName;
            // BUG: Environment.GetEnvironmentVariable is process-global
            string buildId = Environment.GetEnvironmentVariable("BUILD_NUMBER") ?? "local";

            string header = $@"<!DOCTYPE html>
<html>
<head><title>Diagnostic Report — {hostName}</title>
<style>
  body {{ font-family: 'Segoe UI', sans-serif; margin: 20px; }}
  table {{ border-collapse: collapse; width: 100%; }}
  th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
  th {{ background-color: #0078d4; color: white; }}
  .error {{ background-color: #fde7e9; }}
  .warning {{ background-color: #fff4ce; }}
  h1 {{ color: #333; }}
  .summary {{ margin: 15px 0; padding: 10px; background: #f0f0f0; border-radius: 4px; }}
</style></head>
<body>
<h1>Diagnostic Summary</h1>
<div class=""summary"">
  <strong>Host:</strong> {hostName} |
  <strong>Build:</strong> {buildId} |
  <strong>Generated:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
</div>";

            // BUG: File.WriteAllText with path resolved via Path.GetFullPath (process-global)
            File.WriteAllText(reportPath, header);
        }

        private void EmitReportContent(string reportPath, List<DiagnosticEntry> entries)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<table>");
            sb.AppendLine("<tr><th>Severity</th><th>Code</th><th>Description</th><th>Source</th><th>Line</th></tr>");

            var sorted = entries
                .OrderBy(e => e.Severity == "error" ? 0 : 1)
                .ThenBy(e => e.Code);

            foreach (var entry in sorted)
            {
                string cssClass = entry.Severity == "error" ? "error" : "warning";
                string fileName = Path.GetFileName(entry.SourceFile);
                sb.AppendLine($"<tr class=\"{cssClass}\">");
                sb.AppendLine($"  <td>{entry.Severity.ToUpperInvariant()}</td>");
                sb.AppendLine($"  <td>{entry.Code}</td>");
                sb.AppendLine($"  <td>{System.Net.WebUtility.HtmlEncode(entry.Description)}</td>");
                sb.AppendLine($"  <td>{System.Net.WebUtility.HtmlEncode(fileName)}</td>");
                sb.AppendLine($"  <td>{entry.LineNumber}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</table>");

            // BUG: File.AppendAllText builds report incrementally (not thread-safe)
            File.AppendAllText(reportPath, sb.ToString());
        }

        private void EmitReportFooter(string reportPath)
        {
            string footer = @"
</body>
</html>";
            // BUG: File.AppendAllText with process-global resolved path
            File.AppendAllText(reportPath, footer);
        }

        private static TimeSpan? ParseAgeLimit(string ageSpec)
        {
            if (string.IsNullOrWhiteSpace(ageSpec))
                return null;

            string trimmed = ageSpec.Trim().ToLowerInvariant();

            if (trimmed.EndsWith("h") &&
                int.TryParse(trimmed.AsSpan(0, trimmed.Length - 1), out int hours))
                return TimeSpan.FromHours(hours);

            if (trimmed.EndsWith("d") &&
                int.TryParse(trimmed.AsSpan(0, trimmed.Length - 1), out int days))
                return TimeSpan.FromDays(days);

            return null;
        }
    }
}
