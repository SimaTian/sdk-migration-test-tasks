// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Build
{
    [MSBuildMultiThreadableTask]
    public class AssemblyCacheResolver : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = new();

        private readonly ConcurrentDictionary<string, string> _pathCache = new();

        private static readonly string[] s_probeExtensions = new[] { ".dll", ".exe" };

        [Required]
        public ITaskItem[] AssemblyReferences { get; set; } = Array.Empty<ITaskItem>();

        public string TargetDirectory { get; set; } = string.Empty;

        [Output]
        public ITaskItem[] ResolvedReferences { get; set; } = Array.Empty<ITaskItem>();

        public override bool Execute()
        {
            if (string.IsNullOrEmpty(TaskEnvironment.ProjectDirectory) && BuildEngine != null)
            {
                string projectFile = BuildEngine.ProjectFileOfTaskNode;
                if (!string.IsNullOrEmpty(projectFile))
                {
                    TaskEnvironment.ProjectDirectory =
                        Path.GetDirectoryName(Path.GetFullPath(projectFile)) ?? string.Empty;
                }
            }

            if (AssemblyReferences == null || AssemblyReferences.Length == 0)
            {
                ResolvedReferences = Array.Empty<ITaskItem>();
                return true;
            }

            string resolvedTarget = !string.IsNullOrEmpty(TargetDirectory)
                ? TaskEnvironment.GetAbsolutePath(TargetDirectory)
                : TaskEnvironment.ProjectDirectory;

            var results = new List<ITaskItem>();

            foreach (ITaskItem reference in AssemblyReferences)
            {
                string assemblyName = reference.ItemSpec;
                string? resolvedPath = ResolveAssemblyPath(assemblyName);

                if (!string.IsNullOrEmpty(resolvedPath))
                {
                    results.Add(BuildReferenceItem(assemblyName, resolvedPath));
                    Log.LogMessage(MessageImportance.Low, "Resolved '{0}' -> '{1}'", assemblyName, resolvedPath);
                }
                else
                {
                    Log.LogWarning("Could not resolve assembly reference '{0}'.", assemblyName);
                }
            }

            ResolvedReferences = results.ToArray();
            Log.LogMessage(MessageImportance.Normal,
                "Resolved {0} of {1} assembly references.", results.Count, AssemblyReferences.Length);
            return true;
        }

        private string? ResolveAssemblyPath(string assemblyName)
        {
            if (_pathCache.TryGetValue(assemblyName, out string? cachedPath))
            {
                return cachedPath;
            }

            string? computedPath = ComputeAssemblyPath(assemblyName);
            if (!string.IsNullOrEmpty(computedPath))
            {
                _pathCache.TryAdd(assemblyName, computedPath);
            }
            return computedPath;
        }

        private string? ComputeAssemblyPath(string assemblyName)
        {
            string[] probePaths = GetProbePaths();

            foreach (string probeDir in probePaths)
            {
                string? result = ProbeDirectory(probeDir, assemblyName);
                if (result != null)
                    return result;
            }

            return null;
        }

        private string[] GetProbePaths()
        {
            var paths = new List<string>
            {
                TaskEnvironment.GetAbsolutePath("bin"),
                TaskEnvironment.GetAbsolutePath(Path.Combine("obj", "refs")),
            };

            string[] additionalProbes = new[]
            {
                Path.Combine("lib", "net8.0"),
                Path.Combine("lib", "netstandard2.0"),
                "runtimes",
            };

            foreach (string probe in additionalProbes)
            {
                paths.Add(TaskEnvironment.GetAbsolutePath(probe));
            }

            return paths.ToArray();
        }

        private string? ProbeDirectory(string dir, string assemblyName)
        {
            if (!Directory.Exists(dir))
                return null;

            foreach (string ext in s_probeExtensions)
            {
                string candidate = Path.Combine(dir, assemblyName + ext);
                if (File.Exists(candidate))
                    return candidate;
            }

            // Check subdirectories one level deep
            foreach (string subDir in Directory.EnumerateDirectories(dir))
            {
                foreach (string ext in s_probeExtensions)
                {
                    string candidate = Path.Combine(subDir, assemblyName + ext);
                    if (File.Exists(candidate))
                        return candidate;
                }
            }

            return null;
        }

        private ITaskItem BuildReferenceItem(string name, string resolvedPath)
        {
            var item = new TaskItem(resolvedPath);
            item.SetMetadata("AssemblyName", name);
            item.SetMetadata("ResolvedFrom", _pathCache.ContainsKey(name) ? "Cache" : "Probe");
            item.SetMetadata("FileExtension", Path.GetExtension(resolvedPath));
            return item;
        }
    }
}
