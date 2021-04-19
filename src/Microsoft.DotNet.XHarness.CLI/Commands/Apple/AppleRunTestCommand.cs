﻿// Licensed to the .NET Foundation under one or more agreements.
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

namespace Microsoft.DotNet.XHarness.CLI.Commands.Apple
{
    internal class AppleRunTestCommand : AppleAppCommand<AppleTestCommandArguments>
    {
        private const string CommandHelp = "Runs a specific test inside of a given iOS/tvOS/watchOS/MacCatalyst test application bundle containing TestRunner already installed in a target device/simulator";

        protected override string CommandUsage { get; } = "apple run-test --app=... --output-directory=... --targets=... [OPTIONS] [-- [RUNTIME ARGUMENTS]]";
        protected override string CommandDescription { get; } = CommandHelp;
        protected override AppleTestCommandArguments AppleAppArguments { get; } = new();

        public AppleRunTestCommand() : base("run-test", false, CommandHelp)
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
            var orchestrator = new AppRunTestOrchestrator(
                processManager,
                appBundleInformationParser,
                deviceFinder,
                new ConsoleLogger(logger),
                logs,
                mainLog,
                ErrorKnowledgeBase);

            var args = AppleAppArguments;

            return orchestrator.OrchestrateAppRunTest(
                target,
                args.DeviceName,
                args.AppPackagePath,
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
