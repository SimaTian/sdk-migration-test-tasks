// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Framework;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace SdkTasks.Build
{
    [MSBuildMultiThreadableTask]
    public class IntermediateFileTransformer : MSBuildTask, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = new();

        [Required]
        public string InputFile { get; set; } = string.Empty;

        [Required]
        public string TransformName { get; set; } = string.Empty;

        [Output]
        public string TransformedContent { get; set; } = string.Empty;

        public override bool Execute()
        {
            string? intermediateFile = null;

            try
            {
                // Auto-initialize ProjectDirectory from BuildEngine when not explicitly set
                if (string.IsNullOrEmpty(TaskEnvironment.ProjectDirectory) && BuildEngine != null)
                {
                    string projectFile = BuildEngine.ProjectFileOfTaskNode;
                    if (!string.IsNullOrEmpty(projectFile))
                    {
                        TaskEnvironment.ProjectDirectory =
                            Path.GetDirectoryName(Path.GetFullPath(projectFile)) ?? string.Empty;
                    }
                }

                // Use TaskEnvironment for the input path.
                var absoluteInputPath = TaskEnvironment.GetAbsolutePath(InputFile);

                if (!File.Exists(absoluteInputPath))
                {
                    Log.LogError("Input file does not exist: '{0}'", absoluteInputPath);
                    return false;
                }

                var sourceContent = File.ReadAllText(absoluteInputPath, Encoding.UTF8);

                Log.LogMessage(
                    MessageImportance.Normal,
                    "Transforming '{0}' ({1} chars) with transform '{2}'.",
                    InputFile,
                    sourceContent.Length,
                    TransformName);

                // Build a deterministic, project-isolated intermediate file path.
                intermediateFile = Path.Combine(
                    TaskEnvironment.ProjectDirectory,
                    $"msbuild_transform_{TransformName}.tmp");

                // Phase 1: Apply the transformation.
                var transformed = TransformContent(sourceContent);

                // Phase 2: Write the intermediate result for verification.
                var preWriteChecksum = ComputeChecksum(transformed);
                File.WriteAllText(intermediateFile, transformed, Encoding.UTF8);

                Log.LogMessage(
                    MessageImportance.Low,
                    "Wrote intermediate result to '{0}' (checksum: {1}).",
                    intermediateFile,
                    preWriteChecksum);

                // Phase 3: Read it back and verify integrity.
                var readBack = File.ReadAllText(intermediateFile, Encoding.UTF8);

                if (!VerifyIntegrity(readBack, preWriteChecksum))
                {
                    Log.LogWarning(
                        "Integrity check failed for transform '{0}'. " +
                        "Expected checksum {1}, file may have been modified externally.",
                        TransformName,
                        preWriteChecksum);
                }

                TransformedContent = readBack;

                Log.LogMessage(
                    MessageImportance.Normal,
                    "Transform '{0}' completed successfully ({1} chars output).",
                    TransformName,
                    TransformedContent.Length);

                return true;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, showStackTrace: true);
                return false;
            }
            finally
            {
                // Clean up the intermediate file if it exists.
                if (intermediateFile != null)
                {
                    try
                    {
                        if (File.Exists(intermediateFile))
                            File.Delete(intermediateFile);
                    }
                    catch (IOException)
                    {
                        Log.LogMessage(
                            MessageImportance.Low,
                            "Could not delete intermediate file '{0}'; it may be in use.",
                            intermediateFile);
                    }
                }
            }
        }

        private string TransformContent(string source)
        {
            // Apply a multi-step content transformation.
            var sb = new StringBuilder(source.Length + 256);

            sb.AppendLine($"<!-- Transformed by '{TransformName}' at {DateTime.UtcNow:O} -->");
            sb.AppendLine($"<!-- Source project: {TaskEnvironment.ProjectDirectory} -->");

            foreach (var line in source.Split('\n'))
            {
                var trimmed = line.TrimEnd('\r');

                // Normalize whitespace-only lines.
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    sb.AppendLine();
                    continue;
                }

                // Apply a simple token replacement transform.
                var transformed = trimmed
                    .Replace("$(ProjectDir)", TaskEnvironment.ProjectDirectory)
                    .Replace("$(TransformName)", TransformName);

                sb.AppendLine(transformed);
            }

            sb.AppendLine($"<!-- End transform '{TransformName}' -->");
            return sb.ToString();
        }

        private bool VerifyIntegrity(string content, string expectedChecksum)
        {
            var actualChecksum = ComputeChecksum(content);
            return string.Equals(actualChecksum, expectedChecksum, StringComparison.Ordinal);
        }

        private static string ComputeChecksum(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash)[..16];
        }
    }
}
