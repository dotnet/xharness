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
    /// <summary>
    /// Command which executes a given, already-packaged iOS application, waits on it and returns status based on the outcome.
    /// </summary>
    internal class AppleTestCommand : AppleAppCommand<AppleTestCommandArguments>
    {
        private const string CommandHelp = "Runs a given iOS/tvOS/watchOS/MacCatalyst test application bundle containing TestRunner in a target device/simulator";

        protected override string CommandUsage { get; } = "apple test [OPTIONS] [-- [RUNTIME ARGUMENTS]]";
        protected override string CommandDescription { get; } = CommandHelp;
        protected override AppleTestCommandArguments AppleAppArguments { get; } = new();

        public AppleTestCommand() : base("test", false, CommandHelp)
        {
        }

        protected override AppleBaseOrchestrator<AppleTestCommandArguments> GetOrchestrator(
            IMlaunchProcessManager processManager,
            DeviceFinder deviceFinder,
            ILogger logger,
            TestTargetOs target,
            ILogs logs,
            IFileBackedLog mainLog,
            CancellationToken cancellationToken) =>

        new AppleTestOrchestrator(processManager, deviceFinder, logger, logs, mainLog, ErrorKnowledgeBase);
    }
}
