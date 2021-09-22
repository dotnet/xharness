// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Listeners;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Microsoft.DotNet.XHarness.iOS.Shared.XmlResults;

namespace Microsoft.DotNet.XHarness.Apple
{
    public interface ITestOrchestrator
    {
        Task<ExitCode> OrchestrateTest(
            AppBundleInformation appBundleInformation,
            TestTargetOs target,
            string? deviceName,
            TimeSpan timeout,
            TimeSpan launchTimeout,
            CommunicationChannel communicationChannel,
            XmlResultJargon xmlResultJargon,
            IEnumerable<string> singleMethodFilters,
            IEnumerable<string> classMethodFilters,
            bool includeWirelessDevices,
            bool resetSimulator,
            bool enableLldb,
            bool signalAppEnd,
            IReadOnlyCollection<(string, string)> environmentalVariables,
            IEnumerable<string> passthroughArguments,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Common ancestor for `test` and `just-test` orchestrators.
    /// </summary>
    public class TestOrchestrator : BaseOrchestrator, ITestOrchestrator
    {
        private readonly IMlaunchProcessManager _processManager;
        private readonly ILogger _logger;
        private readonly ILogs _logs;
        private readonly IFileBackedLog _mainLog;
        private readonly IErrorKnowledgeBase _errorKnowledgeBase;

        public TestOrchestrator(
            IMlaunchProcessManager processManager,
            IDeviceFinder deviceFinder,
            ILogger consoleLogger,
            ILogs logs,
            IFileBackedLog mainLog,
            IErrorKnowledgeBase errorKnowledgeBase,
            IDiagnosticsData diagnosticsData,
            IHelpers helpers)
            : base(processManager, deviceFinder, consoleLogger, logs, mainLog, errorKnowledgeBase, diagnosticsData, helpers)
        {
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            _logger = consoleLogger ?? throw new ArgumentNullException(nameof(consoleLogger));
            _logs = logs ?? throw new ArgumentNullException(nameof(logs));
            _mainLog = mainLog ?? throw new ArgumentNullException(nameof(mainLog));
            _errorKnowledgeBase = errorKnowledgeBase ?? throw new ArgumentNullException(nameof(errorKnowledgeBase));
        }

        public Task<ExitCode> OrchestrateTest(
            AppBundleInformation appBundleInformation,
            TestTargetOs target,
            string? deviceName,
            TimeSpan timeout,
            TimeSpan launchTimeout,
            CommunicationChannel communicationChannel,
            XmlResultJargon xmlResultJargon,
            IEnumerable<string> singleMethodFilters,
            IEnumerable<string> classMethodFilters,
            bool includeWirelessDevices,
            bool resetSimulator,
            bool enableLldb,
            bool signalAppEnd,
            IReadOnlyCollection<(string, string)> environmentalVariables,
            IEnumerable<string> passthroughArguments,
            CancellationToken cancellationToken)
        {
            Func<AppBundleInformation, Task<ExitCode>> executeMacCatalystApp = (appBundleInfo) =>
                ExecuteMacCatalystApp(
                    appBundleInfo,
                    timeout,
                    launchTimeout,
                    communicationChannel,
                    xmlResultJargon,
                    singleMethodFilters,
                    classMethodFilters,
                    environmentalVariables,
                    passthroughArguments,
                    signalAppEnd,
                    cancellationToken);

            Func<AppBundleInformation, IDevice, IDevice?, Task<ExitCode>> executeApp = (appBundleInfo, device, companionDevice) =>
                ExecuteApp(
                    appBundleInfo,
                    target,
                    device,
                    companionDevice,
                    timeout,
                    launchTimeout,
                    communicationChannel,
                    xmlResultJargon,
                    singleMethodFilters,
                    classMethodFilters,
                    environmentalVariables,
                    passthroughArguments,
                    signalAppEnd,
                    cancellationToken);

            return OrchestrateRun(
                target,
                deviceName,
                includeWirelessDevices,
                resetSimulator,
                enableLldb,
                appBundleInformation,
                executeMacCatalystApp,
                executeApp,
                cancellationToken);
        }

        private async Task<ExitCode> ExecuteApp(
            AppBundleInformation appBundleInfo,
            TestTargetOs target,
            IDevice device,
            IDevice? companionDevice,
            TimeSpan timeout,
            TimeSpan launchTimeout,
            CommunicationChannel communicationChannel,
            XmlResultJargon xmlResultJargon,
            IEnumerable<string> singleMethodFilters,
            IEnumerable<string> classMethodFilters,
            IReadOnlyCollection<(string, string)> environmentalVariables,
            IEnumerable<string> passthroughArguments,
            bool signalAppEnd,
            CancellationToken cancellationToken)
        {
            var runMode = target.Platform.ToRunMode();

            // iOS 14+ devices do not allow local network access and won't work unless the user confirms a dialog on the screen
            // https://developer.apple.com/forums/thread/663858
            if (Version.TryParse(device.OSVersion, out var version) && version.Major >= 14 && runMode == RunMode.iOS && communicationChannel == CommunicationChannel.Network)
            {
                _logger.LogWarning(
                    "Applications need user permission for communication over local network on iOS 14 and newer." + Environment.NewLine +
                    "Either confirm a dialog on the device after the application launches or use the USB tunnel communication channel." + Environment.NewLine +
                    "Test run might fail if permission is not granted. Permission is valid until app is uninstalled.");
            }

            if (signalAppEnd && (runMode == RunMode.Sim64 || runMode == RunMode.Sim32))
            {
                _logger.LogWarning("The --signal-app-end option is used for device tests and has no effect on simulators");
            }

            _logger.LogInformation("Starting test run for " + appBundleInfo.BundleIdentifier + "..");

            AppTester appTester = GetAppTester(communicationChannel, target.Platform.IsSimulator());

            (TestExecutingResult testResult, string resultMessage) = await appTester.TestApp(
                appBundleInfo,
                target,
                device,
                companionDevice,
                timeout,
                launchTimeout,
                signalAppEnd,
                passthroughArguments,
                environmentalVariables,
                xmlResultJargon,
                skippedMethods: singleMethodFilters?.ToArray(),
                skippedTestClasses: classMethodFilters?.ToArray(),
                cancellationToken: cancellationToken);

            return ParseResult(testResult, resultMessage);
        }

        private async Task<ExitCode> ExecuteMacCatalystApp(
            AppBundleInformation appBundleInfo,
            TimeSpan timeout,
            TimeSpan launchTimeout,
            CommunicationChannel communicationChannel,
            XmlResultJargon xmlResultJargon,
            IEnumerable<string> singleMethodFilters,
            IEnumerable<string> classMethodFilters,
            IReadOnlyCollection<(string, string)> environmentalVariables,
            IEnumerable<string> passthroughArguments,
            bool signalAppEnd,
            CancellationToken cancellationToken)
        {
            AppTester appTester = GetAppTester(communicationChannel, TestTarget.MacCatalyst.IsSimulator());

            (TestExecutingResult testResult, string resultMessage) = await appTester.TestMacCatalystApp(
                appBundleInfo,
                timeout,
                launchTimeout,
                signalAppEnd,
                passthroughArguments,
                environmentalVariables,
                xmlResultJargon,
                skippedMethods: singleMethodFilters?.ToArray(),
                skippedTestClasses: classMethodFilters?.ToArray(),
                cancellationToken: cancellationToken);

            return ParseResult(testResult, resultMessage);
        }

        private AppTester GetAppTester(CommunicationChannel communicationChannel, bool isSimulator)
        {
            var tunnelBore = (communicationChannel == CommunicationChannel.UsbTunnel && !isSimulator)
                ? new TunnelBore(_processManager)
                : null;

            // Only add the extra callback if we do know that the feature was indeed enabled
            Action<string>? logCallback = IsLldbEnabled() ? (l) => NotifyUserLldbCommand(_logger, l) : null;

            return new AppTester(
                _processManager,
                new SimpleListenerFactory(tunnelBore),
                new CrashSnapshotReporterFactory(_processManager),
                new CaptureLogFactory(),
                new DeviceLogCapturerFactory(_processManager),
                new TestReporterFactory(_processManager),
                new XmlResultParser(),
                _mainLog,
                _logs,
                new Helpers(),
                logCallback: logCallback);
        }

        private ExitCode ParseResult(TestExecutingResult testResult, string resultMessage)
        {
            string newLine = Environment.NewLine;
            const string checkLogsMessage = "Check logs for more information";

            void LogProblem(string message)
            {
                foreach (var log in _logs)
                {
                    if (_errorKnowledgeBase.IsKnownTestIssue(log, out var issue))
                    {
                        _logger.LogError(message + newLine + issue.Value.HumanMessage);
                        return;
                    }
                }

                if (resultMessage != null)
                {
                    _logger.LogError(message + newLine + resultMessage + newLine + newLine + checkLogsMessage);
                }
                else
                {
                    _logger.LogError(message + newLine + checkLogsMessage);
                }
            }

            switch (testResult)
            {
                case TestExecutingResult.Succeeded:
                    _logger.LogInformation($"Application finished the test run successfully");
                    _logger.LogInformation(resultMessage);
                    return ExitCode.SUCCESS;

                case TestExecutingResult.Failed:
                    _logger.LogInformation($"Application finished the test run successfully with some failed tests");
                    _logger.LogInformation(resultMessage);
                    return ExitCode.TESTS_FAILED;

                case TestExecutingResult.LaunchFailure:
                    LogProblem("Failed to launch the application");
                    return ExitCode.APP_LAUNCH_FAILURE;

                case TestExecutingResult.Crashed:
                    LogProblem("Application test run crashed");
                    return ExitCode.APP_CRASH;

                case TestExecutingResult.TimedOut:
                    _logger.LogWarning($"Application test run timed out");
                    return ExitCode.TIMED_OUT;

                default:
                    _logger.LogError($"Application test run ended in an unexpected way: '{testResult}'" +
                        newLine + (resultMessage != null ? resultMessage + newLine + newLine : null) + checkLogsMessage);
                    return ExitCode.GENERAL_FAILURE;
            }
        }
    }
}
