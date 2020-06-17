﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution.Mlaunch;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;

namespace Microsoft.DotNet.XHarness.iOS.Shared
{
    public interface ICrashSnapshotReporter
    {
        Task EndCaptureAsync(TimeSpan timeout);
        Task StartCaptureAsync();
    }

    public class CrashSnapshotReporter : ICrashSnapshotReporter
    {
        private readonly IMLaunchProcessManager _processManager;
        private readonly ILog _log;
        private readonly ILogs _logs;
        private readonly bool _isDevice;
        private readonly string _deviceName;
        private readonly Func<string> _tempFileProvider;
        private readonly string _symbolicateCrashPath;
        private HashSet<string> _initialCrashes;

        public CrashSnapshotReporter(IMLaunchProcessManager processManager,
            ILog log,
            ILogs logs,
            bool isDevice,
            string deviceName,
            Func<string> tempFileProvider = null)
        {
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _logs = logs ?? throw new ArgumentNullException(nameof(logs));
            _isDevice = isDevice;
            _deviceName = deviceName;
            _tempFileProvider = tempFileProvider ?? Path.GetTempFileName;

            _symbolicateCrashPath = Path.Combine(processManager.XcodeRoot, "Contents", "SharedFrameworks", "DTDeviceKitBase.framework", "Versions", "A", "Resources", "symbolicatecrash");
            if (!File.Exists(_symbolicateCrashPath))
                _symbolicateCrashPath = Path.Combine(processManager.XcodeRoot, "Contents", "SharedFrameworks", "DVTFoundation.framework", "Versions", "A", "Resources", "symbolicatecrash");
            if (!File.Exists(_symbolicateCrashPath))
                _symbolicateCrashPath = null;
        }

        public async Task StartCaptureAsync() => _initialCrashes = await CreateCrashReportsSnapshotAsync();

        public async Task EndCaptureAsync(TimeSpan timeout)
        {
            // Check for crash reports
            var stopwatch = Stopwatch.StartNew();

            do
            {
                HashSet<string> newCrashFiles = await CreateCrashReportsSnapshotAsync();
                newCrashFiles.ExceptWith(_initialCrashes);

                if (newCrashFiles.Count == 0)
                {
                    if (stopwatch.Elapsed.TotalSeconds > timeout.TotalSeconds)
                    {
                        break;
                    }
                    else
                    {
                        _log.WriteLine(
                            "No crash reports, waiting a second to see if the crash report service just didn't complete in time ({0})",
                            (int)(timeout.TotalSeconds - stopwatch.Elapsed.TotalSeconds));

                        Thread.Sleep(TimeSpan.FromSeconds(1));
                    }

                    continue;
                }

                _log.WriteLine("Found {0} new crash report(s)", newCrashFiles.Count);

                IEnumerable<IFileBackedLog> crashReports;
                if (!_isDevice)
                {
                    crashReports = new List<IFileBackedLog>(newCrashFiles.Count);
                    foreach (string path in newCrashFiles)
                    {
                        _logs.AddFile(path, $"Crash report: {Path.GetFileName(path)}");
                    }
                }
                else
                {
                    // Download crash reports from the device. We put them in the project directory so that they're automatically deleted on wrench
                    // (if we put them in /tmp, they'd never be deleted).
                    crashReports = newCrashFiles
                        .Select(async crash => await ProcessCrash(crash))
                        .Select(t => t.Result)
                        .Where(c => c != null);
                }

                foreach (IFileBackedLog cp in crashReports)
                {
                    WrenchLog.WriteLine("AddFile: {0}", cp.FullPath);
                    _log.WriteLine("    {0}", cp.FullPath);
                }

                break;

            } while (true);
        }

        private async Task<IFileBackedLog> ProcessCrash(string crashFile)
        {
            string name = Path.GetFileName(crashFile);
            IFileBackedLog crashReportFile = _logs.Create(name, $"Crash report: {name}", timestamp: false);
            var args = new MlaunchArguments(
                new DownloadCrashReportArgument(crashFile),
                new DownloadCrashReportToArgument(crashReportFile.FullPath));

            if (!string.IsNullOrEmpty(_deviceName)) args.Add(new DeviceNameArgument(_deviceName));

            Common.Execution.ProcessExecutionResult result = await _processManager.ExecuteCommandAsync(args, _log, TimeSpan.FromMinutes(1));

            if (result.Succeeded)
            {
                _log.WriteLine("Downloaded crash report {0} to {1}", crashFile, crashReportFile.FullPath);
                return await GetSymbolicateCrashReportAsync(crashReportFile);
            }
            else
            {
                _log.WriteLine("Could not download crash report {0}", crashFile);
                return null;
            }
        }

        private async Task<IFileBackedLog> GetSymbolicateCrashReportAsync(IFileBackedLog report)
        {
            if (_symbolicateCrashPath == null)
            {
                _log.WriteLine("Can't symbolicate {0} because the symbolicatecrash script {1} does not exist", report.FullPath, _symbolicateCrashPath);
                return report;
            }

            string name = Path.GetFileName(report.FullPath);
            IFileBackedLog symbolicated = _logs.Create(Path.ChangeExtension(name, ".symbolicated.log"), $"Symbolicated crash report: {name}", timestamp: false);
            var environment = new Dictionary<string, string> { { "DEVELOPER_DIR", Path.Combine(_processManager.XcodeRoot, "Contents", "Developer") } };
            Common.Execution.ProcessExecutionResult result = await _processManager.ExecuteCommandAsync(_symbolicateCrashPath, new[] { report.FullPath }, symbolicated, TimeSpan.FromMinutes(1), environment);
            if (result.Succeeded)
            {
                _log.WriteLine("Symbolicated {0} successfully.", report.FullPath);
                return symbolicated;
            }
            else
            {
                _log.WriteLine("Failed to symbolicate {0}.", report.FullPath);
                return report;
            }
        }

        private async Task<HashSet<string>> CreateCrashReportsSnapshotAsync()
        {
            var crashes = new HashSet<string>();

            if (!_isDevice)
            {
                string dir = Path.Combine(Environment.GetEnvironmentVariable("HOME"), "Library", "Logs", "DiagnosticReports");
                if (Directory.Exists(dir))
                    crashes.UnionWith(Directory.EnumerateFiles(dir));
            }
            else
            {
                string tempFile = _tempFileProvider();
                try
                {
                    var args = new MlaunchArguments(new ListCrashReportsArgument(tempFile));

                    if (!string.IsNullOrEmpty(_deviceName)) args.Add(new DeviceNameArgument(_deviceName));

                    Common.Execution.ProcessExecutionResult result = await _processManager.ExecuteCommandAsync(args, _log, TimeSpan.FromMinutes(1));
                    if (result.Succeeded)
                        crashes.UnionWith(File.ReadAllLines(tempFile));
                }
                finally
                {
                    File.Delete(tempFile);
                }
            }

            return crashes;
        }
    }
}
