﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
        protected override AppleJustRunCommandArguments AppleAppArguments { get; } = new();

        public AppleJustRunCommand() : base("just-run", false, CommandHelp)
        {
        }

        protected override Task<ExitCode> InvokeInternal(IServiceProvider serviceProvider, CancellationToken cancellationToken) =>
            serviceProvider.GetRequiredService<IJustRunOrchestrator>().OrchestrateRun(
                AppBundleInformation.FromBundleId(AppleAppArguments.BundleIdentifier),
                AppleAppArguments.Target,
                AppleAppArguments.DeviceName,
                AppleAppArguments.Timeout,
                AppleAppArguments.ExpectedExitCode,
                AppleAppArguments.ResetSimulator,
                AppleAppArguments.EnableLldb,
                AppleAppArguments.EnvironmentalVariables,
                PassThroughArguments,
                cancellationToken);
    }
}
