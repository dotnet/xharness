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
    internal class AppleTestCommand : AppleAppCommand<AppleTestCommandArguments>
    {
        private const string CommandHelp = "Installs, runs and uninstalls a given iOS/tvOS/watchOS/MacCatalyst test application bundle containing TestRunner " +
            "in a target device/simulator.";

        protected override string CommandUsage { get; } = "apple test --app=... --output-directory=... --target=... [OPTIONS] [-- [RUNTIME ARGUMENTS]]";
        protected override string CommandDescription { get; } = CommandHelp;
        protected override AppleTestCommandArguments AppleAppArguments { get; } = new();

        public AppleTestCommand() : base("test", false, CommandHelp)
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
            var orchestrator = new TestOrchestrator(
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

            return await orchestrator.OrchestrateTest(
                appBundleInfo,
                target,
                args.DeviceName,
                args.Timeout,
                args.LaunchTimeout,
                args.CommunicationChannel,
                args.XmlResultJargon,
                args.SingleMethodFilters,
                args.ClassMethodFilters,
                args.ResetSimulator,
                args.EnableLldb,
                args.EnvironmentalVariables,
                PassThroughArguments,
                cancellationToken);
        }
    }
}
