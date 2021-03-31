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
            : base(processManager, captureLogFactory, logs, mainLog, logCallback)
        {
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            _snapshotReporterFactory = snapshotReporterFactory ?? throw new ArgumentNullException(nameof(snapshotReporterFactory));
            _captureLogFactory = captureLogFactory ?? throw new ArgumentNullException(nameof(captureLogFactory));
            _deviceLogCapturerFactory = deviceLogCapturerFactory ?? throw new ArgumentNullException(nameof(deviceLogCapturerFactory));
            _mainLog = mainLog ?? throw new ArgumentNullException(nameof(mainLog));
            _logs = logs ?? throw new ArgumentNullException(nameof(logs));
            _helpers = helpers ?? throw new ArgumentNullException(nameof(helpers));
        }

        public async Task<ProcessExecutionResult> RunApp(
            AppBundleInformation appInformation,
            TestTargetOs target,
            IDevice device,
            TimeSpan timeout,
            IEnumerable<string> extraAppArguments,
            IEnumerable<(string, string)> extraEnvVariables,
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
                return result;
            }

            var crashLogs = new Logs(_logs.Directory);

            ICrashSnapshotReporter crashReporter = _snapshotReporterFactory.Create(
                _mainLog,
                crashLogs,
                isDevice: !isSimulator,
                device.Name);

            _mainLog.WriteLine($"*** Executing '{appInformation.AppName}' on {target.AsString()} '{device.Name}' ***");

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
                    extraEnvVariables,
                    verbosity);

                result = await RunSimulatorApp(
                    mlaunchArguments,
                    appInformation,
                    crashReporter,
                    simulator,
                    companionSimulator,
                    resetSimulator,
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
                    verbosity);

                result = await RunDeviceApp(
                    mlaunchArguments,
                    crashReporter,
                    device,
                    extraEnvVariables,
                    timeout,
                    cancellationToken);
            }

            return result;
        }

        private async Task<ProcessExecutionResult> RunSimulatorApp(
            MlaunchArguments mlaunchArguments,
            AppBundleInformation appInformation,
            ICrashSnapshotReporter crashReporter,
            ISimulatorDevice simulator,
            ISimulatorDevice? companionSimulator,
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

                var result = await _processManager.ExecuteCommandAsync(mlaunchArguments, _mainLog, timeout, cancellationToken: cancellationToken);

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
            IDevice device,
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

        private static MlaunchArguments GetCommonArguments(
            IEnumerable<string> extraAppArguments,
            IEnumerable<(string, string)> extraEnvVariables,
            int verbosity)
        {
            var args = new MlaunchArguments();

            for (var i = -1; i < verbosity; i++)
            {
                args.Add(new VerbosityArgument());
            }

            // Arguments passed to the iOS app bundle
            args.AddRange(extraAppArguments.Select(arg => new SetAppArgumentArgument(arg)));
            args.AddRange(extraEnvVariables.Select(v => new SetEnvVariableArgument(v.Item1, v.Item2)));

            return args;
        }

        private static MlaunchArguments GetSimulatorArguments(
            AppBundleInformation appInformation,
            ISimulatorDevice simulator,
            IEnumerable<string> extraAppArguments,
            IEnumerable<(string, string)> extraEnvVariables,
            int verbosity)
        {
            var args = GetCommonArguments(extraAppArguments, extraEnvVariables, verbosity);

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

        private static MlaunchArguments GetDeviceArguments(
            AppBundleInformation appInformation,
            IDevice device,
            bool isWatchTarget,
            IEnumerable<string> extraAppArguments,
            IEnumerable<(string, string)> extraEnvVariables,
            int verbosity)
        {
            var args = GetCommonArguments(extraAppArguments, extraEnvVariables, verbosity);

            args.Add(new DisableMemoryLimitsArgument());
            args.Add(new DeviceNameArgument(device.UDID));

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
