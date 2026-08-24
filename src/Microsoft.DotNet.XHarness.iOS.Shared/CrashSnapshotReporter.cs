// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;

namespace Microsoft.DotNet.XHarness.iOS.Shared;

public interface ICrashSnapshotReporter
{
    AppleCrashReportDiagnostics CaptureDiagnostics { get; }

    Task EndCaptureAsync(TimeSpan timeout);
    Task StartCaptureAsync();
}

public class CrashSnapshotReporter : ICrashSnapshotReporter
{
    private readonly IMlaunchProcessManager _processManager;
    private readonly ILog _log;
    private readonly ILogs _logs;
    private readonly bool _isDevice;
    private readonly string _deviceName;
    private readonly AppBundleInformation _appInformation;
    private readonly Func<string> _tempFileProvider;
    private readonly string _symbolicateCrashPath;
    private HashSet<string> _initialCrashes;
    private int _matchedReportScore;

    public AppleCrashReportDiagnostics CaptureDiagnostics { get; } = new();

    public CrashSnapshotReporter(IMlaunchProcessManager processManager,
        ILog log,
        ILogs logs,
        bool isDevice,
        string deviceName,
        AppBundleInformation appInformation,
        Func<string> tempFileProvider = null)
    {
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        _isDevice = isDevice;
        _deviceName = deviceName;
        _appInformation = appInformation ?? throw new ArgumentNullException(nameof(appInformation));
        _tempFileProvider = tempFileProvider ?? Path.GetTempFileName;

        _symbolicateCrashPath = Path.Combine(processManager.XcodeRoot, "Contents", "SharedFrameworks", "DTDeviceKitBase.framework", "Versions", "A", "Resources", "symbolicatecrash");
        if (!File.Exists(_symbolicateCrashPath))
        {
            _symbolicateCrashPath = Path.Combine(processManager.XcodeRoot, "Contents", "SharedFrameworks", "DVTFoundation.framework", "Versions", "A", "Resources", "symbolicatecrash");
        }

        if (!File.Exists(_symbolicateCrashPath))
        {
            _symbolicateCrashPath = null;
        }
    }

    public async Task StartCaptureAsync()
    {
        _initialCrashes = await CreateCrashReportsSnapshotAsync();
        CaptureDiagnostics.ReportsBeforeLaunch = _initialCrashes.Count;
        _log.WriteLine("Crash reports before launch: {0}", _initialCrashes.Count);
    }

    public async Task EndCaptureAsync(TimeSpan timeout)
    {
        if (_initialCrashes == null)
        {
            throw new InvalidOperationException("CrashSnapshotReport capturing was ended without being started first!");
        }

        WriteInventory("crash-reports-before.txt", "Crash reports before launch", _initialCrashes);
        _log.WriteLine($"Checking for crash reports created during this run, waiting up to {(int)timeout.TotalSeconds} seconds...");

        // Check for crash reports
        var stopwatch = Stopwatch.StartNew();

        do
        {
            var allCrashFiles = await CreateCrashReportsSnapshotAsync();
            var newCrashFiles = new HashSet<string>(allCrashFiles);
            newCrashFiles.ExceptWith(_initialCrashes);
            CaptureDiagnostics.ReportsCreatedDuringRun = newCrashFiles.Count;

            if (newCrashFiles.Count == 0)
            {
                if (stopwatch.Elapsed.TotalSeconds > timeout.TotalSeconds)
                {
                    WriteInventory("crash-reports-after.txt", "Crash reports after launch", allCrashFiles);
                    break;
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }

                continue;
            }

            WriteInventory("crash-reports-after.txt", "Crash reports after launch", allCrashFiles);
            _log.WriteLine("Found {0} new crash report(s)", newCrashFiles.Count);

            if (!_isDevice)
            {
                foreach (var path in newCrashFiles.OrderBy(path => path, StringComparer.Ordinal))
                {
                    // It can happen that the crash log is still being written to so we have to retry
                    int retry = 1;
                    while (true)
                    {
                        try
                        {
                            var fileName = Path.GetFileName(path);
                            _log.WriteLine($"  - Adding {path}");
                            var report = _logs.AddFile(path, $"Crash report: {fileName}");
                            MatchCrashReport(fileName, report.FullPath);
                            _log.WriteLine($"    Successfully copied {fileName}");
                            break;
                        }
                        catch (Exception e)
                        {
                            _log.WriteLine($"    Attempt {retry} to copy a crash report failed: {e.Message}");
                        }

                        if (retry == 3)
                        {
                            _log.WriteLine($"    Failed to copy a crash report after {retry} retries");
                            break;
                        }

                        ++retry;
                        await Task.Delay(TimeSpan.FromSeconds(2 * retry));
                    }
                }
            }
            else
            {
                foreach (var crash in newCrashFiles.OrderBy(path => path, StringComparer.Ordinal))
                {
                    await ProcessCrash(crash);
                }
            }

            break;

        } while (true);
    }

