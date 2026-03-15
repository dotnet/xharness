// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Apple;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Apple;

internal class AppleJustTestCommand : AppleAppCommand<AppleJustTestCommandArguments>
{
    private const string CommandHelp = "Runs an already installed iOS/tvOS/watchOS/xrOS/MacCatalyst test application containing a TestRunner in a target device/simulator.";

    protected override string CommandUsage { get; } = "apple just-test --app=... --output-directory=... --target=... [OPTIONS] [-- [RUNTIME ARGUMENTS]]";
    protected override string CommandDescription { get; } = CommandHelp;
    protected override AppleJustTestCommandArguments Arguments { get; } = new();

    public AppleJustTestCommand(IServiceCollection services) : base("just-test", false, services, CommandHelp)
    {
    }

    protected override Task<ExitCode> InvokeInternal(ServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var envVars = Arguments.EnvironmentalVariables.Value;

        if (Arguments.EnableCoverage)
        {
            var coverageVars = new List<(string, string)>(envVars)
            {
                ("NUNIT_ENABLE_COVERAGE", "true"),
                ("NUNIT_COVERAGE_OUTPUT_PATH", "coverage.cobertura.xml"),
            };
            envVars = coverageVars;
        }

        return serviceProvider.GetRequiredService<IJustTestOrchestrator>()
            .OrchestrateTest(
                Arguments.BundleIdentifier,
                Arguments.Target,
                Arguments.DeviceName,
                Arguments.Timeout,
                Arguments.LaunchTimeout,
                Arguments.CommunicationChannel,
                Arguments.XmlResultJargon,
                Arguments.SingleMethodFilters.Value,
                Arguments.ClassMethodFilters.Value,
                includeWirelessDevices: Arguments.IncludeWireless,
                enableLldb: Arguments.EnableLldb,
                signalAppEnd: Arguments.SignalAppEnd,
                envVars,
                PassThroughArguments,
                cancellationToken);
    }
}
