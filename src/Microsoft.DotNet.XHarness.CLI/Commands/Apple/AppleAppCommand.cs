// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Apple;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.Common.CLI.Commands;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Apple
{
    internal abstract class AppleAppCommand<TArguments> : XHarnessCommand where TArguments : AppleAppRunArguments
    {
        protected readonly ErrorKnowledgeBase ErrorKnowledgeBase = new();
        protected override XHarnessCommandArguments Arguments => AppleAppArguments;
        protected abstract TArguments AppleAppArguments { get; }

        protected AppleAppCommand(string name, bool allowsExtraArgs, string? help = null) : base(name, allowsExtraArgs, help)
        {
        }

        protected sealed override async Task<ExitCode> InvokeInternal(Extensions.Logging.ILogger logger)
        {
            // We have to set these here because command arguments are not initialized in the ctor yet
            var processManager = new MlaunchProcessManager(AppleAppArguments.XcodeRoot, AppleAppArguments.MlaunchPath);
            var appBundleInformationParser = new AppBundleInformationParser(processManager);
            var deviceLoader = new HardwareDeviceLoader(processManager);
            var simulatorLoader = new SimulatorLoader(processManager);
            var deviceFinder = new DeviceFinder(deviceLoader, simulatorLoader);

            var logs = new Logs(AppleAppArguments.OutputDirectory);

            var cts = new CancellationTokenSource();
            cts.CancelAfter(AppleAppArguments.Timeout);

            var exitCode = ExitCode.SUCCESS;

            foreach (var target in AppleAppArguments.RunTargets)
            {
                logger.LogInformation($"Preparing run for {target.AsString()}{ (AppleAppArguments.DeviceName != null ? " targeting " + AppleAppArguments.DeviceName : null) }");

                // Create main log file for the run
                string logFileName = $"{Name}-{target.AsString()}{(AppleAppArguments.DeviceName != null ? "-" + AppleAppArguments.DeviceName : null)}.log";

                IFileBackedLog mainLog = Log.CreateReadableAggregatedLog(
                    logs.Create(logFileName, LogType.ExecutionLog.ToString(), true),
                    new CallbackLog(message => logger.LogDebug(message.Trim())) { Timestamp = false });

                var exitCodeForRun = await InvokeInternal(processManager, appBundleInformationParser, deviceFinder, logger, target, logs, mainLog, cts.Token);
                if (exitCodeForRun != ExitCode.SUCCESS)
                {
                    exitCode = exitCodeForRun;
                }
            }

            return exitCode;
        }

        protected abstract Task<ExitCode> InvokeInternal(
            IMlaunchProcessManager processManager,
            IAppBundleInformationParser appBundleInformationParser,
            DeviceFinder deviceFinder,
            Extensions.Logging.ILogger logger,
            TestTargetOs target,
            ILogs logs,
            IFileBackedLog mainLog,
            CancellationToken cancellationToken);

        protected class ConsoleLogger : XHarness.Apple.ILogger
        {
            private readonly Extensions.Logging.ILogger _logger;

            public ConsoleLogger(Extensions.Logging.ILogger logger)
            {
                _logger = logger;
            }

            public void LogDebug(string message) => _logger.LogDebug(message);
            public void LogInformation(string message) => _logger.LogInformation(message);
            public void LogWarning(string message) => _logger.LogWarning(message);
            public void LogError(string message) => _logger.LogError(message);
            public void LogCritical(string message) => _logger.LogCritical(message);
        }
    }
}