    private async Task ProcessCrash(string crashFile)
    {
        var name = Path.GetFileName(crashFile);
        var crashReportFile = _logs.Create(name, $"Crash report: {name}", timestamp: false);
        var args = new MlaunchArguments(
            new DownloadCrashReportArgument(crashFile),
            new DownloadCrashReportToArgument(crashReportFile.FullPath));

        if (!string.IsNullOrEmpty(_deviceName))
        {
            args.Add(new DeviceNameArgument(_deviceName));
        }

        var result = await _processManager.ExecuteCommandAsync(args, _log, TimeSpan.FromMinutes(1));

        if (result.Succeeded)
        {
            _log.WriteLine("Downloaded crash report {0} to {1}", crashFile, crashReportFile.FullPath);
            MatchCrashReport(name, crashReportFile.FullPath);
            var processedReport = await GetSymbolicateCrashReportAsync(crashReportFile);
            WrenchLog.WriteLine("AddFile: {0}", processedReport.FullPath);
            _log.WriteLine("    {0}", processedReport.FullPath);
        }
        else
        {
            _log.WriteLine("Could not download crash report {0}", crashFile);
        }
    }

    private void MatchCrashReport(string name, string path)
    {
        var (metadata, score) = ParseCrashReportMetadata(name, path);
        if (metadata is null || score <= _matchedReportScore)
        {
            return;
        }

        _matchedReportScore = score;
        CaptureDiagnostics.MatchedReport = metadata;
        _log.WriteLine("Matched crash report {0} to {1} ({2})", name, _appInformation.BundleIdentifier, metadata.MatchReason);
    }

    private (AppleCrashReportMetadata? Metadata, int Score) ParseCrashReportMetadata(string name, string path)
    {
        try
        {
            string content = File.ReadAllText(path);
            using var reader = new StringReader(content);
            string firstLine = reader.ReadLine();

            string bundleId = null;
            string processName = null;
            string bugType = null;
            string timestamp = null;
            int? processId = null;

            if (!string.IsNullOrWhiteSpace(firstLine) &&
                firstLine.TrimStart('\uFEFF', ' ', '\t').StartsWith("{", StringComparison.Ordinal))
            {
                using (JsonDocument header = JsonDocument.Parse(firstLine))
                {
                    JsonElement root = header.RootElement;
                    bundleId = GetString(root, "bundleID");
                    processName = GetString(root, "app_name") ?? GetString(root, "name");
                    bugType = GetString(root, "bug_type");
                    timestamp = GetString(root, "timestamp");
                }

                string body = reader.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(body))
                {
                    try
                    {
                        using JsonDocument payload = JsonDocument.Parse(body);
                        JsonElement root = payload.RootElement;
                        processName ??= GetString(root, "procName");
                        bugType ??= GetString(root, "bug_type");
                        timestamp ??= GetString(root, "captureTime");
                        processId = GetInt32(root, "pid");
                        bundleId ??= GetNestedString(root, "bundleInfo", "CFBundleIdentifier");
                    }
                    catch (JsonException e)
                    {
                        _log.WriteLine("Crash report {0} has an incomplete payload: {1}", name, e.Message);
                    }
                }
            }
            else
            {
                foreach (string line in content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
                {
                    if (line.StartsWith("Identifier:", StringComparison.Ordinal))
                    {
                        bundleId = line.Substring("Identifier:".Length).Trim();
                    }
                    else if (line.StartsWith("Process:", StringComparison.Ordinal))
                    {
                        string process = line.Substring("Process:".Length).Trim();
                        int pidStart = process.LastIndexOf('[');
                        int pidEnd = process.LastIndexOf(']');
                        processName = pidStart > 0 ? process.Substring(0, pidStart).Trim() : process;
                        if (pidStart > 0 && pidEnd > pidStart &&
                            int.TryParse(process.Substring(pidStart + 1, pidEnd - pidStart - 1), out int pid))
                        {
                            processId = pid;
                        }
                    }
                    else if (line.StartsWith("Date/Time:", StringComparison.Ordinal))
                    {
                        timestamp = line.Substring("Date/Time:".Length).Trim();
                    }
                }
            }

            string expectedProcessName = _appInformation.BundleExecutable ?? _appInformation.AppName;
            bool bundleMatches = string.Equals(bundleId, _appInformation.BundleIdentifier, StringComparison.Ordinal);
            bool processMatches =
                string.Equals(processName, expectedProcessName, StringComparison.Ordinal) ||
                string.Equals(processName, _appInformation.AppName, StringComparison.Ordinal);
            if (!bundleMatches && !processMatches)
            {
                return (null, 0);
            }

            int score;
            string matchReason;
            if (bundleMatches)
            {
                score = 2;
                matchReason = "bundle identifier";
            }
            else
            {
                score = 1;
                matchReason = "process name";
            }

            return (new AppleCrashReportMetadata
            {
                Name = name,
                BugType = bugType,
                BundleId = bundleId,
                ProcessName = processName,
                ProcessId = processId,
                Timestamp = timestamp,
                MatchReason = matchReason,
            }, score);
        }
        catch (IOException e)
        {
            _log.WriteLine("Failed to inspect crash report {0}: {1}", name, e.Message);
            return (null, 0);
        }
        catch (UnauthorizedAccessException e)
        {
            _log.WriteLine("Failed to inspect crash report {0}: {1}", name, e.Message);
            return (null, 0);
        }
        catch (JsonException e)
        {
            _log.WriteLine("Failed to inspect crash report {0}: {1}", name, e.Message);
            return (null, 0);
        }
    }

