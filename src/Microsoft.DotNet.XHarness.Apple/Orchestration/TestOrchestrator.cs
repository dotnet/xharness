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
    /// <summary>
    /// Common ancestor for `test` and `just-test` orchestrators.
    /// </summary>
    public class TestOrchestrator : BaseOrchestrator
    {
        private readonly IMlaunchProcessManager _processManager;
        private readonly ILogger _logger;
        private readonly ILogs _logs;
        private readonly IFileBackedLog _mainLog;
        private readonly IErrorKnowledgeBase _errorKnowledgeBase;

        public TestOrchestrator(
            IMlaunchProcessManager processManager,
            IAppBundleInformationParser appBundleInformationParser,
            DeviceFinder deviceFinder,
            ILogger consoleLogger,
            ILogs logs,
            IFileBackedLog mainLog,
            IErrorKnowledgeBase errorKnowledgeBase)
            : base(processManager, appBundleInformationParser, deviceFinder, consoleLogger, mainLog, errorKnowledgeBase)
        {
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            _logger = consoleLogger ?? throw new ArgumentNullException(nameof(consoleLogger));
            _logs = logs ?? throw new ArgumentNullException(nameof(logs));
            _mainLog = mainLog ?? throw new ArgumentNullException(nameof(mainLog));
            _errorKnowledgeBase = errorKnowledgeBase ?? throw new ArgumentNullException(nameof(errorKnowledgeBase));
        }

        public Task<ExitCode> OrchestrateTest(
            TestTargetOs target,
            string? deviceName,
            string appPackagePath,
            TimeSpan timeout,
            TimeSpan launchTimeout,
            CommunicationChannel communicationChannel,
            XmlResultJargon xmlResultJargon,
            IEnumerable<string> singleMethodFilters,
            IEnumerable<string> classMethodFilters,
            bool resetSimulator,
            bool enableLldb,
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
                    cancellationToken);

            return OrchestrateRun(
                target,
                deviceName,
                appPackagePath,
                resetSimulator,
                enableLldb,
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
            CancellationToken cancellationToken)
        {
            AppTester appTester = GetAppTester(communicationChannel, target.Platform.IsSimulator());

            (TestExecutingResult testResult, string resultMessage) = await appTester.TestApp(
                appBundleInfo,
                target,
                device,
                companionDevice,
                timeout,
                launchTimeout,
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
            CancellationToken cancellationToken)
        {
            AppTester appTester = GetAppTester(communicationChannel, TestTarget.MacCatalyst.IsSimulator());

            (TestExecutingResult testResult, string resultMessage) = await appTester.TestMacCatalystApp(
                appBundleInfo,
                timeout,
                launchTimeout,
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
                if (_errorKnowledgeBase.IsKnownTestIssue(_mainLog, out var issue))
                {
                    _logger.LogError(message + newLine + issue.Value.HumanMessage);
                }
                else
                {
                    if (resultMessage != null)
                    {
                        _logger.LogError(message + newLine + resultMessage + newLine + newLine + checkLogsMessage);
                    }
                    else
                    {
                        _logger.LogError(message + newLine + checkLogsMessage);
                    }
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
                    LogProblem("Application run crashed");
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
