// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Analysis
{
    /// <summary>
    /// Monitors a directory for file changes using a static FileSystemWatcher shared
    /// across all task instances. The watcher path is resolved with TaskEnvironment.GetAbsolutePath on first
    /// creation and reused by subsequent invocations.
    /// </summary>
    [MSBuildMultiThreadableTask]
    public class IncrementalChangeDetector : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = new();

        private FileSystemWatcher? _watcher;
        private readonly object _watcherLock = new();
        private readonly List<string> _detectedChanges = new();

        private const int DefaultCollectionTimeoutMs = 2000;

        [Required]
        public string WatchDirectory { get; set; } = string.Empty;

        public string FileFilter { get; set; } = "*.*";

        public int CollectionTimeoutMs { get; set; } = DefaultCollectionTimeoutMs;

        [Output]
        public ITaskItem[] ChangedFiles { get; set; } = Array.Empty<ITaskItem>();

        public override bool Execute()
        {
            // Defensive ProjectDirectory init from BuildEngine
            if (string.IsNullOrEmpty(TaskEnvironment.ProjectDirectory) && BuildEngine != null)
            {
                string projectFile = BuildEngine.ProjectFileOfTaskNode;
                if (!string.IsNullOrEmpty(projectFile))
                {
                    TaskEnvironment.ProjectDirectory =
                        Path.GetDirectoryName(Path.GetFullPath(projectFile)) ?? string.Empty;
                }
            }

            if (string.IsNullOrWhiteSpace(WatchDirectory))
            {
                Log.LogError("WatchDirectory must be specified.");
                return false;
            }

            InitializeWatcher();

            ITaskItem[] collected = CollectChangedFiles(CollectionTimeoutMs);
            ChangedFiles = collected;

            Log.LogMessage(MessageImportance.Normal,
                "Collected {0} changed file(s) matching '{1}'.", collected.Length, FileFilter);

            return true;
        }

        /// <summary>
        /// Creates or reuses a static FileSystemWatcher for monitoring file changes.
        /// </summary>
        private void InitializeWatcher()
        {
            lock (_watcherLock)
            {
                if (_watcher != null)
                {
                    Log.LogMessage(MessageImportance.Low,
                        "Reusing existing watcher on '{0}'.", _watcher.Path);
                    return;
                }

                string resolvedDir = TaskEnvironment.GetAbsolutePath(WatchDirectory);
                if (!Directory.Exists(resolvedDir))
                {
                    Log.LogWarning("Watch directory '{0}' does not exist. Creating it.", resolvedDir);
                    Directory.CreateDirectory(resolvedDir);
                }

                _watcher = new FileSystemWatcher(resolvedDir, FileFilter)
                {
                    NotifyFilter = NotifyFilters.FileName
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.CreationTime,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true,
                };

                _watcher.Changed += OnFileChanged;
                _watcher.Created += OnFileChanged;
                _watcher.Deleted += OnFileChanged;

                Log.LogMessage(MessageImportance.Normal,
                    "Started watching '{0}' with filter '{1}'.", resolvedDir, FileFilter);
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            lock (_watcherLock)
            {
                if (!_detectedChanges.Contains(e.FullPath))
                {
                    _detectedChanges.Add(e.FullPath);
                }
            }
        }

        /// <summary>
        /// Waits for file-change events up to the specified timeout, then returns
        /// the accumulated changed files as ITaskItem[].
        /// </summary>
        private ITaskItem[] CollectChangedFiles(int timeoutMs)
        {
            Thread.Sleep(timeoutMs);

            List<string> snapshot;
            lock (_watcherLock)
            {
                snapshot = new List<string>(_detectedChanges);
                _detectedChanges.Clear();
            }

            var items = new List<ITaskItem>();
            foreach (string filePath in snapshot)
            {
                var item = new TaskItem(filePath);
                item.SetMetadata("ChangeSource", "FileSystemWatcher");
                item.SetMetadata("ContainingDirectory", Path.GetDirectoryName(filePath) ?? string.Empty);
                item.SetMetadata("FileName", Path.GetFileName(filePath));
                items.Add(item);
            }

            return items.ToArray();
        }

        /// <summary>
        /// Disposes the static watcher. Called at build completion.
        /// </summary>
        public void DisposeWatcher()
        {
            lock (_watcherLock)
            {
                if (_watcher != null)
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Dispose();
                    _watcher = null;
                    _detectedChanges.Clear();
                }
            }
        }
    }
}
