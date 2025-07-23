// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

    public async Task<bool> CopyResultsAsync(
        RunMode runMode,
        bool isSimulator,
        string osVersion,
        string udid,
        string bundleIdentifier,
        string hostDestinationPath,
        CancellationToken token)
    {
        // This file path is set in iOSApplicationEntryPointBase
        string sourcePath = runMode == RunMode.iOS
            ? "/Documents/test-results.xml"
            : "/Library/Caches/Documents/test-results.xml";

        if (isSimulator)
        {
            // Version format contains string like "Simulator 18.0".
            string [] osVersionParts = osVersion.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (osVersionParts.Length < 2)
            {
                _mainLog.WriteLine("Simulator OS version is not in the expected format, skipping result copying.");
                return false;
            }

            if (!Version.TryParse(osVersionParts[1], out Version osVersionParsed))
            {
                _mainLog.WriteLine("Simulator OS version is not in the expected format, skipping result copying.");
                return false;
            }

            if (osVersionParsed.Major >= 18)
            {

                string cmd = $"cp \"$(xcrun simctl get_app_container {udid} {bundleIdentifier} data){sourcePath}\" \"{hostDestinationPath}\"";

                await _processManager.ExecuteCommandAsync(
                    "/bin/bash",
                    new[] { "-c", cmd },
                    _mainLog,
                    _mainLog,
                    _mainLog,
                    TimeSpan.FromMinutes(1),
                    null,
                    cancellationToken: token);

                if (!File.Exists(hostDestinationPath))
                {
                    _mainLog.WriteLine($"Failed to copy results file from simulator. Expected at: {hostDestinationPath}");
                    return false;
                }
            }

            return true;
        }
        else
        {
            if (!Version.TryParse(osVersion, out Version osVersionParsed))
            {
                _mainLog.WriteLine($"Device OS version is not in the expected format, skipping result copying.");
                return false;
            }

            if (osVersionParsed.Major >= 18)
            {
                string cmd = $"xcrun devicectl device copy from --device {udid} --source {sourcePath} --destination {hostDestinationPath} --user mobile --domain-type appDataContainer --domain-identifier {bundleIdentifier}";
                await _processManager.ExecuteCommandAsync(
                    "/bin/bash",
                    new List<string> { "-c", cmd },
                    _mainLog,
                    _mainLog,
                    _mainLog,
                    TimeSpan.FromMinutes(1),
                    null,
                    cancellationToken: token);

                if (!File.Exists(hostDestinationPath))
                {
                    _mainLog.WriteLine($"Failed to copy results file from device. Expected at: {hostDestinationPath}");
                    return false;
                }
            }

            return true;
        }
    }
}
