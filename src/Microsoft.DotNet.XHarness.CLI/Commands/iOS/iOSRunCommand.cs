// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.iOS;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.iOS
{
    /// <summary>
    /// Command which executes a given, already-packaged iOS application, waits on it and returns status based on the outcome.
    /// </summary>
    internal class iOSRunCommand : iOSAppCommand
    {
        private const string CommandHelp = "Runs a given iOS/tvOS/watchOS application bundle in a target device/simulator and tries to detect exit code (might not work reliably across iOS versions).";

        private readonly iOSRunCommandArguments _arguments = new iOSRunCommandArguments();

        protected override iOSRunCommandArguments iOSRunArguments => _arguments;
        protected override string CommandUsage { get; } = "ios run [OPTIONS] [-- [RUNTIME ARGUMENTS]]";
        protected override string CommandDescription { get; } = CommandHelp;

        public iOSRunCommand() : base("run", false, CommandHelp)
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
            // only add the extra callback if we do know that the feature was indeed enabled
            Action<string>? logCallback = IsLldbEnabled() ? (l) => NotifyUserLldbCommand(logger, l) : (Action<string>?)null;

            var appRunner = new AppRunner(
                ProcessManager,
                DeviceLoader,
                SimulatorLoader,
                new CrashSnapshotReporterFactory(ProcessManager),
                new CaptureLogFactory(),
                new DeviceLogCapturerFactory(ProcessManager),
                new ExitCodeDetector(),
                mainLog,
                logs,
                new Helpers(),
                PassThroughArguments,
                logCallback);

            int? exitCode = null;
            (deviceName, exitCode) = await appRunner.RunApp(
                appBundleInfo,
                target,
                _arguments.Timeout,
                deviceName,
                verbosity: GetMlaunchVerbosity(_arguments.Verbosity),
                cancellationToken: cancellationToken);

            if (exitCode.HasValue)
            {
                logger.LogInformation("Application has finished with exit code: " + exitCode);
                return (ExitCode)exitCode.Value;
            }
            else
            {
                logger.LogError("Application has finished but no system log found. Failed to determine the exit code!");
                return ExitCode.RETURN_CODE_NOT_SET;
            }
        }
    }
}
