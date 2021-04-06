// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Listeners;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Microsoft.DotNet.XHarness.iOS.Shared.XmlResults;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.Apple
{
    internal class AppleTestOrchestrator : AppleBaseOrchestrator<AppleTestCommandArguments>
    {
        private readonly IMlaunchProcessManager _processManager;
        private readonly ILogger _logger;
        private readonly ILogs _logs;
        private readonly IFileBackedLog _mainLog;
        private readonly IErrorKnowledgeBase _errorKnowledgeBase;

        public AppleTestOrchestrator(
            IMlaunchProcessManager processManager,
            DeviceFinder deviceFinder,
            ILogger consoleLogger,
            ILogs logs,
            IFileBackedLog mainLog,
            IErrorKnowledgeBase errorKnowledgeBase) : base(processManager, deviceFinder, consoleLogger, mainLog, errorKnowledgeBase)
        {
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            _logger = consoleLogger ?? throw new ArgumentNullException(nameof(consoleLogger));
            _logs = logs ?? throw new ArgumentNullException(nameof(logs));
            _mainLog = mainLog ?? throw new ArgumentNullException(nameof(mainLog));
            _errorKnowledgeBase = errorKnowledgeBase ?? throw new ArgumentNullException(nameof(errorKnowledgeBase));
        }

        protected override async Task<ExitCode> ExecuteApp(
            AppleTestCommandArguments arguments,
            IEnumerable<string> passthroughArguments,
            AppBundleInformation appBundleInfo,
            IDevice device,
            IDevice? companionDevice,
            TestTargetOs target,
            CancellationToken cancellationToken)
        {
            AppTester appTester = GetAppTester(arguments.CommunicationChannel, target.Platform.IsSimulator());

            (TestExecutingResult testResult, string resultMessage) = await appTester.TestApp(
                appBundleInfo,
                target,
                device,
                companionDevice,
                arguments.Timeout,
                arguments.LaunchTimeout,
                passthroughArguments,
                arguments.EnvironmentalVariables,
                arguments.XmlResultJargon,
                skippedMethods: arguments.SingleMethodFilters?.ToArray(),
                skippedTestClasses: arguments.ClassMethodFilters?.ToArray(),
                cancellationToken: cancellationToken);

            return ParseResult(testResult, resultMessage);
        }

        protected override async Task<ExitCode> ExecuteMacCatalystApp(
            AppleTestCommandArguments arguments,
            IEnumerable<string> passthroughArguments,
            AppBundleInformation appBundleInfo,
            CancellationToken cancellationToken)
        {
            AppTester appTester = GetAppTester(arguments.CommunicationChannel, TestTarget.MacCatalyst.IsSimulator());

            (TestExecutingResult testResult, string resultMessage) = await appTester.TestMacCatalystApp(
                appBundleInfo,
                arguments.Timeout,
                arguments.LaunchTimeout,
                passthroughArguments,
                arguments.EnvironmentalVariables,
                arguments.XmlResultJargon,
                skippedMethods: arguments.SingleMethodFilters?.ToArray(),
                skippedTestClasses: arguments.ClassMethodFilters?.ToArray(),
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
