using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Analysis
{
    [MSBuildMultiThreadableTask]
    public class DependencyTopologyAnalyzer : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = new();

        public string ScanRootDirectory { get; set; } = string.Empty;

        public string ExcludedDirectories { get; set; } = string.Empty;

        public string TopologyOutputPath { get; set; } = string.Empty;

        [Output]
        public ITaskItem[] ResolvedBuildOrder { get; set; } = Array.Empty<ITaskItem>();

        [Output]
        public bool ContainsCycles { get; set; }

        public override bool Execute()
        {
            try
            {
                if (string.IsNullOrEmpty(TaskEnvironment.ProjectDirectory) && BuildEngine != null)
                {
                    string projectFile = BuildEngine.ProjectFileOfTaskNode;
                    if (!string.IsNullOrEmpty(projectFile) && Path.IsPathRooted(projectFile))
                    {
                        TaskEnvironment.ProjectDirectory =
                            Path.GetDirectoryName(projectFile) ?? string.Empty;
                    }
                }

                string rootPath = !string.IsNullOrEmpty(ScanRootDirectory)
                    ? TaskEnvironment.GetAbsolutePath(ScanRootDirectory)
                    : TaskEnvironment.ProjectDirectory;

                Log.LogMessage(MessageImportance.Normal, "Scanning for .csproj files in: {0}", rootPath);
                var exclusions = ParseExclusions();
                var discoveredProjects = DiscoverProjectFiles(rootPath, exclusions);
                Log.LogMessage(MessageImportance.Normal, "Discovered {0} project(s).", discoveredProjects.Count);

                var adjacencyMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (string projectPath in discoveredProjects)
                    adjacencyMap[projectPath] = ExtractProjectReferences(projectPath);

                ContainsCycles = CheckForCycles(adjacencyMap);
                if (ContainsCycles)
                    Log.LogWarning("Circular references detected in the dependency topology.");

                var ordered = PerformTopologicalSort(adjacencyMap);
                ResolvedBuildOrder = CreateOutputItems(ordered, adjacencyMap);

                if (!string.IsNullOrEmpty(TopologyOutputPath))
                {
                    string outputPath = TaskEnvironment.GetAbsolutePath(TopologyOutputPath);
                    File.WriteAllText(outputPath, RenderDotNotation(adjacencyMap));
                    Log.LogMessage(MessageImportance.Normal, "DOT topology written to: {0}", outputPath);
                }

                Log.LogMessage(MessageImportance.Normal,
                    "Build order resolved: {0} project(s) in topological order.", ordered.Count);
                return true;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, showStackTrace: true);
                return false;
            }
        }

        private HashSet<string> ParseExclusions()
        {
            if (string.IsNullOrEmpty(ExcludedDirectories))
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            return new HashSet<string>(
                ExcludedDirectories.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim()),
                StringComparer.OrdinalIgnoreCase);
        }

        private List<string> DiscoverProjectFiles(string root, HashSet<string> exclusions)
        {
            var results = new List<string>();

            foreach (string file in Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories))
            {
                bool excluded = false;
                foreach (string exclusion in exclusions)
                {
                    if (file.Contains(Path.DirectorySeparatorChar + exclusion + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        excluded = true;
                        break;
                    }
                }

                if (!excluded)
                {
                    string canonical = TaskEnvironment.GetCanonicalForm(file);
                    results.Add(canonical);
                    Log.LogMessage(MessageImportance.Low, "Found project: {0}", canonical);
                }
            }

            return results;
        }

        private List<string> ExtractProjectReferences(string projectPath)
        {
            var references = new List<string>();

            string absoluteProjectPath = TaskEnvironment.GetAbsolutePath(projectPath);
            string content = File.ReadAllText(absoluteProjectPath);
            XDocument doc = XDocument.Parse(content);
            if (doc.Root == null)
                return references;

            var ns = doc.Root.Name.Namespace;
            string projectDir = Path.GetDirectoryName(absoluteProjectPath) ?? string.Empty;

            foreach (var element in doc.Descendants(ns + "ProjectReference"))
            {
                string? include = element.Attribute("Include")?.Value;
                if (string.IsNullOrEmpty(include))
                    continue;

                string resolvedRef = TaskEnvironment.GetCanonicalForm(Path.Combine(projectDir, include));
                references.Add(resolvedRef);
                Log.LogMessage(MessageImportance.Low, "  Reference: {0} -> {1}", include, resolvedRef);
            }

            return references;
        }

        private bool CheckForCycles(Dictionary<string, List<string>> graph)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var activeStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string node in graph.Keys)
            {
                if (DfsCycleDetect(node, graph, visited, activeStack))
                    return true;
            }
            return false;
        }

        private bool DfsCycleDetect(string node, Dictionary<string, List<string>> graph,
            HashSet<string> visited, HashSet<string> activeStack)
        {
            if (activeStack.Contains(node)) return true;
            if (visited.Contains(node)) return false;

            visited.Add(node);
            activeStack.Add(node);

            if (graph.TryGetValue(node, out var neighbors))
            {
                foreach (string neighbor in neighbors)
                {
                    if (DfsCycleDetect(neighbor, graph, visited, activeStack))
                        return true;
                }
            }

            activeStack.Remove(node);
            return false;
        }

        private List<string> PerformTopologicalSort(Dictionary<string, List<string>> graph)
        {
            var inDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string node in graph.Keys)
                inDegree[node] = 0;

            foreach (var kvp in graph)
            {
                foreach (string dep in kvp.Value)
                {
                    if (!inDegree.ContainsKey(dep))
                        inDegree[dep] = 0;
                    inDegree[dep]++;
                }
            }

            var queue = new Queue<string>();
            foreach (var kvp in inDegree)
            {
                if (kvp.Value == 0)
                    queue.Enqueue(kvp.Key);
            }

            var result = new List<string>();
            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                result.Add(current);

                if (graph.TryGetValue(current, out var deps))
                {
                    foreach (string dep in deps)
                    {
                        inDegree[dep]--;
                        if (inDegree[dep] == 0)
                            queue.Enqueue(dep);
                    }
                }
            }

            return result;
        }

        private string RenderDotNotation(Dictionary<string, List<string>> graph)
        {
            var sb = new StringBuilder();
            sb.AppendLine("digraph DependencyTopology {");
            sb.AppendLine("    rankdir=BT;");
            sb.AppendLine("    node [shape=box, style=filled, fillcolor=lightblue];");

            foreach (var kvp in graph)
            {
                string fromLabel = Path.GetFileNameWithoutExtension(kvp.Key);
                foreach (string dep in kvp.Value)
                {
                    string toLabel = Path.GetFileNameWithoutExtension(dep);
                    sb.AppendLine($"    \"{fromLabel}\" -> \"{toLabel}\";");
                }
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        private ITaskItem[] CreateOutputItems(List<string> sortedProjects,
            Dictionary<string, List<string>> graph)
        {
            var items = new ITaskItem[sortedProjects.Count];
            for (int i = 0; i < sortedProjects.Count; i++)
            {
                string projectPath = sortedProjects[i];
                var item = new TaskItem(projectPath);
                item.SetMetadata("ProjectName", Path.GetFileNameWithoutExtension(projectPath));
                item.SetMetadata("BuildIndex", i.ToString());
                int depCount = graph.TryGetValue(projectPath, out var deps) ? deps.Count : 0;
                item.SetMetadata("DependencyCount", depCount.ToString());
                items[i] = item;
            }
            return items;
        }
    }
}
