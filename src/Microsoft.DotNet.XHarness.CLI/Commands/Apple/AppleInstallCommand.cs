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
    internal class AppleInstallCommand : AppleAppCommand<AppleInstallCommandArguments>
    {
        private const string CommandHelp = "Installs a given iOS/tvOS/watchOS/MacCatalyst application bundle in a target device/simulator";

        protected override AppleInstallCommandArguments AppleAppArguments { get; } = new();

        protected override string CommandUsage { get; } = "apple install [OPTIONS]";
        protected override string CommandDescription { get; } = CommandHelp;

        public AppleInstallCommand() : base("install", false, CommandHelp)
        {
        }

        protected override Task<ExitCode> InvokeInternal(
            IMlaunchProcessManager processManager,
            DeviceFinder deviceFinder,
            Extensions.Logging.ILogger logger,
            TestTargetOs target,
            ILogs logs,
            IFileBackedLog mainLog,
            CancellationToken cancellationToken)
        {
            var orchestrator = new AppInstallOrchestrator(
                processManager,
                deviceFinder,
                new ConsoleLogger(logger),
                logs,
                mainLog,
                ErrorKnowledgeBase);

            var args = AppleAppArguments;

            return orchestrator.OrchestrateAppInstall(
                target,
                args.DeviceName,
                args.AppPackagePath,
                args.Timeout,
                args.ResetSimulator,
                args.EnableLldb,
                cancellationToken);
        }
    }
}
