// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared;

public class ResultFileHandler : IResultFileHandler
{
    private IMlaunchProcessManager _processManager;
    private IFileBackedLog _mainLog;

    public ResultFileHandler(IMlaunchProcessManager pm, IFileBackedLog fs)
    {
        _processManager = pm;
        _mainLog = fs;
    }

    public bool IsVersionSupported(string osVersion, bool isSimulator)
    {
        if (isSimulator)
        {
            // Version format contains string like "Simulator 18.0".
            string[] osVersionParts = osVersion.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (osVersionParts.Length < 2)
            {
                throw new FormatException("Simulator OS version is not in the expected format.");
            }

            if (!Version.TryParse(osVersionParts[1], out Version osVersionParsed))
            {
                throw new FormatException("Simulator OS version is not in the expected format.");
            }

            if (osVersionParsed.Major >= 18)
            {
                return true;
            }
        }
        else
        {
            if (!Version.TryParse(osVersion, out Version osVersionParsed))
            {
                throw new FormatException($"Device OS version is not in the expected format.");
            }

            if (osVersionParsed.Major >= 18)
            {
                return true;
            }
        }

        return false;
    }

    public async Task<bool> CopyResultsAsync(
        RunMode runMode,
        bool isSimulator,
        string osVersion,
        string udid,
        string bundleIdentifier,
        string hostDestinationPath)
    {
        // This file path is set in iOSApplicationEntryPointBase
        string sourcePath = runMode == RunMode.iOS
            ? "/Documents/test-results.xml"
            : "/Library/Caches/Documents/test-results.xml";

        if (IsVersionSupported(osVersion, isSimulator))
        {
            string cmd;
            if (isSimulator)
            {
                cmd = $"cp \"$(xcrun simctl get_app_container {udid} {bundleIdentifier} data){sourcePath}\" \"{hostDestinationPath}\"";
            }
            else
            {
                cmd = $"xcrun devicectl device copy from --device {udid} --source {sourcePath} --destination {hostDestinationPath} --user mobile --domain-type appDataContainer --domain-identifier {bundleIdentifier}";
            }

            await _processManager.ExecuteCommandAsync(
                "/bin/bash",
                new[] { "-c", cmd },
                _mainLog,
                _mainLog,
                _mainLog,
                TimeSpan.FromMinutes(1),
                null);

            if (!File.Exists(hostDestinationPath))
            {
                _mainLog.WriteLine($"Failed to copy results file from {(isSimulator ? "simulator" : "device")}. Expected at: {hostDestinationPath}");
                return false;
            }
        }

        return true;
    }

    public async Task CopyCrashReportAsync(
        string deviceUdid,
        string? deviceName,
        AppBundleInformation appInformation,
        ILog outputLog,
        bool isSimulator)
    {
        _mainLog.WriteLine("Attempting to retrieve crash report from device...");

        // List all crash reports on the device
        string tempCrashListFile = Path.GetTempFileName();

        MlaunchArguments listArgs = new MlaunchArguments(new ListCrashReportsArgument(tempCrashListFile));

        if (!string.IsNullOrEmpty(deviceName))
        {
            listArgs.Add(new DeviceNameArgument(deviceName));
        }

        ProcessExecutionResult listResult = await _processManager.ExecuteCommandAsync(
            listArgs,
            _mainLog,
            TimeSpan.FromMinutes(1));

        if (!listResult.Succeeded || !File.Exists(tempCrashListFile))
        {
            _mainLog.WriteLine("Failed to list crash reports from device.");
            return;
        }

        List<string> crashReports = File.ReadAllLines(tempCrashListFile)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (crashReports.Count == 0)
        {
            _mainLog.WriteLine("No crash reports found on device.");
            return;
        }

        // Filter for crash reports that might be related to our app
        // .ips files typically follow the format: AppName-YYYY-MM-DD-HHMMSS.ips or similar
        List<string> appRelatedCrashes = crashReports
            .Where(crash => crash.Contains(appInformation.AppName, StringComparison.OrdinalIgnoreCase) ||
                            crash.EndsWith(".ips", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Use the last crash report (most recent) from the filtered list
        string latestCrashReport = (appRelatedCrashes.Count > 0 ? appRelatedCrashes : crashReports).Last();

        _mainLog.WriteLine($"Found crash report: {latestCrashReport}");

        // Download the crash report
        string crashReportContent = Path.GetTempFileName();

        MlaunchArguments downloadArgs = new MlaunchArguments(
            new DownloadCrashReportArgument(latestCrashReport),
            new DownloadCrashReportToArgument(crashReportContent));

        if (!string.IsNullOrEmpty(deviceName))
        {
            downloadArgs.Add(new DeviceNameArgument(deviceName));
        }

        ProcessExecutionResult downloadResult = await _processManager.ExecuteCommandAsync(
            downloadArgs,
            _mainLog,
            TimeSpan.FromMinutes(1));

        if (!downloadResult.Succeeded || !File.Exists(crashReportContent))
        {
            _mainLog.WriteLine("Failed to download crash report from device.");
            return;
        }

        // Dump the crash report content to the log
        _mainLog.WriteLine("========================================");
        _mainLog.WriteLine("Crash report:");
        string crashContent = await File.ReadAllTextAsync(crashReportContent);
        _mainLog.WriteLine(crashContent);
        _mainLog.WriteLine("========================================");

        // Also write to the output log if different from main log
        if (outputLog != _mainLog)
        {
            outputLog.WriteLine("========================================");
            outputLog.WriteLine("Crash report:");
            outputLog.WriteLine(crashContent);
            outputLog.WriteLine("========================================");
        }
    }
}
