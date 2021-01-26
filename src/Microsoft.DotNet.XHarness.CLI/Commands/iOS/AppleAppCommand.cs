// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Apple;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.iOS;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.Common.CLI.Commands;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.iOS
{
    internal abstract class AppleAppCommand : XHarnessCommand
    {
        protected static readonly string s_mlaunchLldbConfigFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".mtouch-launch-with-lldb");

        protected readonly ErrorKnowledgeBase ErrorKnowledgeBase = new ErrorKnowledgeBase();
        protected override XHarnessCommandArguments Arguments => iOSRunArguments;
        protected abstract iOSAppRunArguments iOSRunArguments { get; }

        private MlaunchProcessManager? _processManager = null;
        private HardwareDeviceLoader? _deviceLoader = null;
        private SimulatorLoader? _simulatorLoader = null;

        protected MlaunchProcessManager ProcessManager
        {
            get => _processManager ?? throw new NullReferenceException("ProcessManager wasn't initialized properly");
        }

        protected HardwareDeviceLoader DeviceLoader
        {
            get => _deviceLoader ?? throw new NullReferenceException("DeviceLoader wasn't initialized properly");
        }

        protected SimulatorLoader SimulatorLoader
        {
            get => _simulatorLoader ?? throw new NullReferenceException("SimulatorLoader wasn't initialized properly");
        }

        protected AppleAppCommand(string name, bool allowsExtraArgs, string? help = null) : base(name, allowsExtraArgs, help)
        {
        }

        protected abstract Task<ExitCode> RunAppInternal(
            AppBundleInformation appBundleInfo,
            string? deviceName,
            ILogger logger,
            TestTargetOs target,
            Logs logs,
            IFileBackedLog mainLog,
            CancellationToken cancellationToken);

        protected static bool IsLldbEnabled() => File.Exists(s_mlaunchLldbConfigFile);

        protected void NotifyUserLldbCommand(ILogger logger, string line)
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

        protected override async Task<ExitCode> InvokeInternal(ILogger logger)
        {
            // We have to set these here because command arguments are not initialized in the ctor yet
            _processManager = new MlaunchProcessManager(iOSRunArguments.XcodeRoot, iOSRunArguments.MlaunchPath);
            _deviceLoader = new HardwareDeviceLoader(_processManager);
            _simulatorLoader = new SimulatorLoader(_processManager);

            var logs = new Logs(iOSRunArguments.OutputDirectory);

            var cts = new CancellationTokenSource();
            cts.CancelAfter(iOSRunArguments.Timeout);

            var exitCode = ExitCode.SUCCESS;

            foreach (var target in iOSRunArguments.RunTargets)
            {
                var exitCodeForRun = await RunApp(logger, target, logs, cts.Token);

                if (exitCodeForRun != ExitCode.SUCCESS)
                {
                    exitCode = exitCodeForRun;
                }
            }

            return exitCode;
        }

        private async Task<ExitCode> RunApp(ILogger logger, TestTargetOs target, Logs logs, CancellationToken cancellationToken = default)
        {
            logger.LogInformation($"Preparing run for {target.AsString()}{ (iOSRunArguments.DeviceName != null ? " targeting " + iOSRunArguments.DeviceName : null) }");

            bool lldbFileCreated = false;
            var isLldbEnabled = IsLldbEnabled();
            if (isLldbEnabled && !iOSRunArguments.EnableLldb)
            {
                // the file is present, but the user did not set it, warn him about it
                logger.LogWarning("Lldb will be used since '~/.mtouch-launch-with-lldb' was found in the system but it was not created by xharness.");
            }
            else if (iOSRunArguments.EnableLldb)
            {
                if (!File.Exists(s_mlaunchLldbConfigFile))
                {
                    // create empty file
                    File.WriteAllText(s_mlaunchLldbConfigFile, string.Empty);
                    lldbFileCreated = true;
                }
            }

            string mainLogFile = Path.Join(iOSRunArguments.OutputDirectory, $"run-{target.AsString()}{(iOSRunArguments.DeviceName != null ? "-" + iOSRunArguments.DeviceName : null)}.log");

            IFileBackedLog mainLog = Log.CreateReadableAggregatedLog(
                logs.Create(mainLogFile, LogType.ExecutionLog.ToString(), true),
                new CallbackLog(message => logger.LogDebug(message.Trim())) { Timestamp = false });

            int verbosity = GetMlaunchVerbosity(iOSRunArguments.Verbosity);

            string? deviceName = iOSRunArguments.DeviceName;

            var appBundleInformationParser = new AppBundleInformationParser(ProcessManager);

            logger.LogInformation("Getting app bundle information..");

            AppBundleInformation appBundleInfo;

            try
            {
                appBundleInfo = await appBundleInformationParser.ParseFromAppBundle(
                    iOSRunArguments.AppPackagePath,
                    target.Platform,
                    mainLog,
                    cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError($"Failed to get bundle information: {e.Message}");
                return ExitCode.FAILED_TO_GET_BUNDLE_INFO;
            }

            ExitCode exitCode = ExitCode.SUCCESS;

            // Install app on the device
            if (!target.Platform.IsSimulator())
            {
                try
                {
                    (deviceName, exitCode) = await InstallApp(appBundleInfo, deviceName, logger, target, mainLog, cancellationToken);
                }
                catch (Exception e)
                {
                    var message = new StringBuilder()
                        .AppendLine("Application installation failed:")
                        .AppendLine(e.ToString());

                    logger.LogError(message.ToString());
                    return ExitCode.PACKAGE_INSTALLATION_FAILURE;
                }

                if (exitCode != ExitCode.SUCCESS)
                {
                    var message = new StringBuilder().Append("Application installation failed");

                    if (ErrorKnowledgeBase.IsKnownInstallIssue(mainLog, out var failureMessage))
                    {
                        message.AppendLine(":");
                        message.Append(failureMessage.Value.HumanMessage);
                        if (failureMessage.Value.IssueLink != null)
                        {
                            message.AppendLine($" Find more information at {failureMessage.Value.IssueLink}");
                        }
                    }

                    logger.LogError(message.ToString());

                    if (!string.IsNullOrEmpty(deviceName))
                    {
                        logger.LogInformation($"Cleaning up the failed installation from {deviceName}...");
                        await UninstallApp(appBundleInfo, deviceName, logger, mainLog, new CancellationToken());
                    }

                    return exitCode;
                }

                if (string.IsNullOrEmpty(deviceName))
                {
                    logger.LogError("Failed to get the name of the device where application was installed!");
                    return ExitCode.PACKAGE_INSTALLATION_FAILURE;
                }
            }

            // Run / test app
            try
            {
                logger.LogInformation($"Starting application '{appBundleInfo.AppName}' on " + (deviceName != null ? $"device '{deviceName}'" : target.AsString()));

                exitCode = await RunAppInternal(
                    appBundleInfo,
                    deviceName,
                    logger,
                    target,
                    logs,
                    mainLog,
                    cancellationToken);
            }
            catch (NoDeviceFoundException)
            {
                logger.LogError($"Failed to find suitable device for target {target.AsString()}" +
                    (target.Platform.IsSimulator() ? Environment.NewLine + "Please make sure suitable Simulator is installed in Xcode" : string.Empty));

                return ExitCode.DEVICE_NOT_FOUND;
            }
            catch (Exception e)
            {
                var message = new StringBuilder().AppendLine("Application run failed:");

                if (ErrorKnowledgeBase.IsKnownTestIssue(mainLog, out var failureMessage))
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

                logger.LogError(message.ToString());

                exitCode = ExitCode.APP_LAUNCH_FAILURE;
            }
            finally
            {
                mainLog.Dispose();

                if (!target.Platform.IsSimulator() && deviceName != null)
                {
                    await UninstallApp(appBundleInfo, deviceName, logger, mainLog, new CancellationToken());
                }

                if (lldbFileCreated)
                {
                    File.Delete(s_mlaunchLldbConfigFile);
                }
            }

            return exitCode;
        }

        private async Task<(string?, ExitCode)> InstallApp(
            AppBundleInformation appBundleInfo,
            string? deviceName,
            ILogger logger,
            TestTargetOs target,
            IFileBackedLog mainLog,
            CancellationToken cancellationToken)
        {
            logger.LogInformation($"Installing application '{appBundleInfo.AppName}' on " + (deviceName != null ? $" on device '{deviceName}'" : target.AsString()));

            var appInstaller = new AppInstaller(ProcessManager, DeviceLoader, mainLog, GetMlaunchVerbosity(iOSRunArguments.Verbosity));

            ProcessExecutionResult result;

            try
            {
                (deviceName, result) = await appInstaller.InstallApp(appBundleInfo, target, cancellationToken: cancellationToken);
            }
            catch (NoDeviceFoundException)
            {
                logger.LogError($"Failed to find suitable device for target {target.AsString()}" + Environment.NewLine +
                    "Please make sure the device is connected and unlocked.");
                return (null, ExitCode.DEVICE_NOT_FOUND);
            }
            catch (Exception e)
            {
                logger.LogError($"Failed to install the app bundle:{Environment.NewLine}{e}");
                return (null, ExitCode.PACKAGE_INSTALLATION_FAILURE);
            }

            if (!result.Succeeded)
            {
                // use the knowledge base class to decide if the error is known, if it is, let the user know
                // the failure reason
                if (ErrorKnowledgeBase.IsKnownInstallIssue(mainLog, out var errorMessage))
                {
                    var msg = $"Failed to install the app bundle (exit code={result.ExitCode}): {errorMessage.Value.HumanMessage}.";
                    if (errorMessage.Value.IssueLink != null)
                    {
                        msg += $" Find more information at {errorMessage.Value.IssueLink}";
                    }

                    logger.LogError(msg);
                }
                else
                {
                    logger.LogError($"Failed to install the app bundle (exit code={result.ExitCode})");
                }

                return (deviceName, ExitCode.PACKAGE_INSTALLATION_FAILURE);
            }

            logger.LogInformation($"Application '{appBundleInfo.AppName}' was installed successfully on device '{deviceName}'");

            return (deviceName, ExitCode.SUCCESS);
        }

        private async Task UninstallApp(
            AppBundleInformation appBundleInfo,
            string deviceName,
            ILogger logger,
            IFileBackedLog mainLog,
            CancellationToken cancellationToken)
        {
            logger.LogInformation($"Uninstalling the application '{appBundleInfo.AppName}' from device '{deviceName}'");

            var appUninstaller = new AppUninstaller(ProcessManager, mainLog, GetMlaunchVerbosity(iOSRunArguments.Verbosity));
            var uninstallResult = await appUninstaller.UninstallApp(deviceName, appBundleInfo.BundleIdentifier, cancellationToken);
            if (!uninstallResult.Succeeded)
            {
                logger.LogError($"Failed to uninstall the app bundle with exit code: {uninstallResult.ExitCode}");
            }
            else
            {
                logger.LogInformation($"Application '{appBundleInfo.AppName}' was uninstalled successfully");
            }
        }

        protected static int GetMlaunchVerbosity(LogLevel level) => level switch
        {
            LogLevel.Trace => 6,
            LogLevel.Debug => 5,
            LogLevel.Information => 4,
            LogLevel.Warning => 3,
            LogLevel.Error => 2,
            LogLevel.Critical => 1,
            LogLevel.None => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(level)),
        };
    }
}
