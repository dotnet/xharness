// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
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
    /// This orchestrator implements the `uninstall` command flow.
    /// </summary>
    public class AppUninstallOrchestrator : BaseOrchestrator
    {
        public AppUninstallOrchestrator(
            IMlaunchProcessManager processManager,
            IAppBundleInformationParser appBundleInformationParser,
            DeviceFinder deviceFinder,
            ILogger consoleLogger,
            ILogs logs,
            IFileBackedLog mainLog,
            IErrorKnowledgeBase errorKnowledgeBase) : base(processManager, appBundleInformationParser, deviceFinder, consoleLogger, mainLog, errorKnowledgeBase)
        {
        }

        public Task<ExitCode> OrchestrateAppUninstall(
            TestTargetOs target,
            string? deviceName,
            string appPackagePath,
            TimeSpan timeout,
            bool resetSimulator,
            bool enableLldb,
            CancellationToken cancellationToken)
        {
            Func<AppBundleInformation, Task<ExitCode>> executeMacCatalystApp = (appBundleInfo)
                => throw new InvalidOperationException("uninstall command not available on maccatalyst");

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

        protected override Task<ExitCode> InstallApp(AppBundleInformation appBundleInfo, IDevice device, TestTargetOs target, CancellationToken cancellationToken)
            => Task.FromResult(ExitCode.SUCCESS); // no-op for obvious reasons

        private class FakeAppBundleInformationParser : IAppBundleInformationParser
        {
            private readonly string _bundleIdentifier;

            public FakeAppBundleInformationParser(string bundleIdentifier)
            {
                _bundleIdentifier = bundleIdentifier ?? throw new ArgumentNullException(nameof(bundleIdentifier));
            }

            public Task<AppBundleInformation> ParseFromAppBundle(string appPackagePath, TestTarget target, ILog log, CancellationToken cancellationToken = default) =>
                Task.FromResult(new AppBundleInformation(
                    _bundleIdentifier,
                    _bundleIdentifier,
                    appPackagePath,
                    appPackagePath,
                    false));

            public Task<AppBundleInformation> ParseFromProject(string projectFilePath, TestTarget target, string buildConfiguration)
            {
                var path = Path.GetDirectoryName(projectFilePath)!;
                return Task.FromResult(new AppBundleInformation(
                    _bundleIdentifier,
                    _bundleIdentifier,
                    path,
                    path,
                    false));
            }
        }
    }
}