    private static string GetString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static string GetNestedString(JsonElement element, string parentName, string propertyName)
        => element.TryGetProperty(parentName, out JsonElement parent)
            ? GetString(parent, propertyName)
            : null;

    private static int? GetInt32(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out JsonElement property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out int value)
            ? value
            : null;

    private void WriteInventory(string fileName, string description, IEnumerable<string> reports)
    {
        using var inventory = _logs.Create(fileName, description, timestamp: false);
        foreach (string report in reports.OrderBy(path => path, StringComparer.Ordinal))
        {
            inventory.WriteLine(report);
        }
    }

    private async Task<IFileBackedLog> GetSymbolicateCrashReportAsync(IFileBackedLog report)
    {
        if (_symbolicateCrashPath == null)
        {
            _log.WriteLine("Can't symbolicate {0} because the symbolicatecrash script {1} does not exist", report.FullPath, _symbolicateCrashPath);
            return report;
        }

        var name = Path.GetFileName(report.FullPath);
        var symbolicated = _logs.Create(Path.ChangeExtension(name, ".symbolicated.log"), $"Symbolicated crash report: {name}", timestamp: false);
        var environment = new Dictionary<string, string?> { { "DEVELOPER_DIR", Path.Combine(_processManager.XcodeRoot, "Contents", "Developer") } };
        var result = await _processManager.ExecuteCommandAsync(_symbolicateCrashPath, new[] { report.FullPath }, symbolicated, TimeSpan.FromMinutes(1), environment);
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
            var dir = Path.Combine(Environment.GetEnvironmentVariable("HOME"), "Library", "Logs", "DiagnosticReports");
            if (Directory.Exists(dir))
            {
                crashes.UnionWith(Directory.EnumerateFiles(dir));
            }
        }
        else
        {
            var tempFile = _tempFileProvider();
            try
            {
                var args = new MlaunchArguments(new ListCrashReportsArgument(tempFile));

                if (!string.IsNullOrEmpty(_deviceName))
                {
                    args.Add(new DeviceNameArgument(_deviceName));
                }

                var result = await _processManager.ExecuteCommandAsync(args, _log, TimeSpan.FromMinutes(1));
                if (result.Succeeded)
                {
                    crashes.UnionWith(File.ReadAllLines(tempFile));
                }
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        return crashes;
    }
}
