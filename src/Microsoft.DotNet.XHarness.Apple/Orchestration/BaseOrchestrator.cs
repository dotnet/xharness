// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
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

        private readonly IMlaunchProcessManager _processManager;
        private readonly DeviceFinder _deviceFinder;
        private readonly ILogger _logger;
        private readonly ILogs _logs;
        private readonly IFileBackedLog _mainLog;
        private readonly IErrorKnowledgeBase _errorKnowledgeBase;
        private readonly IHelpers _helpers;
        private bool _lldbFileCreated;

        protected BaseOrchestrator(
            IMlaunchProcessManager processManager,
            DeviceFinder deviceFinder,
            ILogger consoleLogger,
            ILogs logs,
            IFileBackedLog mainLog,
            IErrorKnowledgeBase errorKnowledgeBase,
            IHelpers helpers)
        {
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            _deviceFinder = deviceFinder ?? throw new ArgumentNullException(nameof(deviceFinder));
            _logger = consoleLogger ?? throw new ArgumentNullException(nameof(consoleLogger));
            _logs = logs ?? throw new ArgumentNullException(nameof(logs));
            _mainLog = mainLog ?? throw new ArgumentNullException(nameof(mainLog));
            _errorKnowledgeBase = errorKnowledgeBase ?? throw new ArgumentNullException(nameof(errorKnowledgeBase));
            _helpers = helpers ?? throw new ArgumentNullException(nameof(helpers));
        }

        protected async Task<ExitCode> OrchestrateRun(
            TestTargetOs target,
            string? deviceName,
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

                    if (_errorKnowledgeBase.IsKnownTestIssue(_mainLog, out var failureMessage))
                    {
                        message.Append(failureMessage.Value.HumanMessage);
                        if (failureMessage.Value.IssueLink != null)
                        {
                            message.AppendLine($" Find more information at {failureMessage.Value.IssueLink}");
                        }
                    }
                    else
                    {
                        message.AppendLine(e.ToString());
                    }

                    _logger.LogError(message.ToString());

                    return ExitCode.APP_LAUNCH_FAILURE;
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

                (device, companionDevice) = await _deviceFinder.FindDevice(target, deviceName, finderLog);

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

            // Uninstall the app first to get a clean state
            await UninstallApp(target.Platform, appBundleInfo.BundleIdentifier, device, cancellationToken);

            exitCode = await InstallApp(appBundleInfo, device, target, cancellationToken);

            if (exitCode != ExitCode.SUCCESS)
            {
                _logger.LogInformation($"Cleaning up the failed installation from '{device.Name}'");
                await UninstallApp(target.Platform, appBundleInfo.BundleIdentifier, device, new CancellationToken());

                return exitCode;
            }

            try
            {
                exitCode = await executeApp(appBundleInfo, device, companionDevice);
            }
            catch (Exception e)
            {
                var message = new StringBuilder().AppendLine("Application run failed:");

                if (_errorKnowledgeBase.IsKnownTestIssue(_mainLog, out var failureMessage))
                {
                    message.Append(failureMessage.Value.HumanMessage);
                    if (failureMessage.Value.IssueLink != null)
                    {
                        message.AppendLine($" Find more information at {failureMessage.Value.IssueLink}");
                    }
                }
                else
                {
                    message.AppendLine(e.ToString());
                }

                _logger.LogError(message.ToString());

                exitCode = ExitCode.APP_LAUNCH_FAILURE;
            }
            finally
            {
                if (target.Platform.IsSimulator() && resetSimulator)
                {
                    await CleanUpSimulators(device, companionDevice);
                }

                if (device != null)
                {
                    await UninstallApp(target.Platform, appBundleInfo.BundleIdentifier, device, new CancellationToken());
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

            var appInstaller = new AppInstaller(_processManager, _mainLog);

            ProcessExecutionResult result;

            try
            {
                result = await appInstaller.InstallApp(appBundleInfo, target, device, cancellationToken: cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to install the app bundle:{Environment.NewLine}{e}");
                return ExitCode.PACKAGE_INSTALLATION_FAILURE;
            }

            if (!result.Succeeded)
            {
                // use the knowledge base class to decide if the error is known, if it is, let the user know
                // the failure reason
                if (_errorKnowledgeBase.IsKnownInstallIssue(_mainLog, out var errorMessage))
                {
                    var error = new StringBuilder()
                        .AppendLine("Failed to install the application")
                        .AppendLine(errorMessage.Value.HumanMessage);

                    if (errorMessage.Value.IssueLink != null)
                    {
                        error
                            .AppendLine()
                            .AppendLine($" Find more information at {errorMessage.Value.IssueLink}");
                    }

                    _logger.LogError(error.ToString());
                }
                else
                {
                    _logger.LogError($"Failed to install the application");
                }

                return ExitCode.PACKAGE_INSTALLATION_FAILURE;
            }

            _logger.LogInformation($"Application '{appBundleInfo.AppName}' was installed successfully on '{device.Name}'");

            return ExitCode.SUCCESS;
        }

        protected virtual async Task UninstallApp(TestTarget target, string bundleIdentifier, IDevice device, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Uninstalling the application '{bundleIdentifier}' from '{device.Name}'");

            var appUninstaller = new AppUninstaller(_processManager, _mainLog);
            ProcessExecutionResult uninstallResult = target.IsSimulator()
                ? await appUninstaller.UninstallApp(device, bundleIdentifier, cancellationToken)
                : await appUninstaller.UninstallDeviceApp(device, bundleIdentifier, cancellationToken);

            if (!uninstallResult.Succeeded)
            {
                _logger.LogError($"Failed to uninstall the app bundle! Check logs for more details!");
            }
            else
            {
                _logger.LogInformation($"Application '{bundleIdentifier}' was uninstalled successfully");
            }
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
