// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.iOS;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution.Mlaunch;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Listeners;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.iOS
{
    /// <summary>
    /// Command which executes a given, already-packaged iOS application, waits on it and returns status based on the outcome.
    /// </summary>
    internal class iOSTestCommand : TestCommand
    {
        private const string CommandHelp = "Runs a given iOS/tvOS/watchOS application bundle in a target device/simulator";

        private readonly iOSTestCommandArguments _arguments = new iOSTestCommandArguments();
        private readonly ErrorKnowledgeBase _errorKnowledgeBase = new ErrorKnowledgeBase();
        private static readonly string _mlaunchLldbConfigFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".mtouch-launch-with-lldb");
        private bool _createdLldbFile;

        protected override string CommandUsage { get; } = "ios test [OPTIONS]";
        protected override string CommandDescription { get; } = CommandHelp;
        protected override TestCommandArguments TestArguments => _arguments;

        public iOSTestCommand() : base(CommandHelp)
        {
        }

        protected override async Task<ExitCode> InvokeInternal(ILogger logger)
        {
            var processManager = new MLaunchProcessManager(_arguments.XcodeRoot, _arguments.MlaunchPath);
            var deviceLoader = new HardwareDeviceLoader(processManager);
            var simulatorLoader = new SimulatorLoader(processManager);

            var logs = new Logs(_arguments.OutputDirectory);

            var cts = new CancellationTokenSource();
            cts.CancelAfter(_arguments.Timeout);

            var exitCode = ExitCode.SUCCESS;

            foreach (TestTarget target in _arguments.TestTargets)
            {
                var tunnelBore = (_arguments.CommunicationChannel == CommunicationChannel.UsbTunnel && !target.IsSimulator())
                    ? new TunnelBore(processManager)
                    : null;
                var exitCodeForRun = await RunTest(logger, target, logs, processManager, deviceLoader, simulatorLoader,
                    tunnelBore, cts.Token);

                if (exitCodeForRun != ExitCode.SUCCESS)
                {
                    exitCode = exitCodeForRun;
                }
            }

            return exitCode;
        }

        private static bool IsLldbEnabled() => File.Exists(_mlaunchLldbConfigFile);

        private void NotifyUserLldbCommand(ILogger logger, string line)
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

        private async Task<ExitCode> RunTest(
            ILogger logger,
            TestTarget target,
            Logs logs,
            MLaunchProcessManager processManager,
            IHardwareDeviceLoader deviceLoader,
            ISimulatorLoader simulatorLoader,
            ITunnelBore? tunnelBore,
            CancellationToken cancellationToken = default)
        {
            var isLldbEnabled = IsLldbEnabled();
            if (isLldbEnabled && !_arguments.EnableLldb)
            {
                // the file is present, but the user did not set it, warn him about it
                logger.LogWarning("Lldb will be used since '~/.mtouch-launch-with-lldb' was found in the system but it was not created by xharness.");
            }
            else if (_arguments.EnableLldb)
            {
                if (!File.Exists(_mlaunchLldbConfigFile))
                {
                    // create empty file
                    File.WriteAllText(_mlaunchLldbConfigFile, string.Empty);
                    _createdLldbFile = true;
                }
            }

            logger.LogInformation($"Starting test for {target.AsString()}{ (_arguments.DeviceName != null ? " targeting " + _arguments.DeviceName : null) }..");

            string mainLogFile = Path.Join(_arguments.OutputDirectory, $"run-{target}{(_arguments.DeviceName != null ? "-" + _arguments.DeviceName : null)}.log");

            IFileBackedLog mainLog = Log.CreateReadableAggregatedLog(
                logs.Create(mainLogFile, LogType.ExecutionLog.ToString(), true),
                new CallbackLog(message => logger.LogDebug(message.Trim())) { Timestamp = false });

            int verbosity = GetMlaunchVerbosity(_arguments.Verbosity);

            string? deviceName = _arguments.DeviceName;

            var appBundleInformationParser = new AppBundleInformationParser(processManager);

            AppBundleInformation appBundleInfo;

            try
            {
                appBundleInfo = await appBundleInformationParser.ParseFromAppBundle(_arguments.AppPackagePath, target, mainLog, cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError($"Failed to get bundle information: {e.Message}");
                return ExitCode.FAILED_TO_GET_BUNDLE_INFO;
            }

            if (!target.IsSimulator())
            {
                logger.LogInformation($"Installing application '{appBundleInfo.AppName}' on " + (deviceName != null ? $" on device '{deviceName}'" : target.AsString()));

                var appInstaller = new AppInstaller(processManager, deviceLoader, mainLog, verbosity);

                ProcessExecutionResult result;

                try
                {
                    (deviceName, result) = await appInstaller.InstallApp(appBundleInfo, target, cancellationToken: cancellationToken);
                }
                catch (NoDeviceFoundException)
                {
                    logger.LogError($"Failed to find suitable device for target {target.AsString()}" + Environment.NewLine +
                        "Please make sure the device is connected and unlocked.");
                    return ExitCode.DEVICE_NOT_FOUND;
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
                    if (_errorKnowledgeBase.IsKnownInstallIssue(mainLog, out var errorMessage))
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

                logger.LogInformation($"Application '{appBundleInfo.AppName}' was installed successfully on device '{deviceName}'");
            }

            logger.LogInformation($"Starting application '{appBundleInfo.AppName}' on " + (deviceName != null ? $"device '{deviceName}'" : target.AsString()));

            // only add the extra callback if we do know that the feature was indeed enabled
            Action<string>? logCallback = isLldbEnabled ? (l) => NotifyUserLldbCommand(logger, l) : (Action<string>?)null;

            var appRunner = new AppRunner(
                processManager,
                deviceLoader,
                simulatorLoader,
                new SimpleListenerFactory(tunnelBore),
                new CrashSnapshotReporterFactory(processManager),
                new CaptureLogFactory(),
                new DeviceLogCapturerFactory(processManager),
                new TestReporterFactory(processManager),
                new XmlResultParser(),
                mainLog,
                logs,
                new Helpers(),
                logCallback: logCallback);

            try
            {
                string resultMessage;
                TestExecutingResult testResult;
                (deviceName, testResult, resultMessage) = await appRunner.RunApp(appBundleInfo,
                    target,
                    _arguments.Timeout,
                    _arguments.LaunchTimeout,
                    deviceName,
                    verbosity: verbosity,
                    xmlResultJargon: _arguments.XmlResultJargon,
                    cancellationToken: cancellationToken,
                    skippedMethods: _arguments.SingleMethodFilters?.ToArray(),
                    skippedTestClasses: _arguments.ClassMethodFilters?.ToArray());

                switch (testResult)
                {
                    case TestExecutingResult.Succeeded:
                        logger.LogInformation($"Application finished the test run successfully");
                        logger.LogInformation(resultMessage);

                        return ExitCode.SUCCESS;

                    case TestExecutingResult.Failed:
                        logger.LogInformation($"Application finished the test run successfully with some failed tests");
                        logger.LogInformation(resultMessage);

                        return ExitCode.TESTS_FAILED;

                    case TestExecutingResult.LaunchFailure:

                        if (resultMessage != null)
                        {
                            logger.LogError($"Failed to launch the application:{Environment.NewLine}" +
                                $"{resultMessage}{Environment.NewLine}{Environment.NewLine}" +
                                $"Check logs for more information.");
                        }
                        else
                        {
                            logger.LogError($"Failed to launch the application. Check logs for more information");
                        }

                        return ExitCode.APP_LAUNCH_FAILURE;

                    case TestExecutingResult.Crashed:

                        if (resultMessage != null)
                        {
                            logger.LogError($"Application run crashed:{Environment.NewLine}" +
                                $"{resultMessage}{Environment.NewLine}{Environment.NewLine}" +
                                $"Check logs for more information.");
                        }
                        else
                        {
                            logger.LogError($"Application run crashed. Check logs for more information");
                        }

                        return ExitCode.APP_CRASH;

                    case TestExecutingResult.TimedOut:
                        logger.LogWarning($"Application run timed out");

                        return ExitCode.TIMED_OUT;

                    default:

                        if (resultMessage != null)
                        {
                            logger.LogError($"Application run ended in an unexpected way: '{testResult}'{Environment.NewLine}" +
                                $"{resultMessage}{Environment.NewLine}{Environment.NewLine}" +
                                $"Check logs for more information.");
                        }
                        else
                        {
                            logger.LogError($"Application run ended in an unexpected way: '{testResult}'. Check logs for more information");
                        }

                        return ExitCode.GENERAL_FAILURE;
                }
            }
            catch (NoDeviceFoundException)
            {
                logger.LogError($"Failed to find suitable device for target {target.AsString()}" +
                    (target.IsSimulator() ? Environment.NewLine + "Please make sure suitable Simulator is installed in Xcode" : string.Empty));

                return ExitCode.DEVICE_NOT_FOUND;
            }
            catch (Exception e)
            {
                if (_errorKnowledgeBase.IsKnownTestIssue(mainLog, out var failureMessage))
                {
                    var msg = $"Application run failed:{Environment.NewLine}{failureMessage.Value.HumanMessage}.";
                    if (failureMessage.Value.IssueLink != null)
                    {
                        msg += $" Find more information at {failureMessage.Value.IssueLink}";
                    }

                    logger.LogError(msg);
                }
                else
                {
                    logger.LogError($"Application run failed:{Environment.NewLine}{e}");
                }

                return ExitCode.APP_CRASH;
            }
            finally
            {
                mainLog.Dispose();

                if (!target.IsSimulator() && deviceName != null)
                {
                    logger.LogInformation($"Uninstalling the application '{appBundleInfo.AppName}' from device '{deviceName}'");

                    var appUninstaller = new AppUninstaller(processManager, mainLog, verbosity);
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

                if (_createdLldbFile) // clean after the setting
                {
                    File.Delete(_mlaunchLldbConfigFile);
                }
            }
        }

        private static int GetMlaunchVerbosity(LogLevel level) => level switch
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
