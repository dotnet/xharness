// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Apple;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.Extensions.DependencyInjection;
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

        protected override async Task<ExitCode> InvokeInternal(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        {
            var args = AppleAppArguments;

            var logger = serviceProvider.GetRequiredService<Extensions.Logging.ILogger>();
            logger.LogInformation($"Getting app bundle information from '{args.AppPackagePath}'");

            var mainLog = serviceProvider.GetRequiredService<IFileBackedLog>();
            var appBundleInformationParser = serviceProvider.GetRequiredService<IAppBundleInformationParser>();
            var appBundleInfo = await appBundleInformationParser.ParseFromAppBundle(args.AppPackagePath, args.Target.Platform, mainLog, cancellationToken);

            var orchestrator = serviceProvider.GetRequiredService<ITestOrchestrator>();

            return await orchestrator.OrchestrateTest(
                appBundleInfo,
                args.Target,
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
