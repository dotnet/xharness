// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Apple;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.Common.CLI.Commands;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Apple
{
    internal abstract class AppleAppCommand<TArguments> : XHarnessCommand where TArguments : AppleAppRunArguments
    {
        protected readonly ErrorKnowledgeBase ErrorKnowledgeBase = new();
        protected override XHarnessCommandArguments Arguments => AppleAppArguments;
        protected abstract TArguments AppleAppArguments { get; }

        protected AppleAppCommand(string name, bool allowsExtraArgs, string? help = null) : base(name, allowsExtraArgs, help)
        {
        }

        protected sealed override async Task<ExitCode> InvokeInternal(Extensions.Logging.ILogger logger)
        {
            var exitCode = ExitCode.SUCCESS;

            var targetName = AppleAppArguments.Target.AsString();

            logger.LogInformation($"Preparing run for {targetName}{ (AppleAppArguments.DeviceName != null ? " targeting " + AppleAppArguments.DeviceName : null) }");

            // Create main log file for the run
            ILogs logs = new Logs(AppleAppArguments.OutputDirectory);
            string logFileName = $"{Name}-{targetName}{(AppleAppArguments.DeviceName != null ? "-" + AppleAppArguments.DeviceName : null)}.log";
            IFileBackedLog mainLog = logs.Create(logFileName, LogType.ExecutionLog.ToString(), timestamp: true);

            var processManager = new MlaunchProcessManager(AppleAppArguments.XcodeRoot, AppleAppArguments.MlaunchPath);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(logger);
            serviceCollection.AddSingleton(mainLog);
            serviceCollection.AddSingleton(logs);
            serviceCollection.AddSingleton<IMlaunchProcessManager>(processManager);
            serviceCollection.AddSingleton<IMacOSProcessManager>(processManager);
            serviceCollection.AddSingleton<IProcessManager>(processManager);
            serviceCollection.AddSingleton<IAppBundleInformationParser, AppBundleInformationParser>();
            serviceCollection.AddSingleton<ISimulatorLoader, SimulatorLoader>();
            serviceCollection.AddSingleton<IHardwareDeviceLoader, HardwareDeviceLoader>();
            serviceCollection.AddSingleton<IDeviceFinder, DeviceFinder>();
            serviceCollection.AddSingleton<IHelpers, Helpers>();

            serviceCollection.AddTransient<XHarness.Apple.ILogger, ConsoleLogger>();
            serviceCollection.AddTransient<IErrorKnowledgeBase, ErrorKnowledgeBase>();
            serviceCollection.AddTransient<ICaptureLogFactory, CaptureLogFactory>();
            serviceCollection.AddTransient<IDeviceLogCapturerFactory, DeviceLogCapturerFactory>();
            serviceCollection.AddTransient<ICrashSnapshotReporterFactory, CrashSnapshotReporterFactory>();

            serviceCollection.AddTransient<IInstallOrchestrator, InstallOrchestrator>();
            serviceCollection.AddTransient<IJustRunOrchestrator, JustRunOrchestrator>();
            serviceCollection.AddTransient<IJustTestOrchestrator, JustTestOrchestrator>();
            serviceCollection.AddTransient<IRunOrchestrator, RunOrchestrator>();
            serviceCollection.AddTransient<ITestOrchestrator, TestOrchestrator>();
            serviceCollection.AddTransient<IUninstallOrchestrator, UninstallOrchestrator>();

            // Pipe the execution log to the debug output of XHarness effectively making "-v" turn this on
            CallbackLog debugLog = new(message => logger.LogDebug(message.Trim()));
            mainLog = Log.CreateReadableAggregatedLog(mainLog, debugLog);
            mainLog.Timestamp = true;

            var cts = new CancellationTokenSource();
            cts.CancelAfter(AppleAppArguments.Timeout);

            var exitCodeForRun = await InvokeInternal(serviceCollection.BuildServiceProvider(), cts.Token);
            if (exitCodeForRun != ExitCode.SUCCESS)
            {
                exitCode = exitCodeForRun;
            }

            return exitCode;
        }

        protected abstract Task<ExitCode> InvokeInternal(IServiceProvider serviceProvider, CancellationToken cancellationToken);

        protected class ConsoleLogger : XHarness.Apple.ILogger
        {
            private readonly Extensions.Logging.ILogger _logger;

            public ConsoleLogger(Extensions.Logging.ILogger logger)
            {
                _logger = logger;
            }

            public void LogDebug(string message) => _logger.LogDebug(message);
            public void LogInformation(string message) => _logger.LogInformation(message);
            public void LogWarning(string message) => _logger.LogWarning(message);
            public void LogError(string message) => _logger.LogError(message);
            public void LogCritical(string message) => _logger.LogCritical(message);
        }
    }
}
