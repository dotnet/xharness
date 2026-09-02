// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared;

public class ResultFileHandler : IResultFileHandler
{
    private static readonly int[] DefaultRetryDelaysMs = { 5_000, 10_000, 20_000 };

    private IMlaunchProcessManager _processManager;
    private IFileBackedLog _mainLog;
    private readonly int[] _retryDelaysMs;

    public int LastCopyAttempts { get; private set; }

    public ResultFileHandler(IMlaunchProcessManager pm, IFileBackedLog fs, int[]? retryDelaysMs = null)
    {
        _processManager = pm;
        _mainLog = fs;
        // Clone to prevent external mutation of the shared default array
        _retryDelaysMs = (retryDelaysMs ?? DefaultRetryDelaysMs).ToArray();
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
        if (!ShouldCopyFromAppContainer(runMode, osVersion, isSimulator))
        {
            return true;
        }

        return await CopyFileFromAppContainerAsync(
            runMode,
            isSimulator,
            udid,
            bundleIdentifier,
            GetAppContainerSourcePath(runMode, "test-results.xml"),
            hostDestinationPath,
            "results file");
    }

    public async Task<bool> CopyCoverageResultsAsync(
        RunMode runMode,
        bool isSimulator,
        string osVersion,
        string udid,
        string bundleIdentifier,
        string coverageFileName,
        string hostDestinationPath)
    {
        if (!ShouldCopyFromAppContainer(runMode, osVersion, isSimulator))
        {
            return false;
        }

        return await CopyFileFromAppContainerAsync(
            runMode,
            isSimulator,
            udid,
            bundleIdentifier,
            GetAppContainerSourcePath(runMode, coverageFileName),
            hostDestinationPath,
            "coverage results file");
    }

    private bool ShouldCopyFromAppContainer(RunMode runMode, string osVersion, bool isSimulator)
        => runMode == RunMode.MacOS || IsVersionSupported(osVersion, isSimulator);

    public static string GetAppContainerSourcePath(RunMode runMode, string fileName)
        => runMode is RunMode.iOS or RunMode.MacOS
            ? $"/Documents/{fileName}"
            : $"/Library/Caches/Documents/{fileName}";

    private async Task<bool> CopyFileFromAppContainerAsync(
        RunMode runMode,
        bool isSimulator,
        string udid,
        string bundleIdentifier,
        string sourcePath,
        string hostDestinationPath,
        string fileDescription)
    {
        LastCopyAttempts = 0;

        string copySourceDescription;
        string cmd;
        if (runMode == RunMode.MacOS)
        {
            string containerPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "Containers",
                bundleIdentifier,
                "Data");
            copySourceDescription = "MacCatalyst app container";
            cmd = $"cp \"{containerPath}{sourcePath}\" \"{hostDestinationPath}\"";
        }
        else if (isSimulator)
        {
            copySourceDescription = "simulator";
            cmd = $"cp \"$(xcrun simctl get_app_container {udid} {bundleIdentifier} data){sourcePath}\" \"{hostDestinationPath}\"";
        }
        else
        {
            copySourceDescription = "device";
            cmd = $"xcrun devicectl device copy from --device {udid} --source {sourcePath} --destination {hostDestinationPath} --user mobile --domain-type appDataContainer --domain-identifier {bundleIdentifier}";
        }

        // Retry up to 3 times with increasing delays to handle transient device communication errors
        // (e.g., com.apple.Mercury.error 1000 or RSD error 0xE8000003 on tvOS devices).
        for (int attempt = 0; attempt <= _retryDelaysMs.Length; attempt++)
        {
            LastCopyAttempts = attempt + 1;

            if (attempt > 0)
            {
                int delayMs = _retryDelaysMs[attempt - 1];
                _mainLog.WriteLine($"Retrying {fileDescription} copy (attempt {attempt + 1}) after {delayMs / 1000}s delay...");
                await Task.Delay(delayMs);

                // Remove a partial/failed destination file before retrying
                if (File.Exists(hostDestinationPath))
                {
                    File.Delete(hostDestinationPath);
                }
            }

            await _processManager.ExecuteCommandAsync(
                "/bin/bash",
                new[] { "-c", cmd },
                _mainLog,
                _mainLog,
                _mainLog,
                TimeSpan.FromMinutes(1),
                null);

            if (File.Exists(hostDestinationPath))
            {
                return true;
            }

            _mainLog.WriteLine($"Failed to copy {fileDescription} from {copySourceDescription} (attempt {attempt + 1}). Expected at: {hostDestinationPath}");
        }

        return false;
    }
}
