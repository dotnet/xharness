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

namespace Microsoft.DotNet.XHarness.CLI.Commands.Apple
{
    internal class AppleJustTestCommand : AppleAppCommand<AppleJustTestCommandArguments>
    {
        private const string CommandHelp = "Runs an already installed iOS/tvOS/watchOS/MacCatalyst test application containing a TestRunner in a target device/simulator.";

        protected override string CommandUsage { get; } = "apple just-test --app=... --output-directory=... --target=... [OPTIONS] [-- [RUNTIME ARGUMENTS]]";
        protected override string CommandDescription { get; } = CommandHelp;
        protected override AppleJustTestCommandArguments AppleAppArguments { get; } = new();

        public AppleJustTestCommand() : base("just-test", false, CommandHelp)
        {
        }

        protected override Task<ExitCode> InvokeInternal(
            IMlaunchProcessManager processManager,
            IAppBundleInformationParser appBundleInformationParser,
            DeviceFinder deviceFinder,
            Extensions.Logging.ILogger logger,
            TestTargetOs target,
            ILogs logs,
            IFileBackedLog mainLog,
            CancellationToken cancellationToken)
        {
            var args = AppleAppArguments;

            var orchestrator = new JustTestOrchestrator(
                processManager,
                deviceFinder,
                new ConsoleLogger(logger),
                logs,
                mainLog,
                ErrorKnowledgeBase,
                new Helpers());

            return orchestrator.OrchestrateTest(
                AppBundleInformation.FromBundleId(args.BundleIdentifier),
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
