// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

namespace Microsoft.DotNet.XHarness.Apple
{
    /// <summary>
    /// Class that will run an app bundle and return the exit code.
    /// </summary>
    public class AppRunner : AppRunnerBase
    {
        private readonly IMlaunchProcessManager _processManager;
        private readonly ISimulatorLoader _simulatorLoader;
        private readonly ICrashSnapshotReporterFactory _snapshotReporterFactory;
        private readonly ICaptureLogFactory _captureLogFactory;
        private readonly IDeviceLogCapturerFactory _deviceLogCapturerFactory;
        private readonly IFileBackedLog _mainLog;
        private readonly ILogs _logs;
        private readonly IHelpers _helpers;

        public AppRunner(
            IMlaunchProcessManager processManager,
            IHardwareDeviceLoader hardwareDeviceLoader,
            ISimulatorLoader simulatorLoader,
            ICrashSnapshotReporterFactory snapshotReporterFactory,
            ICaptureLogFactory captureLogFactory,
            IDeviceLogCapturerFactory deviceLogCapturerFactory,
            IFileBackedLog mainLog,
            ILogs logs,
            IHelpers helpers,
            Action<string>? logCallback = null)
            : base(processManager, hardwareDeviceLoader, captureLogFactory, logs, mainLog, logCallback)
        {
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            _simulatorLoader = simulatorLoader ?? throw new ArgumentNullException(nameof(simulatorLoader));
            _snapshotReporterFactory = snapshotReporterFactory ?? throw new ArgumentNullException(nameof(snapshotReporterFactory));
            _captureLogFactory = captureLogFactory ?? throw new ArgumentNullException(nameof(captureLogFactory));
            _deviceLogCapturerFactory = deviceLogCapturerFactory ?? throw new ArgumentNullException(nameof(deviceLogCapturerFactory));
            _mainLog = mainLog ?? throw new ArgumentNullException(nameof(mainLog));
            _logs = logs ?? throw new ArgumentNullException(nameof(logs));
            _helpers = helpers ?? throw new ArgumentNullException(nameof(helpers));
        }

        public async Task<(string DeviceName, ProcessExecutionResult result)> RunApp(
            AppBundleInformation appInformation,
            TestTargetOs target,
            TimeSpan timeout,
            IEnumerable<string> extraAppArguments,
            IEnumerable<(string, string)> extraEnvVariables,
            string? deviceName = null,
            string? companionDeviceName = null,
            bool resetSimulator = false,
            int verbosity = 1,
            CancellationToken cancellationToken = default)
        {
            var isSimulator = target.Platform.IsSimulator();

            ProcessExecutionResult result;
            ISimulatorDevice? simulator = null;
            ISimulatorDevice? companionSimulator = null;

            if (target.Platform == TestTarget.MacCatalyst)
            {
                _mainLog.WriteLine($"*** Executing '{appInformation.AppName}' on MacCatalyst ***");

                var envVariables = new Dictionary<string, string>();
                AddExtraEnvVars(envVariables, extraEnvVariables);

                result = await RunMacCatalystApp(appInformation, timeout, extraAppArguments ?? Enumerable.Empty<string>(), envVariables, cancellationToken);
                return ("MacCatalyst", result);
            }

            // Find devices
            if (isSimulator)
            {
                (simulator, companionSimulator) = await _simulatorLoader.FindSimulators(target, _mainLog, 3);
                deviceName = companionSimulator?.Name ?? simulator.Name;
            }
            else
            {
                deviceName ??= await FindDevice(target) ?? throw new NoDeviceFoundException();
            }

            var crashLogs = new Logs(_logs.Directory);

            ICrashSnapshotReporter crashReporter = _snapshotReporterFactory.Create(
                _mainLog,
                crashLogs,
                isDevice: !isSimulator,
                deviceName);

            _mainLog.WriteLine($"*** Executing '{appInformation.AppName}' on {target.AsString()} '{deviceName}' ***");

            if (isSimulator)
            {
                if (simulator == null)
                {
                    _mainLog.WriteLine("Didn't find any suitable simulator");
                    throw new NoDeviceFoundException();
                }

                var mlaunchArguments = GetSimulatorArguments(
                    appInformation,
                    simulator,
                    extraAppArguments,
                    verbosity);

                result = await RunSimulatorApp(
                    mlaunchArguments,
                    appInformation,
                    crashReporter,
                    simulator,
                    companionSimulator,
                    extraEnvVariables,
                    resetSimulator,
                    timeout,
                    cancellationToken);
            }
            else
            {
                var mlaunchArguments = GetDeviceArguments(
                    appInformation,
                    deviceName,
                    target.Platform.IsWatchOSTarget(),
                    extraAppArguments,
                    verbosity);

                result = await RunDeviceApp(
                    mlaunchArguments,
                    crashReporter,
                    deviceName,
                    extraEnvVariables,
                    timeout,
                    cancellationToken);
            }

            return (deviceName, result);
        }

