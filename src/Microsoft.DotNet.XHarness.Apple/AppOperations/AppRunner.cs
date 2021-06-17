// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.Common.Utilities;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

namespace Microsoft.DotNet.XHarness.Apple
{
    /// <summary>
    /// Class that will run an app bundle on a given simulator/device and return the exit code.
    /// </summary>
    public class AppRunner : AppRunnerBase
    {
        private readonly IMlaunchProcessManager _processManager;
        private readonly ICrashSnapshotReporterFactory _snapshotReporterFactory;
        private readonly ICaptureLogFactory _captureLogFactory;
        private readonly IDeviceLogCapturerFactory _deviceLogCapturerFactory;
        private readonly IFileBackedLog _mainLog;
        private readonly ILogs _logs;
        private readonly IHelpers _helpers;

        public AppRunner(
            IMlaunchProcessManager processManager,
            ICrashSnapshotReporterFactory snapshotReporterFactory,
            ICaptureLogFactory captureLogFactory,
            IDeviceLogCapturerFactory deviceLogCapturerFactory,
            IFileBackedLog mainLog,
            ILogs logs,
            IHelpers helpers,
            Action<string>? logCallback = null)
            : base(processManager, captureLogFactory, logs, mainLog, helpers, logCallback)
        {
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            _snapshotReporterFactory = snapshotReporterFactory ?? throw new ArgumentNullException(nameof(snapshotReporterFactory));
            _captureLogFactory = captureLogFactory ?? throw new ArgumentNullException(nameof(captureLogFactory));
            _deviceLogCapturerFactory = deviceLogCapturerFactory ?? throw new ArgumentNullException(nameof(deviceLogCapturerFactory));
            _mainLog = mainLog ?? throw new ArgumentNullException(nameof(mainLog));
            _logs = logs ?? throw new ArgumentNullException(nameof(logs));
            _helpers = helpers ?? throw new ArgumentNullException(nameof(helpers));
        }

        public async Task<ProcessExecutionResult> RunMacCatalystApp(
            AppBundleInformation appInformation,
            TimeSpan timeout,
            bool signalAppEnd,
            IEnumerable<string> extraAppArguments,
            IEnumerable<(string, string)> extraEnvVariables,
            CancellationToken cancellationToken = default)
        {
            _mainLog.WriteLine($"*** Executing '{appInformation.AppName}' on MacCatalyst ***");
            var appOutputLog = _logs.Create(appInformation.BundleIdentifier + ".log", LogType.ApplicationLog.ToString(), timestamp: true);

            var envVariables = new Dictionary<string, string>();
            AddExtraEnvVars(envVariables, extraEnvVariables);

            if (signalAppEnd)
            {
                WatchForAppEndTag(out var appEndTag, ref appOutputLog, ref cancellationToken);
                envVariables.Add(EnviromentVariables.AppEndTag, appEndTag);
            }

            using (appOutputLog)
            {
                var result = await RunMacCatalystApp(
                    appInformation,
                    appOutputLog,
                    timeout,
                    extraAppArguments ?? Enumerable.Empty<string>(),
                    envVariables,
                    cancellationToken);

                // When signal is detected, we cancel the call above via the cancellation token so we need to fix the result
                if (_appEndSignalDetected)
                {
                    result.TimedOut = false;
                    result.ExitCode = 0;
                }

                return result;
            }
        }

