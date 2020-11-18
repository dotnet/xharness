// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

namespace Microsoft.DotNet.XHarness.iOS
{
    public class AppRunner
    {
        private readonly IMlaunchProcessManager _processManager;
        private readonly IHardwareDeviceLoader _hardwareDeviceLoader;
        private readonly ISimulatorLoader _simulatorLoader;
        private readonly ICrashSnapshotReporterFactory _snapshotReporterFactory;
        private readonly ICaptureLogFactory _captureLogFactory;
        private readonly IDeviceLogCapturerFactory _deviceLogCapturerFactory;
        private readonly ILogs _logs;
        private readonly IFileBackedLog _mainLog;
        private readonly IHelpers _helpers;
        private readonly IEnumerable<string> _appArguments; // Arguments that will be passed to the iOS application

        public AppRunner(IMlaunchProcessManager processManager,
            IHardwareDeviceLoader hardwareDeviceLoader,
            ISimulatorLoader simulatorLoader,
            ICrashSnapshotReporterFactory snapshotReporterFactory,
            ICaptureLogFactory captureLogFactory,
            IDeviceLogCapturerFactory deviceLogCapturerFactory,
            IFileBackedLog mainLog,
            ILogs logs,
            IHelpers helpers,
            IEnumerable<string> appArguments,
            Action<string>? logCallback = null)
        {
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            _hardwareDeviceLoader = hardwareDeviceLoader ?? throw new ArgumentNullException(nameof(hardwareDeviceLoader));
            _simulatorLoader = simulatorLoader ?? throw new ArgumentNullException(nameof(simulatorLoader));
            _snapshotReporterFactory = snapshotReporterFactory ?? throw new ArgumentNullException(nameof(snapshotReporterFactory));
            _captureLogFactory = captureLogFactory ?? throw new ArgumentNullException(nameof(captureLogFactory));
            _deviceLogCapturerFactory = deviceLogCapturerFactory ?? throw new ArgumentNullException(nameof(deviceLogCapturerFactory));
            _logs = logs ?? throw new ArgumentNullException(nameof(logs));
            _helpers = helpers ?? throw new ArgumentNullException(nameof(helpers));
            _appArguments = appArguments;

            if (logCallback == null)
            {
                _mainLog = mainLog ?? throw new ArgumentNullException(nameof(mainLog));
            }
            else
            {
                // create using the main as the default log
                _mainLog = Log.CreateReadableAggregatedLog(mainLog, new CallbackLog(logCallback));
            }
        }

        public async Task<(string DeviceName, int exitCode)> RunApp(
            AppBundleInformation appInformation,
            TestTargetOs target,
            TimeSpan timeout,
            string? deviceName = null,
            string? companionDeviceName = null,
            bool ensureCleanSimulatorState = false,
            int verbosity = 1,
            CancellationToken cancellationToken = default)
        {
            bool isSimulator = target.Platform.IsSimulator();

            ISimulatorDevice? simulator = null;
            ISimulatorDevice? companionSimulator = null;

            // Find devices
            if (isSimulator)
            {
                int attempt = 1;
                const int maxAttempts = 3;
                while (true)
                {
                    try
                    {
                        (simulator, companionSimulator) = await _simulatorLoader.FindSimulators(target, _mainLog);
                        break;
                    }
                    catch (Exception e)
                    {
                        _mainLog.WriteLine($"Failed to find/create simulator (attempt {attempt}/{maxAttempts}):" + Environment.NewLine + e);

                        if (attempt == maxAttempts)
                        {
                            throw new NoDeviceFoundException("Failed to find/create suitable simulator");
                        }
                    }
                    finally
                    {
                        attempt++;
                    }
                }

                deviceName = companionSimulator?.Name ?? simulator.Name;
            }
            else
            {
                deviceName ??= await FindDevice(target) ?? throw new NoDeviceFoundException();
            }

            var crashLogs = new Logs(_logs.Directory);

            ICrashSnapshotReporter crashReporter = _snapshotReporterFactory.Create(_mainLog, crashLogs, isDevice: !isSimulator, deviceName);

            _mainLog.WriteLine($"*** Executing '{appInformation.AppName}' on {target} '{deviceName}' ***");

            if (isSimulator)
            {
                if (simulator == null)
                {
                    _mainLog.WriteLine("Didn't find any suitable simulator");
                    throw new NoDeviceFoundException();
                }

                var mlaunchArguments = GetSimulatorArguments(appInformation, simulator, verbosity);

                await RunSimulatorApp(
                    mlaunchArguments,
                    appInformation,
                    crashReporter,
                    simulator,
                    companionSimulator,
                    ensureCleanSimulatorState,
                    timeout,
                    cancellationToken);
            }
            else
            {
                var mlaunchArguments = GetDeviceArguments(appInformation, deviceName, target.Platform.IsWatchOSTarget(), verbosity);

                await RunDeviceApp(
                    mlaunchArguments,
                    crashReporter,
                    deviceName,
                    timeout,
                    cancellationToken);
            }

            // TODO: Remove
            _mainLog.WriteLine("Run ended");

            return (deviceName, 0);
        }

