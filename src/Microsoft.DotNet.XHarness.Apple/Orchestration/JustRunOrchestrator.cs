// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;

namespace Microsoft.DotNet.XHarness.Apple
{
    /// <summary>
    /// This orchestrator implements the `just-run` command flow.
    /// In this flow we spawn the application and do not expect TestRunner inside.
    /// We only try to detect the exit code after the app run is finished.
    /// </summary>
    public class JustRunOrchestrator : RunOrchestrator
    {
        public JustRunOrchestrator(
            IMlaunchProcessManager processManager,
            IAppBundleInformationParser appBundleInformationParser,
            DeviceFinder deviceFinder,
            ILogger logger,
            ILogs logs,
            IFileBackedLog mainLog,
            IErrorKnowledgeBase errorKnowledgeBase)
            : base(processManager, appBundleInformationParser, deviceFinder, logger, logs, mainLog, errorKnowledgeBase)
        {
        }

        protected override Task CleanUpSimulators(IDevice device, IDevice? companionDevice)
            => Task.CompletedTask; // no-op so that we don't remove the app after (reset will only clean it up before)

        protected override Task<ExitCode> InstallApp(AppBundleInformation appBundleInfo, IDevice device, TestTargetOs target, CancellationToken cancellationToken)
            => Task.FromResult(ExitCode.SUCCESS); // no-op - we only want to run the app

        protected override Task UninstallApp(AppBundleInformation appBundleInfo, IDevice device, CancellationToken cancellationToken)
            => Task.CompletedTask; // no-op - we only want to run the app
    }
}
