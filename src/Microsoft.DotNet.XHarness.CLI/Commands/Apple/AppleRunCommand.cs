// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Apple;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Apple
{
    /// <summary>
    /// Command which executes a given, already-packaged iOS application, waits on it and returns status based on the outcome.
    /// </summary>
    internal class AppleRunCommand : AppleAppCommand<AppleRunCommandArguments>
    {
        private const string CommandHelp = "Installs, runs and uninstalls a given iOS/tvOS/watchOS/MacCatalyst application bundle " +
            "in a target device/simulator and tries to detect the exit code.";

        protected override AppleRunCommandArguments AppleAppArguments { get; } = new();
        protected override string CommandUsage { get; } = "apple run --app=... --output-directory=... --target=... [OPTIONS] [-- [RUNTIME ARGUMENTS]]";
        protected override string CommandDescription { get; } = CommandHelp;

        public AppleRunCommand() : base("run", false, CommandHelp)
        {
        }

        protected override async Task<ExitCode> InvokeInternal(
            IMlaunchProcessManager processManager,
            IAppBundleInformationParser appBundleInformationParser,
            DeviceFinder deviceFinder,
            Extensions.Logging.ILogger logger,
            TestTargetOs target,
            ILogs logs,
            IFileBackedLog mainLog,
            CancellationToken cancellationToken)
        {
            var orchestrator = new RunOrchestrator(
                processManager,
                deviceFinder,
                new ConsoleLogger(logger),
                logs,
                mainLog,
                ErrorKnowledgeBase,
                new Helpers());

            var args = AppleAppArguments;

            logger.LogInformation($"Getting app bundle information from '{args.AppPackagePath}'");
            var appBundleInfo = await appBundleInformationParser.ParseFromAppBundle(args.AppPackagePath, target.Platform, mainLog, cancellationToken);

            return await orchestrator.OrchestrateRun(
                appBundleInfo,
                target,
                args.DeviceName,
                args.Timeout,
                args.ExpectedExitCode,
                args.ResetSimulator,
                args.EnableLldb,
                args.EnvironmentalVariables,
                PassThroughArguments,
                cancellationToken);
        }
    }
}