        private async Task RunSimulatorApp(
            MlaunchArguments mlaunchArguments,
            AppBundleInformation appInformation,
            ICrashSnapshotReporter crashReporter,
            ISimulatorDevice simulator,
            ISimulatorDevice? companionSimulator,
            bool ensureCleanSimulatorState,
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
                    entireFile: true,
                    LogType.SystemLog.ToString());

                simulatorLog.StartCapture();
                _logs.Add(simulatorLog);
                systemLogs.Add(simulatorLog);

                if (companionSimulator != null)
                {
                    _mainLog.WriteLine("System log for the '{1}' companion simulator is: {0}", companionSimulator.SystemLog, companionSimulator.Name);

                    var companionLog = _captureLogFactory.Create(
                        path: Path.Combine(_logs.Directory, companionSimulator.Name + ".log"),
                        systemLogPath: companionSimulator.SystemLog,
                        entireFile: true,
                        LogType.CompanionSystemLog.ToString());

                    companionLog.StartCapture();
                    _logs.Add(companionLog);
                    systemLogs.Add(companionLog);
                }

                if (ensureCleanSimulatorState)
                {
                    await simulator.PrepareSimulator(_mainLog, appInformation.BundleIdentifier);

                    if (companionSimulator != null)
                    {
                        await companionSimulator.PrepareSimulator(_mainLog, appInformation.BundleIdentifier);
                    }
                }

                await crashReporter.StartCaptureAsync();

                _mainLog.WriteLine("Starting test run");

                await _processManager.ExecuteCommandAsync(mlaunchArguments, _mainLog, timeout, cancellationToken: cancellationToken);

                // cleanup after us
                if (ensureCleanSimulatorState)
                {
                    await simulator.KillEverything(_mainLog);

                    if (companionSimulator != null)
                    {
                        await companionSimulator.KillEverything(_mainLog);
                    }
                }
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

        private async Task RunDeviceApp(
            MlaunchArguments mlaunchArguments,
            ICrashSnapshotReporter crashReporter,
            string deviceName,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var deviceSystemLog = _logs.Create($"device-{deviceName}-{_helpers.Timestamp}.log", "Device log");
            var deviceLogCapturer = _deviceLogCapturerFactory.Create(_mainLog, deviceSystemLog, deviceName);
            deviceLogCapturer.StartCapture();

            try
            {
                await crashReporter.StartCaptureAsync();

                _mainLog.WriteLine("Starting test run");

                await _processManager.ExecuteCommandAsync(
                    mlaunchArguments,
                    _mainLog,
                    timeout,
                    cancellationToken: cancellationToken);
            }
            finally
            {
                deviceLogCapturer.StopCapture();
                deviceSystemLog.Dispose();
            }

            // Upload the system log
            if (File.Exists(deviceSystemLog.FullPath))
            {
                _mainLog.WriteLine("A capture of the device log is: {0}", deviceSystemLog.FullPath);
            }
        }

        private MlaunchArguments GetCommonArguments(int verbosity)
        {
            var args = new MlaunchArguments
            {
                new SetAppArgumentArgument("-connection-mode"),
                new SetAppArgumentArgument("none"), // This will prevent the app from trying to connect to any IDEs

                // On macOS we can't edit the TCC database easily
                // (it requires adding the mac has to be using MDM: https://carlashley.com/2018/09/28/tcc-round-up/)
                // So by default ignore any tests that would pop up permission dialogs in CI.
                new SetEnvVariableArgument(EnviromentVariables.DisableSystemPermissionTests, 1),
            };

            for (int i = -1; i < verbosity; i++)
            {
                args.Add(new VerbosityArgument());
            }

            // Arguments passed to the iOS app bundle
            args.AddRange(_appArguments.Select(arg => new SetAppArgumentArgument(arg, true)));

            return args;
        }

        private MlaunchArguments GetSimulatorArguments(
            AppBundleInformation appInformation,
            ISimulatorDevice simulator,
            int verbosity)
        {
            var args = GetCommonArguments(verbosity);

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
            int verbosity)
        {
            var args = GetCommonArguments(verbosity);

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

        private async Task<string> FindDevice(TestTargetOs target)
        {
            IHardwareDevice? companionDevice = null;
            IHardwareDevice device = await _hardwareDeviceLoader.FindDevice(target.Platform.ToRunMode(), _mainLog, includeLocked: false, force: false);

            if (target.Platform.IsWatchOSTarget())
            {
                companionDevice = await _hardwareDeviceLoader.FindCompanionDevice(_mainLog, device);
            }

            return companionDevice?.Name ?? device.Name;
        }
    }
}
