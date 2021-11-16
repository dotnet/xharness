// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

namespace Microsoft.DotNet.XHarness.Apple
{
    /// <summary>
    /// Base class that implements the high level flow that enables running iOS/tvOS/MacCatalyst apps:
    ///   - Find device (+ prepare / reset)
    ///   - Install app
    ///   - Run/Test app (abstract)
    ///   - Clean up / uninstall
    ///   - Dispose everything properly
    /// </summary>
    public abstract class BaseOrchestrator : IDisposable
    {
        protected static readonly string s_mlaunchLldbConfigFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".mtouch-launch-with-lldb");
        private readonly IAppInstaller _appInstaller;
        private readonly IAppUninstaller _appUninstaller;
        private readonly IDeviceFinder _deviceFinder;
        private readonly ILogger _logger;
        private readonly ILogs _logs;
        private readonly IFileBackedLog _mainLog;
        private readonly IErrorKnowledgeBase _errorKnowledgeBase;
        private readonly IDiagnosticsData _diagnosticsData;
        private readonly IHelpers _helpers;

        private bool _lldbFileCreated;

        protected BaseOrchestrator(
            IAppInstaller appInstaller,
            IAppUninstaller appUninstaller,
            IDeviceFinder deviceFinder,
            ILogger consoleLogger,
            ILogs logs,
            IFileBackedLog mainLog,
            IErrorKnowledgeBase errorKnowledgeBase,
            IDiagnosticsData diagnosticsData,
            IHelpers helpers)
        {
            _appInstaller = appInstaller ?? throw new ArgumentNullException(nameof(appInstaller));
            _appUninstaller = appUninstaller ?? throw new ArgumentNullException(nameof(appUninstaller));
            _deviceFinder = deviceFinder ?? throw new ArgumentNullException(nameof(deviceFinder));
            _logger = consoleLogger ?? throw new ArgumentNullException(nameof(consoleLogger));
            _logs = logs ?? throw new ArgumentNullException(nameof(logs));
            _mainLog = mainLog ?? throw new ArgumentNullException(nameof(mainLog));
            _errorKnowledgeBase = errorKnowledgeBase ?? throw new ArgumentNullException(nameof(errorKnowledgeBase));
            _diagnosticsData = diagnosticsData ?? throw new ArgumentNullException(nameof(diagnosticsData));
            _helpers = helpers ?? throw new ArgumentNullException(nameof(helpers));
        }

