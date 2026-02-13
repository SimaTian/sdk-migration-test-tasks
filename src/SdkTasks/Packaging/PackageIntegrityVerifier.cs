using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Packaging
{
    [MSBuildMultiThreadableTask]
    public class PackageIntegrityVerifier : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = null!;

        [Required]
        public ITaskItem[] DeclaredPackages { get; set; } = Array.Empty<ITaskItem>();

        [Required]
        public string PackageCacheDirectory { get; set; } = string.Empty;

        [Required]
        public string TargetFramework { get; set; } = string.Empty;

        [Output]
        public string VerificationReport { get; set; } = string.Empty;

        [Output]
        public ITaskItem[] UnresolvedPackages { get; set; } = Array.Empty<ITaskItem>();

        private static readonly Dictionary<string, string[]> FrameworkCompatMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "net9.0", new[] { "net9.0", "net8.0", "net7.0", "net6.0", "netstandard2.1", "netstandard2.0" } },
            { "net8.0", new[] { "net8.0", "net7.0", "net6.0", "netstandard2.1", "netstandard2.0" } },
            { "net7.0", new[] { "net7.0", "net6.0", "netstandard2.1", "netstandard2.0" } },
            { "net6.0", new[] { "net6.0", "netstandard2.1", "netstandard2.0" } },
            { "netstandard2.1", new[] { "netstandard2.1", "netstandard2.0", "netstandard1.6" } },
            { "netstandard2.0", new[] { "netstandard2.0", "netstandard1.6", "netstandard1.0" } },
        };

        public override bool Execute()
        {
            try
            {
                // Defensive ProjectDirectory initialization from BuildEngine
                if (string.IsNullOrEmpty(TaskEnvironment.ProjectDirectory) && BuildEngine != null)
                {
                    string projectFile = BuildEngine.ProjectFileOfTaskNode;
                    if (!string.IsNullOrEmpty(projectFile))
                    {
                        TaskEnvironment.ProjectDirectory =
                            Path.GetDirectoryName(Path.GetFullPath(projectFile)) ?? string.Empty;
                    }
                }

                string resolvedCacheDir;
                if (string.IsNullOrEmpty(PackageCacheDirectory))
                {
                    resolvedCacheDir = ResolveDefaultCacheDirectory();
                }
                else
                {
                    resolvedCacheDir = TaskEnvironment.GetAbsolutePath(PackageCacheDirectory);
                    if (!Directory.Exists(resolvedCacheDir))
                    {
                        resolvedCacheDir = ResolveDefaultCacheDirectory();
                    }
                }

                Log.LogMessage(MessageImportance.Normal,
                    "Verifying {0} package declarations in: {1}", DeclaredPackages.Length, resolvedCacheDir);

                var reportLines = new List<string>();
                var unresolvedItems = new List<ITaskItem>();
                int verifiedCount = 0;

                reportLines.Add($"Package Integrity Verification Report â€” {DateTime.UtcNow:u}");
                reportLines.Add($"Target Framework: {TargetFramework}");
                reportLines.Add($"Package Cache: {resolvedCacheDir}");
                reportLines.Add(new string('-', 72));

                foreach (ITaskItem pkgRef in DeclaredPackages)
                {
                    string packageId = pkgRef.ItemSpec;
                    string version = pkgRef.GetMetadata("Version") ?? "0.0.0";

                    var result = VerifySinglePackage(packageId, version, resolvedCacheDir);

                    if (result.IsResolved)
                    {
                        string assemblyInfo = result.ContainsAssembly ? "assembly located" : "no assembly (meta-package?)";
                        reportLines.Add($"  OK   {packageId}/{version} â€” {assemblyInfo}");

                        if (!string.IsNullOrEmpty(result.SpecVersion) &&
                            !VersionsAreEquivalent(version, result.SpecVersion))
                        {
                            reportLines.Add($"  WARN {packageId}: declared {version} but spec has {result.SpecVersion}");
                            Log.LogWarning("Version discrepancy for {0}: declared {1}, found {2}",
                                packageId, version, result.SpecVersion);
                        }

                        verifiedCount++;
                    }
                    else
                    {
                        reportLines.Add($"  MISS {packageId}/{version} â€” {result.Reason}");

                        var item = new TaskItem(packageId);
                        item.SetMetadata("Version", version);
                        item.SetMetadata("Reason", result.Reason ?? "Not found in cache");
                        unresolvedItems.Add(item);

                        Log.LogError("Unresolved package: {0} {1} â€” {2}", packageId, version, result.Reason);
                    }
                }

                reportLines.Add(new string('-', 72));
                reportLines.Add($"Summary: {verifiedCount} verified, {unresolvedItems.Count} unresolved out of {DeclaredPackages.Length} total.");

                UnresolvedPackages = unresolvedItems.ToArray();
                PersistVerificationReport(reportLines);

                Log.LogMessage(MessageImportance.Normal,
                    "Integrity verification complete. {0} verified, {1} unresolved.", verifiedCount, unresolvedItems.Count);

                return unresolvedItems.Count == 0;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, showStackTrace: true);
                return false;
            }
        }

        private string ResolveDefaultCacheDirectory()
        {
            string? nugetCache = TaskEnvironment.GetEnvironmentVariable("NUGET_PACKAGES");
            if (!string.IsNullOrEmpty(nugetCache))
            {
                Log.LogMessage(MessageImportance.Low, "Using NUGET_PACKAGES env: {0}", nugetCache);
                return nugetCache;
            }

            string userHome = TaskEnvironment.GetEnvironmentVariable("USERPROFILE")
                ?? TaskEnvironment.GetEnvironmentVariable("HOME")
                ?? string.Empty;
            string defaultCache = Path.Combine(userHome, ".nuget", "packages");
            Log.LogMessage(MessageImportance.Low, "Falling back to default cache: {0}", defaultCache);
            return defaultCache;
        }

        private PackageVerificationResult VerifySinglePackage(string id, string version, string cacheDir)
        {
            string packageDir = TaskEnvironment.GetAbsolutePath(
                Path.Combine(cacheDir, id.ToLowerInvariant(), version));

            if (!Directory.Exists(packageDir))
            {
                string altDir = Path.Combine(cacheDir, id, version);
                packageDir = TaskEnvironment.GetAbsolutePath(altDir);

                if (!Directory.Exists(packageDir))
                    return new PackageVerificationResult(false, false, null,
                        $"Package directory not found: {id}/{version}");
            }

            string? specVersion = ReadPackageSpecVersion(packageDir, id);

            bool containsAssembly = LocatePackageAssembly(packageDir, TargetFramework) != null;

            return new PackageVerificationResult(true, containsAssembly, specVersion, null);
        }

        private string? LocatePackageAssembly(string packageDir, string tfm)
        {
            string libDir = Path.Combine(packageDir, "lib");
            if (!Directory.Exists(libDir))
                return null;

            string[] compatFrameworks = GetCompatibleFrameworks(tfm);

            foreach (string compatTfm in compatFrameworks)
            {
                string tfmDir = Path.Combine(libDir, compatTfm);
                if (!Directory.Exists(tfmDir))
                    continue;

                foreach (string assembly in Directory.EnumerateFiles(tfmDir, "*.dll"))
                {
                    Log.LogMessage(MessageImportance.Low, "Found assembly: {0}", assembly);
                    return assembly;
                }
            }

            string refDir = Path.Combine(packageDir, "ref");
            if (Directory.Exists(refDir))
            {
                foreach (string compatTfm in compatFrameworks)
                {
                    string refTfmDir = Path.Combine(refDir, compatTfm);
                    if (!Directory.Exists(refTfmDir))
                        continue;

                    foreach (string assembly in Directory.EnumerateFiles(refTfmDir, "*.dll"))
                    {
                        Log.LogMessage(MessageImportance.Low, "Found ref assembly: {0}", assembly);
                        return assembly;
                    }
                }
            }

            return null;
        }

        private string? ReadPackageSpecVersion(string packageDir, string packageId)
        {
            try
            {
                string specPath = Path.Combine(packageDir, $"{packageId}.nuspec");
                if (!File.Exists(specPath))
                {
                    specPath = Path.Combine(packageDir, $"{packageId.ToLowerInvariant()}.nuspec");
                    if (!File.Exists(specPath))
                        return null;
                }

                string content = File.ReadAllText(specPath);
                XDocument specDoc = XDocument.Parse(content);

                XNamespace ns = specDoc.Root?.Name.Namespace ?? XNamespace.None;
                string? version = specDoc.Descendants(ns + "version").FirstOrDefault()?.Value;
                return version;
            }
            catch (Exception ex)
            {
                Log.LogMessage(MessageImportance.Low,
                    "Failed to read spec for {0}: {1}", packageId, ex.Message);
                return null;
            }
        }

        private static string[] GetCompatibleFrameworks(string tfm)
        {
            if (FrameworkCompatMap.TryGetValue(tfm, out string[]? compatList))
                return compatList;

            return new[] { tfm, "netstandard2.0" };
        }

        private static bool VersionsAreEquivalent(string declared, string actual)
        {
            if (string.Equals(declared, actual, StringComparison.OrdinalIgnoreCase))
                return true;

            if (Version.TryParse(declared, out Version? declVer) &&
                Version.TryParse(actual, out Version? actVer))
            {
                return declVer.Major == actVer.Major &&
                       declVer.Minor == actVer.Minor &&
                       declVer.Build == actVer.Build;
            }

            return false;
        }

        private void PersistVerificationReport(List<string> lines)
        {
            string reportDir = Path.Combine(
                TaskEnvironment.ProjectDirectory, "obj", TargetFramework);
            string reportPath = Path.Combine(reportDir, "package-integrity-report.txt");

            try
            {
                if (!Directory.Exists(reportDir))
                    Directory.CreateDirectory(reportDir);

                File.WriteAllText(reportPath, string.Join(Environment.NewLine, lines));
                VerificationReport = reportPath;
                Log.LogMessage(MessageImportance.Normal, "Verification report: {0}", reportPath);
            }
            catch (Exception ex)
            {
                Log.LogWarning("Could not write verification report: {0}", ex.Message);
                VerificationReport = string.Empty;
            }
        }

        private record PackageVerificationResult(
            bool IsResolved,
            bool ContainsAssembly,
            string? SpecVersion,
            string? Reason);
    }
}
