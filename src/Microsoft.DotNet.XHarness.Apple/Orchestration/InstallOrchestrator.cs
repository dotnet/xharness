// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

namespace Microsoft.DotNet.XHarness.Apple
{
    /// <summary>
    /// This orchestrator implements the `install` command flow.
    /// </summary>
    public class InstallOrchestrator : BaseOrchestrator
    {
        public InstallOrchestrator(
            IMlaunchProcessManager processManager,
            IAppBundleInformationParser appBundleInformationParser,
            DeviceFinder deviceFinder,
            ILogger consoleLogger,
            ILogs logs,
            IFileBackedLog mainLog,
            IErrorKnowledgeBase errorKnowledgeBase,
            IHelpers helpers)
            : base(processManager, appBundleInformationParser, deviceFinder, consoleLogger, logs, mainLog, errorKnowledgeBase, helpers)
        {
        }

        public Task<ExitCode> OrchestrateInstall(
            TestTargetOs target,
            string? deviceName,
            string appPackagePath,
            TimeSpan timeout,
            bool resetSimulator,
            bool enableLldb,
            CancellationToken cancellationToken)
        {
            Func<AppBundleInformation, Task<ExitCode>> executeMacCatalystApp = (appBundleInfo)
                => throw new InvalidOperationException("install command not available on maccatalyst");

            Func<AppBundleInformation, IDevice, IDevice?, Task<ExitCode>> executeApp = (appBundleInfo, device, companionDevice)
                => Task.FromResult(ExitCode.SUCCESS); // no-op

            return OrchestrateRun(
                target,
                deviceName,
                appPackagePath,
                resetSimulator,
                enableLldb,
                executeMacCatalystApp,
                executeApp,
                cancellationToken);
        }

        protected override Task CleanUpSimulators(IDevice device, IDevice? companionDevice)
            => Task.CompletedTask; // no-op so that we don't remove the app after (reset will only clean it up before)

        protected override Task UninstallApp(AppBundleInformation appBundleInfo, IDevice device, CancellationToken cancellationToken)
            => Task.CompletedTask; // no-op - we only want to install the app
    }
}
