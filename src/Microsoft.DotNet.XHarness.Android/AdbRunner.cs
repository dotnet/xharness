﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.DotNet.XHarness.Android.Execution;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.Android;

public class AdbRunner
{
    #region Constructor and state variables

    private const string AdbEnvironmentVariableName = "ADB_EXE_PATH";
    private const string AdbDeviceFullInstallFailureMessage = "INSTALL_FAILED_INSUFFICIENT_STORAGE";
    private const string AdbInstallBrokenPipeError = "Failure calling service package: Broken pipe";
    private const string AdbInstallException = "Exception occurred while executing 'install':";
    private const string AdbShellPropertyForBootCompletion = "sys.boot_completed";
    private int? _api;
    private readonly string _absoluteAdbExePath;
    private readonly ILogger _log;
    private readonly IAdbProcessManager _processManager;
    private readonly Dictionary<string, string[]> _commandList = new()
    {
        { "architecture", new[] { "shell", "getprop", "ro.product.cpu.abilist" } },
        { "app", new[] { "shell", "pm", "list", "packages", "-3" } },
    };

    public int APIVersion => _api ?? GetAPIVersion();

    public string AdbExePath => _absoluteAdbExePath;

    public AdbRunner(ILogger log, string adbExePath = "") : this(log, new AdbProcessManager(log), adbExePath) { }

