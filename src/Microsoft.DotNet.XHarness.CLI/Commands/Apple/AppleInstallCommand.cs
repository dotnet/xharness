// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using Microsoft.DotNet.XHarness.Apple;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Apple
{
    internal class AppleInstallCommand : AppleAppCommand<AppleInstallCommandArguments>
    {
        private const string CommandHelp = "Installs a given iOS/tvOS/watchOS/MacCatalyst application bundle in a target device/simulator";

        protected override AppleInstallCommandArguments AppleAppArguments { get; } = new();

        protected override string CommandUsage { get; } = "apple install [OPTIONS] [-- [RUNTIME ARGUMENTS]]";
        protected override string CommandDescription { get; } = CommandHelp;

        public AppleInstallCommand() : base("run", false, CommandHelp)
        {
        }

        protected override AppleOrchestrator<AppleInstallCommandArguments> GetOrchestrator(IMlaunchProcessManager processManager, DeviceFinder deviceFinder, ILogger logger, TestTargetOs target, ILogs logs, IFileBackedLog mainLog, CancellationToken cancellationToken) => throw new System.NotImplementedException();
    }
}
