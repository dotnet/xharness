// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

namespace Microsoft.DotNet.XHarness.Apple;

public interface IJustRunOrchestrator : IRunOrchestrator
{
}

/// <summary>
/// This orchestrator implements the `just-run` command flow.
/// In this flow we spawn the application and do not expect TestRunner inside.
/// We only try to detect the exit code after the app run is finished.
/// </summary>
public class JustRunOrchestrator : RunOrchestrator, IJustRunOrchestrator
{
    public JustRunOrchestrator(
        IAppInstaller appInstaller,
        IAppUninstaller appUninstaller,
        IAppRunnerFactory appRunnerFactory,
        IDeviceFinder deviceFinder,
        IiOSExitCodeDetector iOSExitCodeDetector,
        IMacCatalystExitCodeDetector macCatalystExitCodeDetector,
        ILogger consoleLogger,
        ILogs logs,
        IFileBackedLog mainLog,
        IErrorKnowledgeBase errorKnowledgeBase,
        IDiagnosticsData diagnosticsData,
        IHelpers helpers)
        : base(appInstaller, appUninstaller, appRunnerFactory, deviceFinder, iOSExitCodeDetector, macCatalystExitCodeDetector, consoleLogger, logs, mainLog, errorKnowledgeBase, diagnosticsData, helpers)
    {
    }

    public override Task<ExitCode> OrchestrateRun(
        AppBundleInformation appBundleInformation,
        TestTargetOs target,
        string? deviceName,
        TimeSpan timeout,
        TimeSpan launchTimeout,
        int expectedExitCode,
        bool includeWirelessDevices,
        bool resetSimulator,
        bool enableLldb,
        bool signalAppEnd,
        IReadOnlyCollection<(string, string)> environmentalVariables,
        IEnumerable<string> passthroughArguments,
        CancellationToken cancellationToken)
        => base.OrchestrateRun(
            appBundleInformation,
            target,
            deviceName,
            timeout,
            launchTimeout,
            expectedExitCode,
            includeWirelessDevices,
            resetSimulator: false, // No simulator reset for just- commands
            enableLldb,
            signalAppEnd,
            environmentalVariables,
            passthroughArguments,
            cancellationToken);

    protected override Task CleanUpSimulators(IDevice device, IDevice? companionDevice)
        => Task.CompletedTask; // no-op so that we don't remove the app after (reset will only clean it up before)

    protected override Task<ExitCode> InstallApp(AppBundleInformation appBundleInfo, IDevice device, TestTargetOs target, CancellationToken cancellationToken)
        => Task.FromResult(ExitCode.SUCCESS); // no-op - we only want to run the app

    protected override Task<ExitCode> UninstallApp(TestTarget target, string bundleIdentifier, IDevice device, bool isPreparation, CancellationToken cancellationToken)
        => Task.FromResult(ExitCode.SUCCESS); // no-op - we only want to run the app
}