    public AdbRunner(ILogger log, IAdbProcessManager processManager, string adbExePath = "")
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));

        // If we don't get passed one in, use the real implementation
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));

        // We need to find ADB.exe somewhere
        string? environmentPath = Environment.GetEnvironmentVariable(AdbEnvironmentVariableName);
        if (!string.IsNullOrEmpty(environmentPath))
        {
            _log.LogDebug($"Using {AdbEnvironmentVariableName} environment variable ({environmentPath}) for ADB path.");
            adbExePath = environmentPath;
        }

        if (string.IsNullOrEmpty(adbExePath))
        {
            adbExePath = GetCliAdbExePath();
        }

        _absoluteAdbExePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(adbExePath));

        if (!File.Exists(_absoluteAdbExePath))
        {
            _log.LogError($"Unable to find adb.exe");
            throw new FileNotFoundException($"Could not find adb.exe. Either set it in the environment via {AdbEnvironmentVariableName} or call with valid path (provided:  '{adbExePath}')", adbExePath);
        }

        if (!_absoluteAdbExePath.Equals(adbExePath))
        {
            _log.LogDebug($"ADBRunner using ADB.exe supplied from {adbExePath}");
            _log.LogDebug($"Full resolved path:'{_absoluteAdbExePath}'");
        }
    }

    public void SetActiveDevice(string? deviceSerialNumber)
    {
        _processManager.DeviceSerial = deviceSerialNumber ?? string.Empty;

        _api = GetAPIVersion();

        _log.LogInformation($"Active Android device set to serial '{deviceSerialNumber}'");
    }

    private int GetAPIVersion()
    {
        var output = RunAdbCommand(new[] { "shell", "getprop", "ro.build.version.sdk" });
        return int.Parse(output.StandardOutput);
    }

    private static string GetCliAdbExePath()
    {
        var currentAssemblyDirectory = Path.GetDirectoryName(typeof(AdbRunner).Assembly.Location);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.Join(currentAssemblyDirectory, @"..\..\..\runtimes\any\native\adb\windows\adb.exe");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return Path.Join(currentAssemblyDirectory, @"../../../runtimes/any/native/adb/linux/adb");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Path.Join(currentAssemblyDirectory, @"../../../runtimes/any/native/adb/macos/adb");
        }
        throw new NotSupportedException("Cannot determine OS platform being used, thus we can not select an ADB executable.");
    }

    #endregion

    #region Functions

    public TimeSpan TimeToWaitForBootCompletion { get; set; } = TimeSpan.FromMinutes(5);

    public string GetAdbVersion() => RunAdbCommand(new[] { "version" }).StandardOutput;

    public string GetAdbState() => RunAdbCommand(new[] { "get-state" }).StandardOutput;

    public string RebootAndroidDevice() => RunAdbCommand(new[] { "reboot" }).StandardOutput;

    public void ClearAdbLog() => RunAdbCommand(new[] { "logcat", "-c" });

    public void EnableWifi(bool enable) => RunAdbCommand(new[] { "shell", "svc", "wifi", enable ? "enable" : "disable" });

    public void DumpAdbLog(string outputFilePath, string filterSpec = "")
    {
        // Workaround: Doesn't seem to have a flush() function and sometimes it doesn't have the full log on emulators.
        Thread.Sleep(3000);

        var result = RunAdbCommand(new[] { "logcat", "-d", filterSpec }, TimeSpan.FromMinutes(2));
        if (result.ExitCode != 0)
        {
            // Could throw here, but it would tear down a possibly otherwise acceptable execution.
            _log.LogError($"Error getting ADB log:{Environment.NewLine}{result}");
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath) ?? throw new ArgumentNullException(nameof(outputFilePath)));
            File.WriteAllText(outputFilePath, result.StandardOutput);
            _log.LogInformation($"Wrote current ADB log to {outputFilePath}");
        }
    }

    public string DumpBugReport(string outputFilePathWithoutFormat)
    {
        var reportManager = AdbReportFactory.CreateReportManager(_log, _api ?? GetAPIVersion());
        return reportManager.DumpBugReport(this, outputFilePathWithoutFormat);
    }

    public void WaitForDevice()
    {
        // This command waits for ANY kind of device to be available (emulator or real)
        // Needed because emulators start up asynchronously and take a while.
        // (Returns instantly if device is ready)
        // This can fail if _currentDevice is unset if there are multiple devices.
        _log.LogInformation("Waiting for device to be available (max 5 minutes)");
        var result = RunAdbCommand(new[] { "wait-for-device" }, TimeSpan.FromMinutes(5));
        _log.LogDebug($"{result.StandardOutput}");
        if (result.ExitCode != 0)
        {
            throw new Exception($"Error waiting for Android device/emulator.  Std out:{result.StandardOutput} Std. Err: {result.StandardError}.  Do you need to set the current device?");
        }

        // Some users will be installing the emulator and immediately calling xharness, they need to be able to expect the device is ready to load APKs.
        // Once wait-for-device returns, we'll give it up to TimeToWaitForBootCompletion (default 5 min) for 'adb shell getprop sys.boot_completed'
        // to be '1' (as opposed to empty) to make subsequent automation happy.
        var began = DateTimeOffset.UtcNow;
        var waitingUntil = began.Add(TimeToWaitForBootCompletion);
        var bootCompleted = RunAdbCommand(new[] { "shell", "getprop", AdbShellPropertyForBootCompletion });

        while (!bootCompleted.StandardOutput.Trim().StartsWith("1") && DateTimeOffset.UtcNow < waitingUntil)
        {
            bootCompleted = RunAdbCommand(new[] { "shell", "getprop", AdbShellPropertyForBootCompletion });
            _log.LogDebug($"{AdbShellPropertyForBootCompletion} = '{bootCompleted.StandardOutput.Trim()}'");
            Thread.Sleep((int)TimeSpan.FromSeconds(10).TotalMilliseconds);
        }

        if (bootCompleted.StandardOutput.Trim().StartsWith("1"))
        {
            _log.LogDebug($"Waited {DateTimeOffset.UtcNow.Subtract(began).TotalSeconds} seconds for device for {AdbShellPropertyForBootCompletion} to be 1.");
        }
        else
        {
            _log.LogWarning($"Did not detect boot completion variable on device; variable used ('{AdbShellPropertyForBootCompletion}') may be incorrect or device may be in a bad state");
        }
    }

    public void StartAdbServer()
    {
        var result = RunAdbCommand(new[] { "start-server" });
        _log.LogDebug($"{result.StandardOutput}");
        if (result.ExitCode != 0)
        {
            throw new Exception($"Error starting ADB Server.  Std out:{result.StandardOutput} Std. Err: {result.StandardError}");
        }
    }

    public void KillAdbServer()
    {
        var result = RunAdbCommand(new[] { "kill-server" });
        if (result.ExitCode != 0)
        {
            throw new Exception($"Error killing ADB Server.  Std out:{result.StandardOutput} Std. Err: {result.StandardError}");
        }
    }

    public int InstallApk(string apkPath)
    {
        _log.LogInformation($"Attempting to install {apkPath}: ");
        if (string.IsNullOrEmpty(apkPath))
        {
            throw new ArgumentException($"No value supplied for {nameof(apkPath)} ");
        }
        if (!File.Exists(apkPath))
        {
            throw new FileNotFoundException($"Could not find {apkPath}", apkPath);
        }

        var result = RunAdbCommand(new[] { "install", apkPath });

        // Two possible retry scenarios, theoretically both can happen on the same run:

        // 1. Pipe between ADB server and emulator device is broken; restarting the ADB server helps
        if (result.ExitCode == (int)AdbExitCodes.ADB_BROKEN_PIPE || result.StandardError.Contains(AdbInstallBrokenPipeError))
        {
            _log.LogWarning($"Hit broken pipe error; Will make one attempt to restart ADB server, then retry the install");
            KillAdbServer();
            StartAdbServer();
            result = RunAdbCommand(new[] { "install", apkPath });
        }

        // 2. Installation cache on device is messed up; restarting the device reliably seems to unblock this (unless the device is actually full, if so this will error the same)
        if (result.ExitCode != (int)AdbExitCodes.SUCCESS && result.StandardError.Contains(AdbDeviceFullInstallFailureMessage))
        {
            _log.LogWarning($"It seems the package installation cache may be full on the device.  We'll try to reboot it before trying one more time.{Environment.NewLine}Output:{result}");
            RebootAndroidDevice();
            WaitForDevice();
            result = RunAdbCommand(new[] { "install", apkPath });
        }

        // 3. Installation timed out or failed with exception; restarting the ADB server, reboot the device and give more time for installation
        // installer might hang up so we need to clean it up and free memory
        if (result.ExitCode == (int)AdbExitCodes.INSTRUMENTATION_TIMEOUT || (result.ExitCode != (int)AdbExitCodes.SUCCESS && result.StandardError.Contains(AdbInstallException)))
        {
            _log.LogWarning($"Installation failed; Will make one attempt to restart ADB server and the device, then retry the install");
            KillAdbServer();
            StartAdbServer();
            RebootAndroidDevice();
            WaitForDevice();
            result = RunAdbCommand(new[] { "install", apkPath }, TimeSpan.FromMinutes(10));
        }

        if (result.ExitCode != 0)
        {
            _log.LogError($"Error:{Environment.NewLine}{result}");
        }
        else
        {
            _log.LogInformation($"Successfully installed {apkPath}.");
        }

        return result.ExitCode;
    }

    public int UninstallApk(string apkName)
    {
        if (string.IsNullOrEmpty(apkName))
        {
            throw new ArgumentNullException(nameof(apkName));
        }

        _log.LogInformation($"Attempting to remove apk '{apkName}': ");
        var result = RunAdbCommand(new[] { "uninstall", apkName });

        // See note above in install()
        if (result.ExitCode == (int)AdbExitCodes.ADB_BROKEN_PIPE)
        {
            _log.LogWarning($"Hit broken pipe error; Will make one attempt to restart ADB server, and retry the uninstallation");

            KillAdbServer();
            StartAdbServer();
            result = RunAdbCommand(new[] { "uninstall", apkName });
        }

        if (result.ExitCode == (int)AdbExitCodes.SUCCESS)
        {
            _log.LogInformation($"Successfully uninstalled {apkName}.");
        }
        else if (result.ExitCode == (int)AdbExitCodes.ADB_UNINSTALL_APP_NOT_ON_DEVICE ||
                 result.ExitCode == (int)AdbExitCodes.ADB_UNINSTALL_APP_NOT_ON_EMULATOR)
        {
            _log.LogInformation($"APK '{apkName}' not on device.");
        }
        else
        {
            _log.LogError(message: $"Error: {result}");
        }
        return result.ExitCode;
    }

    // This function works but given we'll likely only be using Instrumentations doesn't matter.
    public int KillApk(string apkName)
    {
        _log.LogInformation($"Killing all running processes for '{apkName}': ");
        var result = RunAdbCommand(new[] { "shell", "am", "kill", "--user", "all", apkName});
        if (result.ExitCode != (int)AdbExitCodes.SUCCESS)
        {
            _log.LogError($"Error:{Environment.NewLine}{result}");
        }
        else
        {
            _log.LogDebug($"Success!{Environment.NewLine}{result.StandardOutput}");
        }
        return result.ExitCode;
    }

    // Assumes the directory is empty so any files present after the pull are new.
    public List<string> PullFiles(string apkPackageName, string devicePath, string localPath)
    {
        if (string.IsNullOrEmpty(localPath))
        {
            throw new ArgumentNullException(nameof(localPath));
        }

        string tempFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(tempFolder);
            Directory.CreateDirectory(localPath);
            _log.LogInformation($"Attempting to pull contents of {devicePath} to {localPath}");
            var copiedFiles = new List<string>();

            var result = RunAdbCommand(new[] { "pull", devicePath, tempFolder });

            if (result.ExitCode != (int)AdbExitCodes.SUCCESS)
            {
                if (APIVersion == 30)
                {
                    // On Android API 30 we can't use "adb pull" directly due to permission issues on emulators, see https://github.com/dotnet/xharness/issues/385
                    // As a workaround we copy the files to the temp directory on the device using "run-as" and pull from there
                    _log.LogInformation($"Failed to pull file. Device is running Android API 30, trying fallback to pull {devicePath}");

                    result = RunAdbCommand(new[] { "shell", "run-as", apkPackageName, "ls", devicePath });

                    if (result.ExitCode != (int)AdbExitCodes.SUCCESS)
                    {
                        throw new Exception($"Failed checking for file using fallback: {result}");
                    }

                    string? fileName = devicePath.Split("/").LastOrDefault();
                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        throw new Exception($"Failed pulling file using fallback: Couldn't determine filename for {devicePath}");
                    }

                    string deviceTempPath = $"/data/local/tmp/{fileName}";

                    result = RunAdbCommand(new[] { "shell", "rm", "-f", deviceTempPath });

                    if (result.ExitCode != (int)AdbExitCodes.SUCCESS)
                    {
                        throw new Exception($"Failed removing {deviceTempPath} before using fallback: {result}");
                    }

                    result = RunAdbCommand(new[] { "shell", "touch", deviceTempPath });

                    if (result.ExitCode != (int)AdbExitCodes.SUCCESS)
                    {
                        throw new Exception($"Failed touching {deviceTempPath}: {result}");
                    }

                    result = RunAdbCommand(new[] { "shell", "run-as", apkPackageName, "cp", devicePath, deviceTempPath });

                    if (result.ExitCode != (int)AdbExitCodes.SUCCESS)
                    {
                        throw new Exception($"Failed copying file using fallback: {result}");
                    }

                    result = RunAdbCommand(new[] { "pull", deviceTempPath, tempFolder });

                    if (result.ExitCode != (int)AdbExitCodes.SUCCESS)
                    {
                        throw new Exception($"Failed pulling file using fallback: {result}");
                    }

                    result = RunAdbCommand(new[] { "shell", "rm", "-f", deviceTempPath });

                    if (result.ExitCode != (int)AdbExitCodes.SUCCESS)
                    {
                        throw new Exception($"Failed removing {deviceTempPath} after using fallback: {result}");
                    }
                }
                else
                {
                    throw new Exception($"Failed pulling files: {result}");
                }
            }

            var copiedToTemp = Directory.GetFiles(tempFolder, "*", SearchOption.AllDirectories);
            foreach (var filePath in copiedToTemp)
            {
                var relativePath = Path.GetRelativePath(tempFolder, filePath);
                var destinationPath = Path.Combine(localPath, relativePath);
                // if the file is already there, just warn and skip it.
                if (File.Exists(destinationPath))
                {
                    _log.LogWarning($"Skipping file copy as {destinationPath} already exists.");
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? throw new ArgumentException(nameof(destinationPath)));
                    File.Move(filePath, destinationPath);
                    copiedFiles.Add(destinationPath);
                }
            }
            _log.LogDebug($"Copied {copiedFiles.Count} files to {localPath}.");
            return copiedFiles;
        }
        finally
        {
            Directory.Delete(tempFolder, true);
        }
    }

    public Dictionary<string, string?> GetAttachedDevicesWithProperties(string property)
    {
        var devicesAndProperties = new Dictionary<string, string?>();

        IEnumerable<string> GetAdbArguments(string deviceSerial)
        {
            var args = new List<string>
            {
                "-s",
                deviceSerial,
            };

            args.AddRange(_commandList[property]);

            return args;
        }

        var result = RunAdbCommand(new[] { "devices", "-l" }, TimeSpan.FromSeconds(30));
        string[] standardOutputLines = result.StandardOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        // Retry up to 3 mins til we get output; if the ADB server isn't started the output will come from a child process and we'll miss it.
        int retriesLeft = 18;

        // We will keep retrying until we get something back like 'List of devices attached...{newline} {info about a device} ',
        // which when split on newlines ignoring empties will be at least 2 lines when there are any available devices.
        while (retriesLeft-- > 0 && standardOutputLines.Length < 2)
        {
            _log.LogDebug($"Unexpected response from adb devices -l:{Environment.NewLine}Exit code={result.ExitCode}{Environment.NewLine}Std. Output: {result.StandardOutput} {Environment.NewLine}Std. Error: {result.StandardError}");
            Thread.Sleep(10000);
            result = RunAdbCommand(new[] { "devices", "-l" }, TimeSpan.FromSeconds(30));
            standardOutputLines = result.StandardOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        }

        // Two lines = At least one device was found.  On a multi-device machine, we can't function without specifying device serial number.
        if (result.ExitCode == (int)AdbExitCodes.SUCCESS && standardOutputLines.Length >= 2)
        {
            // Start at 1 to skip first line, which is always 'List of devices attached'
            for (int lineNumber = 1; lineNumber < standardOutputLines.Length; lineNumber++)
            {
                _log.LogDebug($"Evaluating output line for device serial: {standardOutputLines[lineNumber]}");
                var lineParts = standardOutputLines[lineNumber].Split(' ');
                if (!string.IsNullOrEmpty(lineParts[0]))
                {
                    var deviceSerial = lineParts[0];

                    var shellResult = RunAdbCommand(GetAdbArguments(deviceSerial), TimeSpan.FromSeconds(30));

                    // Assumption:  All Devices on a machine running Xharness should attempt to be online or disconnected.
                    retriesLeft = 30; // Max 5 minutes (30 attempts * 10 second waits)
                    while (retriesLeft-- > 0 && shellResult.StandardError.Contains("device offline", StringComparison.OrdinalIgnoreCase))
                    {
                        _log.LogWarning($"Device '{deviceSerial}' is offline; retrying up to one minute.");
                        Thread.Sleep(10000);

                        shellResult = RunAdbCommand(GetAdbArguments(deviceSerial), TimeSpan.FromSeconds(30));
                    }

                    if (shellResult.ExitCode == (int)AdbExitCodes.SUCCESS)
                    {
                        devicesAndProperties.Add(deviceSerial, shellResult.StandardOutput.Trim());
                    }
                    else
                    {
                        _log.LogError($"Error trying to get device: {shellResult.StandardError}");
                        devicesAndProperties.Add(deviceSerial, null);
                    }
                }
            }
        }
        else
        {
            // Abandon the run here, don't just guess.
            _log.LogError($"Error: listing attached devices / emulators: {result.StandardError}. Check that any emulators have been started, and attached device(s) are connected via USB, powered-on, unlocked and authorized.");
            throw new Exception("One or more attached Android devices are offline or without USB debug permission");
        }
        return devicesAndProperties;
    }

    public string? GetDeviceToUse(ILogger logger, IEnumerable<string> apkRequiredProperties, string propertyName)
    {
        var allDevicesAndTheirProperties = GetAllDevicesToUse(logger, apkRequiredProperties, propertyName);
        if (allDevicesAndTheirProperties.Count > 0)
        {
            var firstAvailableCompatible = allDevicesAndTheirProperties.First();
            logger.LogDebug($"Using first-found compatible device of {allDevicesAndTheirProperties.Count} total- serial: '{firstAvailableCompatible.Key}' - {propertyName}: {firstAvailableCompatible.Value}");
            return firstAvailableCompatible.Key;
        }
        return null;
    }

    public string? GetUniqueDeviceToUse(ILogger logger, string apkRequiredProperties, string propertyName)
    {
        var devices = GetAllDevicesToUse(logger, new[] { apkRequiredProperties }, propertyName);
        if (devices.Count == 0)
        {
            logger.LogError($"Cannot find a device with {propertyName}={apkRequiredProperties}, please check that a device is attached");
            return null;
        }
        else if (devices.Count > 1)
        {
            logger.LogError($"There is more than one device with {propertyName}={apkRequiredProperties}, please provide --device-id to choose the required one");
            return null;
        }
        return devices.Keys.First();
    }

    public Dictionary<string, string> GetAllDevicesToUse(ILogger logger, IEnumerable<string> apkRequiredProperties, string propertyName)
    {

        var allDevicesAndTheirProperties = new Dictionary<string, string?>();
        try
        {
            allDevicesAndTheirProperties = GetAttachedDevicesWithProperties(propertyName);
        }
        catch (Exception toLog)
        {
            logger.LogError(toLog, $"Exception thrown while trying to find compatible device with {propertyName} '{ string.Join("', '", apkRequiredProperties) }'");

            return new Dictionary<string, string>();
        }

        if (allDevicesAndTheirProperties.Count == 0)
        {
            logger.LogError("No attached device detected");
            return new Dictionary<string, string>();
        }

        var result = allDevicesAndTheirProperties
            .Where(kvp => !string.IsNullOrEmpty(kvp.Value) && kvp.Value.Split(new char[] { ',', '\r', '\n' }).Intersect(apkRequiredProperties).Any())
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value!);

        if (result.Count == 0)
        {
            // In this case, the enumeration worked, we found one or more devices, but nothing matched the APK's architecture; fail out.
            logger.LogError($"No devices with {propertyName} '{ string.Join("', '", apkRequiredProperties) }' was found among attached and authorized devices.");
        }

        return result;
    }

    public string? GetDeviceArchitecture(ILogger logger, string? deviceName = null)
    {
        IEnumerable<string> args = new[] { "shell", "getprop", "ro.product.cpu.abi" };

        if (!string.IsNullOrEmpty(deviceName))
        {
            args = new[] { "-s", deviceName }.Concat(args);
        }

        var result = RunAdbCommand(args, TimeSpan.FromMinutes(1));

        if (!result.Succeeded)
        {
            _log.LogError("Failed to get device's architecture. Check if a device is attached / emulator is started" + Environment.NewLine + result.StandardError);
            return null;
        }

        _log.LogDebug($"Detected architecture `{result.StandardOutput}` on the selected device");

        return result.StandardOutput.Trim();
    }

    public ProcessExecutionResults RunApkInstrumentation(string apkName, string? instrumentationClassName, Dictionary<string, string> args, TimeSpan timeout)
    {
        string displayName = string.IsNullOrEmpty(instrumentationClassName) ? "{default}" : instrumentationClassName;

        var adbArgs = new List<string>
        {
            "shell", "am", "instrument"
        };

        adbArgs.AddRange(args.SelectMany(arg => new[] { "-e", arg.Key, arg.Value }));
        adbArgs.Add("-w");

        if (string.IsNullOrEmpty(instrumentationClassName))
        {
            _log.LogInformation($"Starting default instrumentation class on {apkName} (exit code 0 == success)");
            adbArgs.Add(apkName);
        }
        else
        {
            _log.LogInformation($"Starting instrumentation class '{instrumentationClassName}' on {apkName}");
            adbArgs.Add($"{apkName}/{instrumentationClassName}");
        }

        var stopWatch = Stopwatch.StartNew();
        var result = RunAdbCommand(adbArgs, timeout);
        stopWatch.Stop();

        if (result.ExitCode == (int)AdbExitCodes.INSTRUMENTATION_TIMEOUT)
        {
            _log.LogInformation($"Running instrumentation class {displayName} timed out after waiting {stopWatch.Elapsed.TotalSeconds} seconds");
        }
        else
        {
            _log.LogInformation($"Running instrumentation class {displayName} took {stopWatch.Elapsed.TotalSeconds} seconds");
        }

        _log.LogDebug(result.ToString());

        return result;
    }

    #endregion

    #region Process runner helpers

    public ProcessExecutionResults RunAdbCommand(IEnumerable<string> arguments) => RunAdbCommand(arguments, TimeSpan.FromMinutes(5));

    public ProcessExecutionResults RunAdbCommand(IEnumerable<string> arguments, TimeSpan timeOut)
    {
        if (!File.Exists(_absoluteAdbExePath))
        {
            throw new FileNotFoundException($"Provided path for adb.exe was not valid ('{_absoluteAdbExePath}')", _absoluteAdbExePath);
        }

        return _processManager.Run(_absoluteAdbExePath, arguments, timeOut);
    }

    #endregion
}
