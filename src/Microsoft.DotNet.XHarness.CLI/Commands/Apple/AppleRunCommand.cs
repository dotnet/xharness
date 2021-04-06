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
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Apple
{
    /// <summary>
    /// Command which executes a given, already-packaged iOS application, waits on it and returns status based on the outcome.
    /// </summary>
    internal class AppleRunCommand : AppleAppCommand
    {
        private const string CommandHelp = "Runs a given iOS/tvOS/watchOS/MacCatalyst application bundle in a target device/simulator and tries to detect exit code (might not work reliably across iOS versions).";

        private readonly AppleRunCommandArguments _arguments = new();

        protected override AppleAppRunArguments iOSRunArguments => _arguments;
        protected override string CommandUsage { get; } = "apple run [OPTIONS] [-- [RUNTIME ARGUMENTS]]";
        protected override string CommandDescription { get; } = CommandHelp;

        public AppleRunCommand() : base("run", false, CommandHelp)
        {
        }

        protected override Task<ExitCode> InvokeInternal(
            IMlaunchProcessManager processManager,
            DeviceFinder deviceFinder,
            ILogger logger,
            TestTargetOs target,
            ILogs logs,
            IFileBackedLog mainLog,
            CancellationToken cancellationToken)
        {
            var orchestrator = new AppleRunOrchestrator(processManager, deviceFinder, logger, logs, mainLog, ErrorKnowledgeBase);
            return orchestrator.Execute(_arguments, PassThroughArguments, target, cancellationToken);
        }
    }
}