        protected async Task<ExitCode> OrchestrateOperation(
            TestTargetOs target,
            string? deviceName,
            bool includeWirelessDevices,
            bool resetSimulator,
            bool enableLldb,
            AppBundleInformation appBundleInfo,
            Func<AppBundleInformation, Task<ExitCode>> executeMacCatalystApp,
            Func<AppBundleInformation, IDevice, IDevice?, Task<ExitCode>> executeApp,
            CancellationToken cancellationToken)
        {
            _lldbFileCreated = false;
            var isLldbEnabled = IsLldbEnabled();
            if (isLldbEnabled && !enableLldb)
            {
                // the file is present, but the user did not set it, warn him about it
                _logger.LogWarning("Lldb will be used since '~/.mtouch-launch-with-lldb' was found in the system but it was not created by xharness.");
            }
            else if (enableLldb)
            {
                if (!File.Exists(s_mlaunchLldbConfigFile))
                {
                    // create empty file
                    File.WriteAllText(s_mlaunchLldbConfigFile, string.Empty);
                    _lldbFileCreated = true;
                }
            }

            if (includeWirelessDevices && target.Platform.IsSimulator())
            {
                _logger.LogWarning("Including wireless devices while targeting a simulator has no effect");
            }

            ExitCode exitCode;
            IDevice device;
            IDevice? companionDevice;

            if (target.Platform == TestTarget.MacCatalyst)
            {
                try
                {
                    return await executeMacCatalystApp(appBundleInfo);
                }
                catch (Exception e)
                {
                    var message = new StringBuilder().AppendLine("Application run failed:");
                    exitCode = ExitCode.APP_LAUNCH_FAILURE;

                    if (_errorKnowledgeBase.IsKnownTestIssue(_mainLog, out var failure))
                    {
                        message.Append(failure.HumanMessage);
                        if (failure.IssueLink != null)
                        {
                            message.AppendLine($" Find more information at {failure.IssueLink}");
                        }

                        if (failure.SuggestedExitCode.HasValue)
                        {
                            exitCode = (ExitCode)failure.SuggestedExitCode.Value;
                        }
                    }
                    else
                    {
                        message.AppendLine(e.ToString());
                    }

                    _logger.LogError(message.ToString());

                    return exitCode;
                }
            }

            try
            {
                _logger.LogInformation($"Looking for available {target.AsString()} {(target.Platform.IsSimulator() ? "simulators" : "devices")}..");

                var finderLogName = $"list-{target.AsString()}-{_helpers.Timestamp}.log";
                using var finderLog = _logs.Create(finderLogName, "DeviceList", true);

                _mainLog.WriteLine(
                    $"Looking for available {target.AsString()} {(target.Platform.IsSimulator() ? "simulators" : "devices")}. " +
                    $"Storing logs into {finderLogName}");

                (device, companionDevice) = await _deviceFinder.FindDevice(target, deviceName, finderLog, includeWirelessDevices);

                _logger.LogInformation($"Found {(target.Platform.IsSimulator() ? "simulator" : "physical")} device '{device.Name}'");

                if (companionDevice != null)
                {
                    _logger.LogInformation($"Found companion {(target.Platform.IsSimulator() ? "simulator" : "physical")} device '{companionDevice.Name}'");
                }

                if (target.Platform.IsSimulator() && resetSimulator)
                {
                    var simulator = (ISimulatorDevice)device;

                    _logger.LogInformation($"Reseting simulator '{device.Name}'");
                    await simulator.PrepareSimulator(_mainLog, appBundleInfo.BundleIdentifier);

                    if (companionDevice != null)
                    {
                        _logger.LogInformation($"Reseting companion simulator '{companionDevice.Name}'");
                        var companionSimulator = (ISimulatorDevice)companionDevice;
                        await companionSimulator.PrepareSimulator(_mainLog, appBundleInfo.BundleIdentifier);
                    }

                    _logger.LogInformation("Simulator reset finished");
                }
            }
            catch (NoDeviceFoundException e)
            {
                _logger.LogError(e.Message);
                return ExitCode.DEVICE_NOT_FOUND;
            }

            // Note down the actual test target
            // For simulators (e.g. "iOS 13.4"), we strip the iOS part and keep the version only, for devices there's no OS
            _diagnosticsData.TargetOS = target.Platform.IsSimulator() ? device.OSVersion.Split(' ', 2).Last() : device.OSVersion;
            _diagnosticsData.Device = device.Name ?? device.UDID;

            // Uninstall the app first to get a clean state
            if (!resetSimulator)
            {
                var uninstallResult = await UninstallApp(target.Platform, appBundleInfo.BundleIdentifier, device, isPreparation: true, cancellationToken);
                if (uninstallResult == ExitCode.SIMULATOR_FAILURE)
                {
                    // Sometimes the simulator gets in a bad shape and we won't be able to run the app, we can tell here already
                    return ExitCode.SIMULATOR_FAILURE;
                }
            }

            exitCode = await InstallApp(appBundleInfo, device, target, cancellationToken);

            if (exitCode != ExitCode.SUCCESS)
            {
                _logger.LogInformation($"Cleaning up the failed installation from '{device.Name}'");

                var uninstallResult = await UninstallApp(target.Platform, appBundleInfo.BundleIdentifier, device, isPreparation: false, new CancellationToken());
                if (uninstallResult == ExitCode.SIMULATOR_FAILURE)
                {
                    // Sometimes the simulator gets in a bad shape and we won't be able to run the app, we can tell here
                    return ExitCode.SIMULATOR_FAILURE;
                }

                return exitCode;
            }

            try
            {
                exitCode = await executeApp(appBundleInfo, device, companionDevice);
            }
            catch (Exception e)
            {
                exitCode = ExitCode.GENERAL_FAILURE;

                var message = new StringBuilder().AppendLine("Application run failed:");

                if (_errorKnowledgeBase.IsKnownTestIssue(_mainLog, out var failure))
                {
                    message.Append(failure.HumanMessage);
                    if (failure.IssueLink != null)
                    {
                        message.AppendLine($" Find more information at {failure.IssueLink}");
                    }

                    if (failure.SuggestedExitCode.HasValue)
                    {
                        exitCode = (ExitCode)failure.SuggestedExitCode.Value;
                    }
                }
                else
                {
                    message.AppendLine(e.ToString());
                }

                _logger.LogError(message.ToString());
            }
            finally
            {
                if (target.Platform.IsSimulator() && resetSimulator)
                {
                    await CleanUpSimulators(device, companionDevice);
                }
                else if (device != null) // Do not uninstall if device was cleaned up
                {
                    await UninstallApp(target.Platform, appBundleInfo.BundleIdentifier, device, false, new CancellationToken());
                }
            }

            return exitCode;
        }

        public void Dispose()
        {
            _mainLog.Dispose();

            if (_lldbFileCreated)
            {
                File.Delete(s_mlaunchLldbConfigFile);
            }

            GC.SuppressFinalize(this);
        }

