﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.Apple
{
    /// <summary>
    /// Base class that implements the high level flow that enables running iOS/tvOS/MacCatalyst apps:
    ///   - Find device
    ///   - Install app
    ///   - Run/Test app (abstract)
    ///   - Clean up / uninstall
    /// </summary>
    internal abstract class AppleBaseOrchestrator<TArguments> : IDisposable where TArguments : AppleAppRunArguments
    {
        protected static readonly string s_mlaunchLldbConfigFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".mtouch-launch-with-lldb");

        private readonly IMlaunchProcessManager _processManager;
        private readonly DeviceFinder _deviceFinder;
        private readonly ILogger _logger;
        private readonly IFileBackedLog _mainLog;
        private readonly IErrorKnowledgeBase _errorKnowledgeBase;

        private bool _lldbFileCreated;

        protected AppleBaseOrchestrator(
            IMlaunchProcessManager processManager,
            DeviceFinder deviceFinder,
            ILogger consoleLogger,
            IFileBackedLog mainLog,
            IErrorKnowledgeBase errorKnowledgeBase)
        {
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            _deviceFinder = deviceFinder ?? throw new ArgumentNullException(nameof(deviceFinder));
            _logger = consoleLogger ?? throw new ArgumentNullException(nameof(consoleLogger));
            _mainLog = mainLog ?? throw new ArgumentNullException(nameof(mainLog));
            _errorKnowledgeBase = errorKnowledgeBase ?? throw new ArgumentNullException(nameof(errorKnowledgeBase));
        }

        protected abstract Task<ExitCode> ExecuteApp(
            TArguments arguments,
            IEnumerable<string> passthroughArguments,
            AppBundleInformation appBundleInfo,
            IDevice device,
            IDevice? companionDevice,
            TestTargetOs target,
            CancellationToken cancellationToken);

        protected abstract Task<ExitCode> ExecuteMacCatalystApp(
            TArguments arguments,
            IEnumerable<string> passthroughArguments,
            AppBundleInformation appBundleInfo,
            CancellationToken cancellationToken);

        public async Task<ExitCode> Execute(
            TArguments arguments,
            IEnumerable<string> passthroughArguments,
            TestTargetOs target,
            CancellationToken cancellationToken)
        {
            _lldbFileCreated = false;
            var isLldbEnabled = IsLldbEnabled();
            if (isLldbEnabled && !arguments.EnableLldb)
            {
                // the file is present, but the user did not set it, warn him about it
                _logger.LogWarning("Lldb will be used since '~/.mtouch-launch-with-lldb' was found in the system but it was not created by xharness.");
            }
            else if (arguments.EnableLldb)
            {
                if (!File.Exists(s_mlaunchLldbConfigFile))
                {
                    // create empty file
                    File.WriteAllText(s_mlaunchLldbConfigFile, string.Empty);
                    _lldbFileCreated = true;
                }
            }

            var appBundleInformationParser = new AppBundleInformationParser(_processManager);

            _logger.LogInformation("Getting app bundle information");

            AppBundleInformation appBundleInfo;

            try
            {
                appBundleInfo = await appBundleInformationParser.ParseFromAppBundle(
                    arguments.AppPackagePath,
                    target.Platform,
                    _mainLog,
                    cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to get bundle information: {e.Message}");
                return ExitCode.FAILED_TO_GET_BUNDLE_INFO;
            }

            ExitCode exitCode;
            IDevice device;
            IDevice? companionDevice;

            if (target.Platform == TestTarget.MacCatalyst)
            {
                try
                {
                    _logger.LogInformation($"Starting '{appBundleInfo.AppName}' on MacCatalyst");

                    return await ExecuteMacCatalystApp(arguments, passthroughArguments, appBundleInfo, cancellationToken);
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
                (device, companionDevice) = await _deviceFinder.FindDevice(target, arguments.DeviceName, _mainLog);

                _logger.LogInformation($"Found {(target.Platform.IsSimulator() ? "simulator" : "physical")} device '{device.Name}'");

                if (companionDevice != null)
                {
                    _logger.LogInformation($"Found companion {(target.Platform.IsSimulator() ? "simulator" : "physical")} device '{companionDevice.Name}'");
                }

                if (target.Platform.IsSimulator() && arguments.ResetSimulator)
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

            exitCode = await InstallApp(appBundleInfo, device, target, cancellationToken);

            if (exitCode != ExitCode.SUCCESS)
            {
                if (!target.Platform.IsSimulator())
                {
                    _logger.LogInformation($"Cleaning up the failed installation from '{device.Name}'");
                    await UninstallApp(appBundleInfo, device, new CancellationToken());
                }

                return exitCode;
            }

            try
            {
                _logger.LogInformation($"Starting application '{appBundleInfo.AppName}' on '{device.Name}'");

                exitCode = await ExecuteApp(
                    arguments,
                    passthroughArguments,
                    appBundleInfo,
                    device,
                    companionDevice,
                    target,
                    cancellationToken);
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
                if (target.Platform.IsSimulator() && arguments.ResetSimulator)
                {
                    await CleanUpSimulators(device, companionDevice);
                }

                if (!target.Platform.IsSimulator() && device != null)
                {
                    await UninstallApp(appBundleInfo, device, new CancellationToken());
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
        }

        private async Task<ExitCode> InstallApp(
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

        private async Task UninstallApp(AppBundleInformation appBundleInfo, IDevice device, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Uninstalling the application '{appBundleInfo.AppName}' from '{device.Name}'");

            var appUninstaller = new AppUninstaller(_processManager, _mainLog);
            var uninstallResult = await appUninstaller.UninstallApp(device, appBundleInfo.BundleIdentifier, cancellationToken);
            if (!uninstallResult.Succeeded)
            {
                _logger.LogError($"Failed to uninstall the app bundle with exit code: {uninstallResult.ExitCode}");
            }
            else
            {
                _logger.LogInformation($"Application '{appBundleInfo.AppName}' was uninstalled successfully");
            }
        }

        private async Task CleanUpSimulators(IDevice device, IDevice? companionDevice)
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
