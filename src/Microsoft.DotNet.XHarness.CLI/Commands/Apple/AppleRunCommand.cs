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
using Microsoft.Extensions.DependencyInjection;
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

        public AppleRunCommand(IServiceCollection services) : base("run", false, services, CommandHelp)
        {
        }

        protected override async Task<ExitCode> InvokeInternal(CancellationToken cancellationToken)
        {
            var args = AppleAppArguments;

            var serviceProvider = Services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<Extensions.Logging.ILogger>();
            logger.LogInformation($"Getting app bundle information from '{args.AppPackagePath}'");

            var mainLog = serviceProvider.GetRequiredService<IFileBackedLog>();
            var appBundleInformationParser = serviceProvider.GetRequiredService<IAppBundleInformationParser>();
            var appBundleInfo = await appBundleInformationParser.ParseFromAppBundle(args.AppPackagePath, args.Target.Platform, mainLog, cancellationToken);

            var orchestrator = serviceProvider.GetRequiredService<IRunOrchestrator>();
            return await orchestrator.OrchestrateRun(
                appBundleInfo,
                args.Target,
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
