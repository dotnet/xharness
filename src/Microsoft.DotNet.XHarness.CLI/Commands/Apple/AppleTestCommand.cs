// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Apple;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;
using Microsoft.DotNet.XHarness.Common.Resources;
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

    protected override Task<ExitCode> InvokeInternal(ServiceProvider serviceProvider, CancellationToken cancellationToken) =>
        serviceProvider.GetRequiredService<ITestOrchestrator>()
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
                Arguments.EnvironmentalVariables.Value,
                PassThroughArguments,
                cancellationToken);
}
