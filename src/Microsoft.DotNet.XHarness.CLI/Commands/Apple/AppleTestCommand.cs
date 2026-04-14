// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Apple;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;
using Microsoft.DotNet.XHarness.CLI.Resources;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Apple;

/// <summary>
/// Command which executes a given, already-packaged iOS application, waits on it and returns status based on the outcome.
/// </summary>
internal class AppleTestCommand : AppleAppCommand<AppleTestCommandArguments>
{
    protected override string CommandUsage { get; } = Strings.Apple_Test_Usage;
    protected override string CommandDescription { get; } = Strings.Apple_Test_Description;
    protected override AppleTestCommandArguments Arguments { get; } = new();

    public AppleTestCommand(IServiceCollection services) : base("test", false, services, Strings.Apple_Test_Description)
    {
    }

    protected override Task<ExitCode> InvokeInternal(ServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<(string, string?)> envVars = Arguments.EnvironmentalVariables.Value;

        if (Arguments.EnableCoverage)
        {
            // Inject coverage env vars so the test runner on the device enables coverage.
            // Use Documents/coverage.cobertura.xml — the same directory where test results go,
            // which the orchestrator already knows how to pull from the app container.
            var coverageVars = new List<(string, string?)>(envVars)
            {
                ("NUNIT_ENABLE_COVERAGE", "true"),
                ("NUNIT_COVERAGE_OUTPUT_PATH", "coverage.cobertura.xml"),
            };
            envVars = coverageVars;
        }

        return serviceProvider.GetRequiredService<ITestOrchestrator>()
            .OrchestrateTest(
                Arguments.AppBundlePath,
                Arguments.Target,
                Arguments.DeviceName,
                Arguments.Timeout,
                Arguments.LaunchTimeout,
                Arguments.CommunicationChannel,
                Arguments.XmlResultJargon,
                Arguments.SingleMethodFilters.Value,
                Arguments.ClassMethodFilters.Value,
                includeWirelessDevices: Arguments.IncludeWireless,
                resetSimulator: Arguments.ResetSimulator,
                enableLldb: Arguments.EnableLldb,
                signalAppEnd: Arguments.SignalAppEnd,
                envVars,
                PassThroughArguments,
                cancellationToken);
    }
}
