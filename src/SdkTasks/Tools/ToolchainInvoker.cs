// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SdkTasks.Tools
{
    /// <summary>
    /// Invokes an external tool and captures its output.
    /// </summary>
    [MSBuildMultiThreadableTask]
    public class ToolchainInvoker : Microsoft.Build.Utilities.Task, IMultiThreadableTask
    {
        public TaskEnvironment TaskEnvironment { get; set; } = new();

        private const int DefaultTimeoutMs = 60_000;

        [Required]
        public string ToolName { get; set; } = string.Empty;

        public string Arguments { get; set; } = string.Empty;

        [Output]
        public string ToolOutput { get; set; } = string.Empty;

        public int TimeoutMilliseconds { get; set; } = DefaultTimeoutMs;

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

            if (string.IsNullOrWhiteSpace(ToolName))
            {
                Log.LogError("ToolName must be specified.");
                return false;
            }

            Log.LogMessage(MessageImportance.Normal,
                "Launching tool '{0}' with arguments '{1}'.", ToolName, Arguments);

            ProcessStartInfo psi = ConfigureInvocation();

            using Process? process = Process.Start(psi);
            if (process == null)
            {
                Log.LogError("Failed to start process '{0}'.", ToolName);
                return false;
            }

            string stdout = ReadProcessOutput(process);
            string stderr = process.StandardError.ReadToEnd();

            bool exited = process.WaitForExit(TimeoutMilliseconds);
            if (!exited)
            {
                Log.LogError("Process '{0}' did not exit within {1} ms.", ToolName, TimeoutMilliseconds);
                TryKillProcess(process);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                Log.LogWarning("stderr: {0}", stderr.Trim());
            }

            ToolOutput = stdout;
            return ValidateExitCode(process.ExitCode);
        }

        /// <summary>
        /// Configures the process start info for tool invocation.
        /// </summary>
        private ProcessStartInfo ConfigureInvocation()
        {
            var psi = TaskEnvironment.GetProcessStartInfo();
            psi.FileName = ToolName;
            psi.Arguments = Arguments;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;

            string? pathEnv = TaskEnvironment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                psi.Environment["PATH"] = pathEnv;
            }

            return psi;
        }

        private string ReadProcessOutput(Process process)
        {
            var sb = new StringBuilder();
            while (!process.StandardOutput.EndOfStream)
            {
                string? line = process.StandardOutput.ReadLine();
                if (line != null)
                {
                    sb.AppendLine(line);
                    Log.LogMessage(MessageImportance.Low, line);
                }
            }
            return sb.ToString().TrimEnd();
        }

        private bool ValidateExitCode(int exitCode)
        {
            if (exitCode == 0)
            {
                Log.LogMessage(MessageImportance.Normal,
                    "Tool '{0}' completed successfully.", ToolName);
                return true;
            }

            Log.LogError("Tool '{0}' exited with code {1}.", ToolName, exitCode);
            return false;
        }

        /// <summary>
        /// Best-effort kill of a child tool process on timeout.
        /// Acceptable: targets the child subprocess, not the MSBuild host.
        /// ProcessStartInfo was obtained via TaskEnvironment.GetProcessStartInfo().
        /// </summary>
        private static void TryKillProcess(Process process)
        {
            try { process.Kill(); } catch { /* best effort */ }
        }
    }
}
