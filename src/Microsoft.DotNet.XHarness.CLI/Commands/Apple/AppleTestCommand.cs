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
    internal class AppleTestCommand : AppleAppCommand
    {
        private const string CommandHelp = "Runs a given iOS/tvOS/watchOS/MacCatalyst test application bundle containing TestRunner in a target device/simulator";

        private readonly AppleTestCommandArguments _arguments = new();

        protected override string CommandUsage { get; } = "apple test [OPTIONS] [-- [RUNTIME ARGUMENTS]]";
        protected override string CommandDescription { get; } = CommandHelp;
        protected override AppleAppRunArguments iOSRunArguments => _arguments;

        public AppleTestCommand() : base("test", false, CommandHelp)
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
            var orchestrator = new AppleTestOrchestrator(processManager, deviceFinder, logger, logs, mainLog, ErrorKnowledgeBase);
            return orchestrator.Execute(_arguments, PassThroughArguments, target, cancellationToken);
        }
    }
}