        public async Task<ProcessExecutionResult> RunApp(
            AppBundleInformation appInformation,
            TestTargetOs target,
            IDevice device,
            IDevice? companionDevice,
            TimeSpan timeout,
            bool signalAppEnd,
            IEnumerable<string> extraAppArguments,
            IEnumerable<(string, string)> extraEnvVariables,
            CancellationToken cancellationToken = default)
        {
            ProcessExecutionResult result;
            ISimulatorDevice? simulator;
            ISimulatorDevice? companionSimulator;

            var isSimulator = target.Platform.IsSimulator();
            using var crashLogs = new Logs(_logs.Directory);
            var appOutputLog = _logs.Create(appInformation.BundleIdentifier + ".log", LogType.ApplicationLog.ToString(), timestamp: true);

            ICrashSnapshotReporter crashReporter = _snapshotReporterFactory.Create(
                _mainLog,
                crashLogs,
                isDevice: !isSimulator,
                device.Name);

            _mainLog.WriteLine($"*** Executing '{appInformation.AppName}' on {target.AsString()} '{device.Name}' ***");

            string? appEndTag = null;
            if (signalAppEnd)
            {
                WatchForAppEndTag(out appEndTag, ref appOutputLog, ref cancellationToken);
            }

            using (appOutputLog)
            {
                if (isSimulator)
                {
                    simulator = device as ISimulatorDevice;
                    companionSimulator = companionDevice as ISimulatorDevice;

                    if (simulator == null)
                    {
                        _mainLog.WriteLine("Didn't find any suitable simulator");
                        throw new NoDeviceFoundException();
                    }

                    var mlaunchArguments = GetSimulatorArguments(
                        appInformation,
                        simulator,
                        extraAppArguments,
                        extraEnvVariables,
                        appEndTag);

                    result = await RunSimulatorApp(
                        mlaunchArguments,
                        crashReporter,
                        simulator,
                        companionSimulator,
                        appOutputLog,
                        timeout,
                        cancellationToken);
                }
                else
                {
                    var mlaunchArguments = GetDeviceArguments(
                        appInformation,
                        device,
                        target.Platform.IsWatchOSTarget(),
                        extraAppArguments,
                        extraEnvVariables,
                        appEndTag);

                    result = await RunDeviceApp(
                        mlaunchArguments,
                        crashReporter,
                        device,
                        appOutputLog,
                        extraEnvVariables,
                        timeout,
                        cancellationToken);
                }

                return result;
            }
        }

        private async Task<ProcessExecutionResult> RunSimulatorApp(
            MlaunchArguments mlaunchArguments,
            ICrashSnapshotReporter crashReporter,
            ISimulatorDevice simulator,
            ISimulatorDevice? companionSimulator,
            ILog appOutputLog,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            _mainLog.WriteLine("System log for the '{1}' simulator is: {0}", simulator.SystemLog, simulator.Name);

            var simulatorLog = _captureLogFactory.Create(
                path: Path.Combine(_logs.Directory, simulator.Name + ".log"),
                systemLogPath: simulator.SystemLog,
                entireFile: false,
                LogType.SystemLog);

            simulatorLog.StartCapture();
            _logs.Add(simulatorLog);

            using var systemLogs = new DisposableList<ICaptureLog>
            {
                simulatorLog
            };

            if (companionSimulator != null)
            {
                _mainLog.WriteLine("System log for the '{1}' companion simulator is: {0}", companionSimulator.SystemLog, companionSimulator.Name);

                var companionLog = _captureLogFactory.Create(
                    path: Path.Combine(_logs.Directory, companionSimulator.Name + ".log"),
                    systemLogPath: companionSimulator.SystemLog,
                    entireFile: false,
                    LogType.CompanionSystemLog);

                companionLog.StartCapture();
                _logs.Add(companionLog);
                systemLogs.Add(companionLog);
            }

            await crashReporter.StartCaptureAsync();

            _mainLog.WriteLine("Starting test run");

            var result = await _processManager.ExecuteCommandAsync(
                mlaunchArguments,
                _mainLog,
                appOutputLog,
                appOutputLog,
                timeout,
                cancellationToken: cancellationToken);

            // When signal is detected, we cancel the call above via the cancellation token so we need to fix the result
            if (_appEndSignalDetected)
            {
                result.TimedOut = false;
                result.ExitCode = 0;
            }

            return result;
        }