        protected virtual async Task<ExitCode> InstallApp(
            AppBundleInformation appBundleInfo,
            IDevice device,
            TestTargetOs target,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Installing application '{appBundleInfo.AppName}' on '{device.Name}'");

            ProcessExecutionResult result;

            try
            {
                result = await _appInstaller.InstallApp(appBundleInfo, target, device, cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to install the app bundle:{Environment.NewLine}{e}");
                return ExitCode.PACKAGE_INSTALLATION_FAILURE;
            }

            if (!result.Succeeded)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogError($"Application installation timed out");
                    return ExitCode.PACKAGE_INSTALLATION_TIMEOUT;
                }

                var exitCode = ExitCode.PACKAGE_INSTALLATION_FAILURE;

                // use the knowledge base class to decide if the error is known, if it is, let the user know
                // the failure reason
                if (_errorKnowledgeBase.IsKnownInstallIssue(_mainLog, out var failure))
                {
                    var error = new StringBuilder()
                        .AppendLine("Failed to install the application")
                        .AppendLine(failure.HumanMessage);

                    if (failure.IssueLink != null)
                    {
                        error
                            .AppendLine()
                            .AppendLine($" Find more information at {failure.IssueLink}");
                    }

                    if (failure.SuggestedExitCode.HasValue)
                    {
                        exitCode = (ExitCode)failure.SuggestedExitCode.Value;
                    }

                    _logger.LogError(error.ToString());
                }
                else
                {
                    _logger.LogError($"Failed to install the application");
                }

                return exitCode;
            }

            _logger.LogInformation($"Application '{appBundleInfo.AppName}' was installed successfully on '{device.Name}'");

            return ExitCode.SUCCESS;
        }

        protected virtual async Task<ExitCode> UninstallApp(TestTarget target, string bundleIdentifier, IDevice device, bool isPreparation, CancellationToken cancellationToken)
        {
            if (isPreparation)
            {
                _logger.LogInformation($"Uninstalling any previous instance of '{bundleIdentifier}' from '{device.Name}'");
            }
            else
            {
                _logger.LogInformation($"Uninstalling the application '{bundleIdentifier}' from '{device.Name}'");
            }

            ProcessExecutionResult uninstallResult = target.IsSimulator()
                ? await _appUninstaller.UninstallSimulatorApp(device, bundleIdentifier, cancellationToken)
                : await _appUninstaller.UninstallDeviceApp(device, bundleIdentifier, cancellationToken);

            // We try to uninstall app before each run to clear it from the device
            // For those cases, we don't care about the result
            if (!isPreparation)
            {
                if (!uninstallResult.Succeeded)
                {
                    _logger.LogError($"Failed to uninstall the app bundle! Check logs for more details!");
                }
                else
                {
                    _logger.LogInformation($"Application '{bundleIdentifier}' was uninstalled successfully");
                }
            }

            if (uninstallResult.Succeeded)
            {
                return ExitCode.SUCCESS;
            }

            if (target.IsSimulator() && uninstallResult.ExitCode == 165)
            {
                // When uninstallation returns 165, the simulator is in a weird state where it cannot be booted and running an app later will fail
                _logger.LogCritical($"Failed to uninstall the application - bad simulator state detected!");
                return ExitCode.SIMULATOR_FAILURE;
            }

            return ExitCode.GENERAL_FAILURE;
        }

        protected virtual async Task CleanUpSimulators(IDevice device, IDevice? companionDevice)
        {
            try
            {
                var simulator = (ISimulatorDevice)device;

                _logger.LogInformation($"Cleaning up simulator '{device.Name}'");
                await simulator.KillEverything(_mainLog);

                if (companionDevice != null)
                {
                    _logger.LogInformation($"Cleaning up companion simulator '{companionDevice.Name}'");
                    var companionSimulator = (ISimulatorDevice)companionDevice;
                    await companionSimulator.KillEverything(_mainLog);
                }
            }
            finally
            {
                _logger.LogInformation("Simulators cleaned up");
            }
        }

        protected static bool IsLldbEnabled() => File.Exists(s_mlaunchLldbConfigFile);

        protected static void NotifyUserLldbCommand(ILogger logger, string line)
        {
            if (!line.Contains("mtouch-lldb-prep-cmds"))
            {
                return;
            }

            // let the user know the command to execute. Might change in mlaunch so trust the log
            var sb = new StringBuilder();
            sb.AppendLine("LLDB debugging is enabled.");
            sb.AppendLine("You must now execute:");
            sb.AppendLine(line.Substring(line.IndexOf("lldb", StringComparison.Ordinal)));

            logger.LogInformation(sb.ToString());
        }
    }
}
