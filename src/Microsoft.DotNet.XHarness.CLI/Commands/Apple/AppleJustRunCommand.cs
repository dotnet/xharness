// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Apple;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Apple
{
    internal class AppleJustRunCommand : AppleAppCommand<AppleJustRunCommandArguments>
    {
        private const string CommandHelp = "Runs an already installed iOS/tvOS/watchOS/MacCatalyst test application containing a TestRunner " +
            "in a target device/simulator and tries to detect the exit code.";

        protected override string CommandUsage { get; } = "apple just-run --app=... --output-directory=... --target=... [OPTIONS] [-- [RUNTIME ARGUMENTS]]";
        protected override string CommandDescription { get; } = CommandHelp;
        protected override AppleJustRunCommandArguments Arguments { get; } = new();

        public AppleJustRunCommand(IServiceCollection services) : base("just-run", false, services, CommandHelp)
        {
        }

        protected override Task<ExitCode> InvokeInternal(ServiceProvider serviceProvider, CancellationToken cancellationToken) =>
            serviceProvider.GetRequiredService<IJustRunOrchestrator>()
                .OrchestrateRun(
                    AppBundleInformation.FromBundleId(Arguments.BundleIdentifier),
                    Arguments.Target,
                    Arguments.DeviceName,
                    Arguments.Timeout,
                    Arguments.ExpectedExitCode,
                    Arguments.IncludeWireless,
                    resetSimulator: false,
                    Arguments.EnableLldb,
                    Arguments.SignalAppEnd,
                    Arguments.EnvironmentalVariables.Value,
                    PassThroughArguments,
                    cancellationToken);
    }
}
