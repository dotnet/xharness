// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.Common;
using Microsoft.DotNet.XHarness.iOS;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Listeners;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Microsoft.Extensions.Logging;
using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.iOS
{
    /// <summary>
    /// Command which executes a given, already-packaged iOS application, waits on it and returns status based on the outcome.
    /// </summary>
    internal class iOSTestCommand : TestCommand
    {
        private readonly iOSTestCommandArguments _arguments = new iOSTestCommandArguments();
        protected override ITestCommandArguments TestArguments => _arguments;

        public iOSTestCommand() : base()
        {
            Options = new OptionSet()
            {
                "usage: ios test [OPTIONS]",
                "",
                "Packaging command that will create a iOS/tvOS/watchOS or macOS application that can be used to run NUnit or XUnit-based test dlls",
                { "xcode-root=", "Path where Xcode is installed", v => _arguments.XcodeRoot = v},
                { "mlaunch-path=", "Path to the mlaunch binary", v => _arguments.MlaunchPath = v},
                { "launch-timeout=|lt=", "Time span, in seconds, to wait for the iOS app to start.", v => _arguments.LaunchTimeout = TimeSpan.FromSeconds(int.Parse(v))},
            };

            foreach (var option in CommonOptions)
            {
                Options.Add(option);
            }
        }

        protected override async Task<int> InvokeInternal()
        {
            var processManager = new ProcessManager(_arguments.XcodeRoot, _arguments.MlaunchPath);
            var deviceLoader = new HardwareDeviceLoader(processManager);
            var simulatorLoader = new SimulatorLoader(processManager);

            var logs = new Logs(_arguments.OutputDirectory);
            var cancellationToken = new CancellationToken(); // TODO: Get cancellation from command line env?

            // TODO try catch {}
            foreach (var target in _arguments.TestTargets)
            {
                var mainLogFile = Path.Combine(_arguments.OutputDirectory, $"run-{target}.log");
                ILog mainLog = logs.Create(mainLogFile, LogType.ExecutionLog.ToString(), true);
                var verbosity = _arguments.Verbosity.ToInt();

                string deviceName = null;

                if (!target.IsSimulator())
                {
                    _log.LogInformation("Installing the application");

                    var appInstaller = new AppInstaller(processManager, deviceLoader, mainLog, verbosity);

                    ProcessExecutionResult result;
                    (deviceName, result) = await appInstaller.InstallApp(_arguments.AppPackagePath, target, cancellationToken: cancellationToken);
                    if (!result.Succeeded)
                    {
                        _log.LogError("Failed to install the app bundle");
                        return result.ExitCode;
                    }

                    _log.LogInformation("Application was installed successfully");
                }

                var appRunner = new AppRunner(
                    processManager,
                    deviceLoader,
                    simulatorLoader,
                    new SimpleListenerFactory(),
                    new CrashSnapshotReporterFactory(processManager),
                    new CaptureLogFactory(),
                    new DeviceLogCapturerFactory(processManager),
                    new TestReporterFactory(processManager),
                    mainLog,
                    logs,
                    new Helpers());

                var appBundleInformationParser = new AppBundleInformationParser(processManager);

                AppBundleInformation appBundleInfo;

                try
                {
                    appBundleInfo = await appBundleInformationParser.ParseFromAppBundle(_arguments.AppPackagePath, target, mainLog, cancellationToken);
                }
                catch (Exception e)
                {
                    _log.LogError($"Failed to get bundle information: {e}");
                    return (int)ExitCodes.FAILED_TO_GET_BUNDLE_INFO;
                }

                _log.LogInformation($"Starting the application {appBundleInfo.AppName}");

                int exitCode;
                (deviceName, exitCode) = await appRunner.RunApp(appBundleInfo,
                    target,
                    _arguments.Timeout,
                    _arguments.LaunchTimeout,
                    deviceName,
                    verbosity: verbosity,
                    xmlResultJargon: XmlResultJargon.xUnit);

                if (exitCode != 0)
                {
                    _log.LogError("Failed to run the app bundle");
                    return exitCode;
                }

                _log.LogInformation("Application finished the run successfully");

                // TODO: Finally {}
                if (!target.IsSimulator())
                {
                    _log.LogInformation("Uninstalling the application");

                    var appUninstaller = new AppUninstaller(processManager, mainLog, verbosity);
                    var uninstallResult = await appUninstaller.UninstallApp(deviceName, appBundleInfo.BundleIdentifier, cancellationToken);
                    if (!uninstallResult.Succeeded)
                    {
                        _log.LogError("Failed to install the app bundle");
                        return uninstallResult.ExitCode;
                    }

                    _log.LogInformation("Application was uninstalled successfully");
                }
            }

            return 0;
        }
    }
}
