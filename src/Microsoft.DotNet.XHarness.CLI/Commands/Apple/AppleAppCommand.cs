// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Apple;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;
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

namespace Microsoft.DotNet.XHarness.CLI.Commands.Apple
{
    internal abstract class AppleAppCommand : XHarnessCommand
    {
        protected static readonly string s_mlaunchLldbConfigFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".mtouch-launch-with-lldb");

        protected readonly ErrorKnowledgeBase ErrorKnowledgeBase = new();
        protected override XHarnessCommandArguments Arguments => iOSRunArguments;
        protected abstract AppleAppRunArguments iOSRunArguments { get; }

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
            IDevice device,
            IDevice? companionDevice,
            ILogger logger,
            TestTargetOs target,
            Logs logs,
            IFileBackedLog mainLog,
            CancellationToken cancellationToken);

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

            IDevice device;
            IDevice? companionDevice;

            try
            {
                (device, companionDevice) = await FindDevice(target, iOSRunArguments.DeviceName, mainLog);
            }
            catch (NoDeviceFoundException e)
            {
                logger.LogError(e.Message);
                return ExitCode.DEVICE_NOT_FOUND;
            }

            try
            {
                exitCode = await InstallApp(appBundleInfo, device, logger, target, mainLog, cancellationToken);
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

                string? deviceName = device?.UDID ?? iOSRunArguments.DeviceName;
                if (!string.IsNullOrEmpty(deviceName))
                {
                    logger.LogInformation($"Cleaning up the failed installation from {iOSRunArguments.DeviceName}...");
                    await UninstallApp(appBundleInfo, deviceName, logger, mainLog, new CancellationToken());
                }

                return exitCode;
            }

            // Run / test app
            try
            {
                logger.LogInformation($"Starting application '{appBundleInfo.AppName}' on '{device.Name}'");

                exitCode = await RunAppInternal(appBundleInfo, device, companionDevice, logger, target, logs, mainLog, cancellationToken);
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

                string? deviceName = device?.UDID ?? iOSRunArguments.DeviceName;
                if (!target.Platform.IsSimulator() && !string.IsNullOrEmpty(deviceName))
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

        private async Task<(IDevice Device, IDevice? CompanionDevice)> FindDevice(TestTargetOs target, string? deviceName, ILog mainLog)
        {
            IDevice? device;
            IDevice? companionDevice = null;

            bool IsMatchingDevice(IDevice device) =>
                device.Name.Equals(deviceName, StringComparison.InvariantCultureIgnoreCase) ||
                device.UDID.Equals(deviceName, StringComparison.InvariantCultureIgnoreCase);

            if (target.Platform.IsSimulator())
            {
                if (deviceName == null)
                {
                    (device, companionDevice) = await SimulatorLoader.FindSimulators(target, mainLog, 3);
                }
                else
                {
                    await SimulatorLoader.LoadDevices(mainLog, false);

                    device = SimulatorLoader.AvailableDevices.FirstOrDefault(IsMatchingDevice)
                        ?? throw new NoDeviceFoundException($"Failed to find a simulator '{deviceName}'");
                }
            }
            else
            {
                // The DeviceLoader.FindDevice will return the fist device of the type, but we want to make sure that
                // the device we use is of the correct arch, therefore, we will use the LoadDevices and handpick one
                await DeviceLoader.LoadDevices(mainLog, false, false);

                if (deviceName == null)
                {

                    IHardwareDevice? hardwareDevice = target.Platform switch
                    {
                        TestTarget.Simulator_iOS32 => DeviceLoader.Connected32BitIOS.FirstOrDefault(),
                        TestTarget.Device_iOS => DeviceLoader.Connected64BitIOS.FirstOrDefault(),
                        TestTarget.Device_tvOS => DeviceLoader.ConnectedTV.FirstOrDefault(),
                        _ => throw new ArgumentOutOfRangeException(nameof(target), $"Unrecognized device platform {target.Platform}")
                    };

                    if (target.Platform.IsWatchOSTarget() && hardwareDevice != null)
                    {
                        companionDevice = await DeviceLoader.FindCompanionDevice(mainLog, hardwareDevice);
                    }

                    device = hardwareDevice;
                }
                else
                {
                    device = DeviceLoader.ConnectedDevices.FirstOrDefault(IsMatchingDevice)
                        ?? throw new NoDeviceFoundException($"Failed to find a device '{deviceName}'. " +
                                                            "Please make sure the device is connected and unlocked.");
                }
            }

            if (device == null)
            {
                throw new NoDeviceFoundException($"Failed to find a suitable device for target {target.AsString()}");
            }

            return (device, companionDevice);
        }

        private async Task<ExitCode> InstallApp(
            AppBundleInformation appBundleInfo,
            IDevice device,
            ILogger logger,
            TestTargetOs target,
            IFileBackedLog mainLog,
            CancellationToken cancellationToken)
        {
            logger.LogInformation($"Installing application '{appBundleInfo.AppName}' on '{device}'");

            var appInstaller = new AppInstaller(
                ProcessManager,
                mainLog,
                GetMlaunchVerbosity(iOSRunArguments.Verbosity));

            ProcessExecutionResult result;

            try
            {
                result = await appInstaller.InstallApp(appBundleInfo, target, device, cancellationToken: cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError($"Failed to install the app bundle:{Environment.NewLine}{e}");
                return ExitCode.PACKAGE_INSTALLATION_FAILURE;
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

                return ExitCode.PACKAGE_INSTALLATION_FAILURE;
            }

            logger.LogInformation($"Application '{appBundleInfo.AppName}' was installed successfully on device '{device}'");

            return ExitCode.SUCCESS;
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
