// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
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
        private readonly iOSTestCommandArguments _arguments = new iOSTestCommandArguments();
        private readonly ErrorKnowledgeBase _errorKnowledgeBase = new ErrorKnowledgeBase();

        protected override string CommandUsage { get; } = "ios test [OPTIONS]";
        protected override string CommandDescription { get; } = "Packaging command that will create a iOS/tvOS/watchOS or macOS application that can be used to run NUnit or XUnit-based test dlls";
        protected override TestCommandArguments TestArguments => _arguments;

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
            logger.LogInformation($"Starting test for {target.AsString()}{ (_arguments.DeviceName != null ? " targeting " + _arguments.DeviceName : null) }..");

            string mainLogFile = Path.Join(_arguments.OutputDirectory, $"run-{target}{(_arguments.DeviceName != null ? "-" + _arguments.DeviceName : null)}.log");
            ILog mainLog = logs.Create(mainLogFile, LogType.ExecutionLog.ToString(), true);
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
                    logger.LogError($"Failed to find suitable device for target {target.AsString()}");
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
                        logger.LogError($"Failed to install the app bundle (exit code={result.ExitCode}): {errorMessage}");
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

            var appRunner = new AppRunner(
                processManager,
                deviceLoader,
                simulatorLoader,
                new SimpleListenerFactory(tunnelBore),
                new CrashSnapshotReporterFactory(processManager),
                new CaptureLogFactory(),
                new DeviceLogCapturerFactory(processManager),
                new TestReporterFactory(processManager),
                mainLog,
                logs,
                new Helpers(),
                useXmlOutput: true); // the cli ALWAYS will get the output as xml

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
                    cancellationToken: cancellationToken);

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
                logger.LogError($"Failed to find suitable device for target {target.AsString()}");
                return ExitCode.DEVICE_NOT_FOUND;
            }
            catch (Exception e)
            {
                if (_errorKnowledgeBase.IsKnownTestIssue(mainLog, out var failureMessage))
                {
                    logger.LogError($"Application run failed:{Environment.NewLine}{failureMessage}");
                }
                else
                {
                    logger.LogError($"Application run failed:{Environment.NewLine}{e}");
                }

                return ExitCode.APP_CRASH;
            }
            finally
            {
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
