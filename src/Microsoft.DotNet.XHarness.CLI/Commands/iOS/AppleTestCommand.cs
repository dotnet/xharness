// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Apple;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Listeners;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Microsoft.DotNet.XHarness.iOS.Shared.XmlResults;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.iOS
{
    /// <summary>
    /// Command which executes a given, already-packaged iOS application, waits on it and returns status based on the outcome.
    /// </summary>
    internal class AppleTestCommand : AppleAppCommand
    {
        private const string CommandHelp = "Runs a given iOS/tvOS/watchOS/MacCatalyst test application bundle containing TestRunner in a target device/simulator";

        private readonly AppleTestCommandArguments _arguments = new AppleTestCommandArguments();

        protected override string CommandUsage { get; } = "apple test [OPTIONS] [-- [RUNTIME ARGUMENTS]]";
        protected override string CommandDescription { get; } = CommandHelp;
        protected override AppleAppRunArguments iOSRunArguments => _arguments;

        public AppleTestCommand() : base("test", false, CommandHelp)
        {
        }

        protected override async Task<ExitCode> RunAppInternal(
            AppBundleInformation appBundleInfo,
            string? deviceName,
            ILogger logger,
            TestTargetOs target,
            Logs logs,
            IFileBackedLog mainLog,
            CancellationToken cancellationToken)
        {
            var tunnelBore = (_arguments.CommunicationChannel == CommunicationChannel.UsbTunnel && !target.Platform.IsSimulator())
                ? new TunnelBore(ProcessManager)
                : null;

            // only add the extra callback if we do know that the feature was indeed enabled
            Action<string>? logCallback = IsLldbEnabled() ? (l) => NotifyUserLldbCommand(logger, l) : (Action<string>?)null;

            var appTester = new AppTester(
                ProcessManager,
                DeviceLoader,
                SimulatorLoader,
                new SimpleListenerFactory(tunnelBore),
                new CrashSnapshotReporterFactory(ProcessManager),
                new CaptureLogFactory(),
                new DeviceLogCapturerFactory(ProcessManager),
                new TestReporterFactory(ProcessManager),
                new XmlResultParser(),
                mainLog,
                logs,
                new Helpers(),
                logCallback: logCallback,
                appArguments: PassThroughArguments);

            string resultMessage;
            TestExecutingResult testResult;
            (deviceName, testResult, resultMessage) = await appTester.TestApp(appBundleInfo,
                target,
                _arguments.Timeout,
                _arguments.LaunchTimeout,
                deviceName,
                verbosity: GetMlaunchVerbosity(_arguments.Verbosity),
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
                        logger.LogError($"Application test run crashed. Check logs for more information");
                    }

                    return ExitCode.APP_CRASH;

                case TestExecutingResult.TimedOut:
                    logger.LogWarning($"Application test run timed out");

                    return ExitCode.TIMED_OUT;

                default:

                    if (resultMessage != null)
                    {
                        logger.LogError($"Application test run ended in an unexpected way: '{testResult}'{Environment.NewLine}" +
                            $"{resultMessage}{Environment.NewLine}{Environment.NewLine}" +
                            $"Check logs for more information.");
                    }
                    else
                    {
                        logger.LogError($"Application test run ended in an unexpected way: '{testResult}'. Check logs for more information");
                    }

                    return ExitCode.GENERAL_FAILURE;
            }
        }
    }
}
