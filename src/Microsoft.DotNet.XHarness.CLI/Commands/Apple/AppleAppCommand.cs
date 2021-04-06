// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
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

        protected sealed override async Task<ExitCode> InvokeInternal(ILogger logger)
        {
            // We have to set these here because command arguments are not initialized in the ctor yet
            var processManager = new MlaunchProcessManager(AppleAppArguments.XcodeRoot, AppleAppArguments.MlaunchPath);
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
                string logFileName = $"run-{target.AsString()}{(AppleAppArguments.DeviceName != null ? "-" + AppleAppArguments.DeviceName : null)}.log";

                IFileBackedLog mainLog = Log.CreateReadableAggregatedLog(
                    logs.Create(logFileName, LogType.ExecutionLog.ToString(), true),
                    new CallbackLog(message => logger.LogDebug(message.Trim())) { Timestamp = false });

                using var orchestrator = GetOrchestrator(processManager, deviceFinder, logger, target, logs, mainLog, cts.Token);

                var exitCodeForRun = await orchestrator.OrchestrateRun(AppleAppArguments, PassThroughArguments, target, cts.Token);
                if (exitCodeForRun != ExitCode.SUCCESS)
                {
                    exitCode = exitCodeForRun;
                }
            }

            return exitCode;
        }

        protected abstract AppleBaseOrchestrator<TArguments> GetOrchestrator(
            IMlaunchProcessManager processManager,
            DeviceFinder deviceFinder,
            ILogger logger,
            TestTargetOs target,
            ILogs logs,
            IFileBackedLog mainLog,
            CancellationToken cancellationToken);
    }
}