        private async Task<ProcessExecutionResult> RunSimulatorApp(
            MlaunchArguments mlaunchArguments,
            AppBundleInformation appInformation,
            ICrashSnapshotReporter crashReporter,
            ISimulatorDevice simulator,
            ISimulatorDevice? companionSimulator,
            IEnumerable<(string, string)> extraEnvVariables,
            bool resetSimulator,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var systemLogs = new List<ICaptureLog>();

            try
            {
                _mainLog.WriteLine("System log for the '{1}' simulator is: {0}", simulator.SystemLog, simulator.Name);

                var simulatorLog = _captureLogFactory.Create(
                    path: Path.Combine(_logs.Directory, simulator.Name + ".log"),
                    systemLogPath: simulator.SystemLog,
                    entireFile: false,
                    LogType.SystemLog);

                simulatorLog.StartCapture();
                _logs.Add(simulatorLog);
                systemLogs.Add(simulatorLog);

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

                if (resetSimulator)
                {
                    await simulator.PrepareSimulator(_mainLog, appInformation.BundleIdentifier);

                    if (companionSimulator != null)
                    {
                        await companionSimulator.PrepareSimulator(_mainLog, appInformation.BundleIdentifier);
                    }
                }

                await crashReporter.StartCaptureAsync();

                _mainLog.WriteLine("Starting test run");

                var envVariables = new Dictionary<string, string>();
                AddExtraEnvVars(envVariables, extraEnvVariables);

                var result = await _processManager.ExecuteCommandAsync(mlaunchArguments, _mainLog, timeout, envVariables, cancellationToken);

                // cleanup after us
                if (resetSimulator)
                {
                    await simulator.KillEverything(_mainLog);

                    if (companionSimulator != null)
                    {
                        await companionSimulator.KillEverything(_mainLog);
                    }
                }

                return result;
            }
            finally
            {
                foreach (ICaptureLog? log in systemLogs)
                {
                    log.StopCapture();
                    log.Dispose();
                }
            }
        }

        private async Task<ProcessExecutionResult> RunDeviceApp(
            MlaunchArguments mlaunchArguments,
            ICrashSnapshotReporter crashReporter,
            string deviceName,
            IEnumerable<(string, string)> extraEnvVariables,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var deviceSystemLog = _logs.Create($"device-{deviceName}-{_helpers.Timestamp}.log", LogType.SystemLog.ToString());
            var deviceLogCapturer = _deviceLogCapturerFactory.Create(_mainLog, deviceSystemLog, deviceName);
            deviceLogCapturer.StartCapture();

            try
            {
                await crashReporter.StartCaptureAsync();

                _mainLog.WriteLine("Starting the app");

                var envVars = new Dictionary<string, string>();
                AddExtraEnvVars(envVars, extraEnvVariables);

                return await _processManager.ExecuteCommandAsync(
                    mlaunchArguments,
                    _mainLog,
                    timeout,
                    envVars,
                    cancellationToken: cancellationToken);
            }
            finally
            {
                deviceLogCapturer.StopCapture();
                deviceSystemLog.Dispose();
            }
        }

        private static MlaunchArguments GetCommonArguments(IEnumerable<string> extraAppArguments, int verbosity)
        {
            var args = new MlaunchArguments();

            for (var i = -1; i < verbosity; i++)
            {
                args.Add(new VerbosityArgument());
            }

            // Arguments passed to the iOS app bundle
            args.AddRange(extraAppArguments.Select(arg => new SetAppArgumentArgument(arg)));

            return args;
        }

        private MlaunchArguments GetSimulatorArguments(
            AppBundleInformation appInformation,
            ISimulatorDevice simulator,
            IEnumerable<string> extraAppArguments,
            int verbosity)
        {
            var args = GetCommonArguments(extraAppArguments, verbosity);

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
                args.Add(new LaunchSimulatorArgument(appInformation.LaunchAppPath));
            }

            return args;
        }

        private MlaunchArguments GetDeviceArguments(
            AppBundleInformation appInformation,
            string deviceName,
            bool isWatchTarget,
            IEnumerable<string> extraAppArguments,
            int verbosity)
        {
            var args = GetCommonArguments(extraAppArguments, verbosity);

            args.Add(new DisableMemoryLimitsArgument());
            args.Add(new DeviceNameArgument(deviceName));

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
                args.Add(new LaunchDeviceArgument(appInformation.LaunchAppPath));
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
