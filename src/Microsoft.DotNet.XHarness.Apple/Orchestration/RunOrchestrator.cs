// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
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
    public interface IRunOrchestrator
    {
        Task<ExitCode> OrchestrateRun(
            AppBundleInformation appBundleInformation,
            TestTargetOs target,
            string? deviceName,
            TimeSpan timeout,
            int expectedExitCode,
            bool resetSimulator,
            bool enableLldb,
            IReadOnlyCollection<(string, string)> environmentalVariables,
            IEnumerable<string> passthroughArguments,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// This orchestrator implements the `run` command flow.
    /// In this flow we spawn the application and do not expect TestRunner inside.
    /// We only try to detect the exit code after the app run is finished.
    /// </summary>
    public class RunOrchestrator : BaseOrchestrator, IRunOrchestrator
    {
        private readonly IMlaunchProcessManager _processManager;
        private readonly ILogger _logger;
        private readonly ILogs _logs;
        private readonly IFileBackedLog _mainLog;
        private readonly IErrorKnowledgeBase _errorKnowledgeBase;
        private readonly AppRunner _appRunner;

        public RunOrchestrator(
            IMlaunchProcessManager processManager,
            IDeviceFinder deviceFinder,
            ILogger consoleLogger,
            ILogs logs,
            IFileBackedLog mainLog,
            IErrorKnowledgeBase errorKnowledgeBase,
            IHelpers helpers)
            : base(processManager, deviceFinder, consoleLogger, logs, mainLog, errorKnowledgeBase, helpers)
        {
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            _logger = consoleLogger ?? throw new ArgumentNullException(nameof(consoleLogger));
            _logs = logs ?? throw new ArgumentNullException(nameof(logs));
            _mainLog = mainLog ?? throw new ArgumentNullException(nameof(mainLog));
            _errorKnowledgeBase = errorKnowledgeBase ?? throw new ArgumentNullException(nameof(errorKnowledgeBase));

            // Only add the extra callback if we do know that the feature was indeed enabled
            Action<string>? logCallback = IsLldbEnabled() ? (l) => NotifyUserLldbCommand(_logger, l) : null;

            _appRunner = new AppRunner(
                _processManager,
                new CrashSnapshotReporterFactory(_processManager),
                new CaptureLogFactory(),
                new DeviceLogCapturerFactory(_processManager),
                _mainLog,
                _logs,
                new Helpers(),
                logCallback);
        }

        public Task<ExitCode> OrchestrateRun(
            AppBundleInformation appBundleInformation,
            TestTargetOs target,
            string? deviceName,
            TimeSpan timeout,
            int expectedExitCode,
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
                    expectedExitCode,
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
                    expectedExitCode,
                    environmentalVariables,
                    passthroughArguments,
                    cancellationToken);

            return OrchestrateRun(
                target,
                deviceName,
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
            int expectedExitCode,
            IReadOnlyCollection<(string, string)> environmentalVariables,
            IEnumerable<string> passthroughArguments,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Starting application '{appBundleInfo.AppName}' on '{device.Name}'");

            ProcessExecutionResult result = await _appRunner.RunApp(
                appBundleInfo,
                target,
                device,
                companionDevice,
                timeout,
                passthroughArguments,
                environmentalVariables,
                cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogError($"App run has failed. mlaunch exited with {result.ExitCode}");
                return ExitCode.APP_LAUNCH_FAILURE;
            }

            return ParseResult(new iOSExitCodeDetector(), expectedExitCode, appBundleInfo, result);
        }

        private async Task<ExitCode> ExecuteMacCatalystApp(
            AppBundleInformation appBundleInfo,
            TimeSpan timeout,
            int expectedExitCode,
            IReadOnlyCollection<(string, string)> environmentalVariables,
            IEnumerable<string> passthroughArguments,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Starting '{appBundleInfo.AppName}' on MacCatalyst");

            ProcessExecutionResult result = await _appRunner.RunMacCatalystApp(
                appBundleInfo,
                timeout,
                passthroughArguments,
                environmentalVariables,
                cancellationToken: cancellationToken);

            return ParseResult(new MacCatalystExitCodeDetector(), expectedExitCode, appBundleInfo, result);
        }

        private ExitCode ParseResult(
            IExitCodeDetector exitCodeDetector,
            int expectedExitCode,
            AppBundleInformation appBundleInfo,
            ProcessExecutionResult result)
        {
            if (result.TimedOut)
            {
                _logger.LogError($"App run has timed out");
                return ExitCode.TIMED_OUT;
            }

            int exitCode;

            var systemLog = _logs.FirstOrDefault(log => log.Description == LogType.SystemLog.ToString());
            if (systemLog == null)
            {
                _logger.LogError("Application has finished but no system log found. Failed to determine the exit code!");
                return ExitCode.RETURN_CODE_NOT_SET;
            }

            exitCode = exitCodeDetector.DetectExitCode(appBundleInfo, systemLog);
            _logger.LogInformation($"App run ended with {exitCode}");

            if (expectedExitCode != exitCode)
            {
                _logger.LogError($"Application has finished with exit code {exitCode} but {expectedExitCode} was expected");

                if (_errorKnowledgeBase.IsKnownTestIssue(_mainLog, out var failureMessage))
                {
                    _logger.LogError(failureMessage.Value.HumanMessage);
                }

                return ExitCode.GENERAL_FAILURE;
            }

            _logger.LogInformation("Application has finished with exit code: " + exitCode +
                (expectedExitCode != 0 ? " (as expected)" : null));

            return ExitCode.SUCCESS;
        }
    }
}