        private async Task<ProcessExecutionResult> RunDeviceApp(
            MlaunchArguments mlaunchArguments,
            ICrashSnapshotReporter crashReporter,
            IDevice device,
            ILog appOutputLog,
            IEnumerable<(string, string)> extraEnvVariables,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var deviceSystemLog = _logs.Create($"device-{device.Name}-{_helpers.Timestamp}.log", LogType.SystemLog.ToString());
            var deviceLogCapturer = _deviceLogCapturerFactory.Create(_mainLog, deviceSystemLog, device.Name);
            deviceLogCapturer.StartCapture();

            try
            {
                await crashReporter.StartCaptureAsync();

                _mainLog.WriteLine("Starting the app");

                var envVars = new Dictionary<string, string>();
                AddExtraEnvVars(envVars, extraEnvVariables);

                var result = await _processManager.ExecuteCommandAsync(
                    mlaunchArguments,
                    _mainLog,
                    appOutputLog,
                    appOutputLog,
                    timeout,
                    envVars,
                    cancellationToken: cancellationToken);

                // When signal is detected, we cancel the call above via the cancellation token so we need to fix the result
                if (_appEndSignalDetected)
                {
                    result.TimedOut = false;
                    result.ExitCode = 0;
                }

                return result;
            }
            finally
            {
                deviceLogCapturer.StopCapture();
                deviceSystemLog.Dispose();
            }
        }

        private static MlaunchArguments GetCommonArguments(
            IEnumerable<string> extraAppArguments,
            IEnumerable<(string, string)> extraEnvVariables,
            string? appEndTag)
        {
            var args = new MlaunchArguments();

            // Arguments passed to the iOS app bundle
            args.AddRange(extraAppArguments.Select(arg => new SetAppArgumentArgument(arg)));
            args.AddRange(extraEnvVariables.Select(v => new SetEnvVariableArgument(v.Item1, v.Item2)));

            if (appEndTag != null)
            {
                args.Add(new SetEnvVariableArgument(EnviromentVariables.AppEndTag, appEndTag));
            }

            return args;
        }

        private static MlaunchArguments GetSimulatorArguments(
            AppBundleInformation appInformation,
            ISimulatorDevice simulator,
            IEnumerable<string> extraAppArguments,
            IEnumerable<(string, string)> extraEnvVariables,
            string? appEndTag)
        {
            var args = GetCommonArguments(extraAppArguments, extraEnvVariables, appEndTag);

            args.Add(new SimulatorUDIDArgument(simulator.UDID));

            if (appInformation.Extension.HasValue)
            {
                switch (appInformation.Extension)
                {
                    case Extension.TodayExtension:
                        args.Add(new LaunchSimulatorExtensionArgument(appInformation.LaunchAppPath, appInformation.BundleIdentifier));
                        break;
                    case Extension.WatchKit2:
                    default:
                        throw new NotImplementedException();
                }
            }
            else
            {
                args.Add(new LaunchSimulatorBundleArgument(appInformation));
            }

            return args;
        }

        private static MlaunchArguments GetDeviceArguments(
            AppBundleInformation appInformation,
            IDevice device,
            bool isWatchTarget,
            IEnumerable<string> extraAppArguments,
            IEnumerable<(string, string)> extraEnvVariables,
            string? appEndTag)
        {
            var args = GetCommonArguments(extraAppArguments, extraEnvVariables, appEndTag);

            args.Add(new DisableMemoryLimitsArgument());
            args.Add(new DeviceNameArgument(device));

            if (appInformation.Extension.HasValue)
            {
                switch (appInformation.Extension)
                {
                    case Extension.TodayExtension:
                        args.Add(new LaunchDeviceExtensionArgument(appInformation.LaunchAppPath, appInformation.BundleIdentifier));
                        break;
                    case Extension.WatchKit2:
                    default:
                        throw new NotImplementedException();
                }
            }
            else
            {
                args.Add(new LaunchDeviceBundleIdArgument(appInformation));
            }

            if (isWatchTarget)
            {
                args.Add(new AttachNativeDebuggerArgument()); // this prevents the watch from backgrounding the app.
            }
            else
            {
                args.Add(new WaitForExitArgument());
            }

            return args;
        }
    }
}
