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
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;

namespace Microsoft.DotNet.XHarness.Apple
{
    /// <summary>
    /// This orchestrator implements the `test` command flow.
    /// In this flow we need to connect to the running application over TCP and receive
    /// the test results. We also need to watch timeouts better and parse the results
    /// more comprehensively.
    /// </summary>
    public class TestOrchestrator : BaseTestOrchestrator
    {
        public TestOrchestrator(
            IMlaunchProcessManager processManager,
            IAppBundleInformationParser appBundleInformationParser,
            DeviceFinder deviceFinder,
            ILogger consoleLogger,
            ILogs logs,
            IFileBackedLog mainLog,
            IErrorKnowledgeBase errorKnowledgeBase)
            : base(processManager, appBundleInformationParser, deviceFinder, consoleLogger, logs, mainLog, errorKnowledgeBase)
        {
        }

        public Task<ExitCode> OrchestrateTest(
            TestTargetOs target,
            string? deviceName,
            string appPackagePath,
            TimeSpan timeout,
            TimeSpan launchTimeout,
            CommunicationChannel communicationChannel,
            XmlResultJargon xmlResultJargon,
            IEnumerable<string> singleMethodFilters,
            IEnumerable<string> classMethodFilters,
            bool resetSimulator,
            bool enableLldb,
            IReadOnlyCollection<(string, string)> environmentalVariables,
            IEnumerable<string> passthroughArguments,
            CancellationToken cancellationToken)
        {
            Func<AppBundleInformation, Task<ExitCode>> executeMacCatalystApp = (appBundleInfo) =>
                ExecuteMacCatalystApp(
                    appBundleInfo,
                    timeout,
                    launchTimeout,
                    communicationChannel,
                    xmlResultJargon,
                    singleMethodFilters,
                    classMethodFilters,
                    environmentalVariables,
                    passthroughArguments,
                    cancellationToken);

            Func<AppBundleInformation, IDevice, IDevice?, Task<ExitCode>> executeApp = (appBundleInfo, device, companionDevice) =>
                ExecuteApp(
                    appBundleInfo,
                    target,
                    device,
                    companionDevice,
                    timeout,
                    launchTimeout,
                    communicationChannel,
                    xmlResultJargon,
                    singleMethodFilters,
                    classMethodFilters,
                    environmentalVariables,
                    passthroughArguments,
                    cancellationToken);

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
    }
}
