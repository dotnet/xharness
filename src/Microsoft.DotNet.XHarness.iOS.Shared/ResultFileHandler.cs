// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared;

public class ResultFileHandler : IResultFileHandler
{
    private readonly IMlaunchProcessManager _processManager;
    private readonly IFileBackedLog _mainLog;

    public ResultFileHandler(IMlaunchProcessManager processManager, IFileBackedLog mainLog)
    {
        _processManager = processManager;
        _mainLog = mainLog;
    }

    public bool IsSimulatorVersionSupported(string osVersion)
    {
        string[] osVersionParts = osVersion.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return osVersionParts.Length >= 2 &&
            Version.TryParse(osVersionParts[1], out Version? parsedVersion) &&
            parsedVersion.Major >= 18;
    }

    public async Task<bool> ClearSimulatorResultsAsync(
        RunMode runMode,
        string osVersion,
        string udid,
        string bundleIdentifier,
        CancellationToken cancellationToken)
    {
        if (!IsSimulatorVersionSupported(osVersion))
        {
            return true;
        }

        string sourcePath = GetSimulatorResultsPath(runMode);
        string command = $"rm -f \"$(xcrun simctl get_app_container {udid} {bundleIdentifier} data){sourcePath}\"";
        var result = await _processManager.ExecuteCommandAsync(
            "/bin/bash",
            new[] { "-c", command },
            _mainLog,
            _mainLog,
            _mainLog,
            TimeSpan.FromMinutes(1),
            cancellationToken: cancellationToken);

        if (result.Succeeded)
        {
            return true;
        }

        _mainLog.WriteLine("Failed to clear previous test results from simulator.");
        return false;
    }

    public async Task<bool> CopySimulatorResultsAsync(
        RunMode runMode,
        string osVersion,
        string udid,
        string bundleIdentifier,
        string hostDestinationPath,
        CancellationToken cancellationToken)
    {
        string[] osVersionParts = osVersion.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (osVersionParts.Length < 2 || !Version.TryParse(osVersionParts[1], out _))
        {
            _mainLog.WriteLine("Simulator OS version is not in the expected format, skipping result copying.");
            return false;
        }

        if (!IsSimulatorVersionSupported(osVersion))
        {
            return true;
        }

        string sourcePath = GetSimulatorResultsPath(runMode);
        string command = $"cp \"$(xcrun simctl get_app_container {udid} {bundleIdentifier} data){sourcePath}\" \"{hostDestinationPath}\"";

        await _processManager.ExecuteCommandAsync(
            "/bin/bash",
            new[] { "-c", command },
            _mainLog,
            _mainLog,
            _mainLog,
            TimeSpan.FromMinutes(1),
            cancellationToken: cancellationToken);

        if (File.Exists(hostDestinationPath))
        {
            return true;
        }

        _mainLog.WriteLine($"Failed to copy results file from simulator. Expected at: {hostDestinationPath}");
        return false;
    }

    // This path is set by iOSApplicationEntryPointBase in the architecture-specific test apps.
    private static string GetSimulatorResultsPath(RunMode runMode)
        => runMode == RunMode.iOS ||
            runMode == RunMode.Sim64 ||
            runMode == RunMode.Sim32 ||
            runMode == RunMode.Classic
            ? "/Documents/test-results.xml"
            : "/Library/Caches/Documents/test-results.xml";
}
