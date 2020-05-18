// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.Common.Utilities;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution.Mlaunch;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Listeners;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

namespace Microsoft.DotNet.XHarness.iOS
{
    public class AppRunner
    {
        private readonly IMLaunchProcessManager _processManager;
        private readonly IHardwareDeviceLoader _hardwareDeviceLoader;
        private readonly ISimulatorLoader _simulatorLoader;
        private readonly ISimpleListenerFactory _listenerFactory;
        private readonly ICrashSnapshotReporterFactory _snapshotReporterFactory;
        private readonly ICaptureLogFactory _captureLogFactory;
        private readonly IDeviceLogCapturerFactory _deviceLogCapturerFactory;
        private readonly ITestReporterFactory _testReporterFactory;
        private readonly ILogs _logs;
        private readonly IFileBackedLog _mainLog;
        private readonly IHelpers _helpers;
        private readonly bool _useXmlOutput;

        public AppRunner(IMLaunchProcessManager processManager,
            IHardwareDeviceLoader hardwareDeviceLoader,
            ISimulatorLoader simulatorLoader,
            ISimpleListenerFactory simpleListenerFactory,
            ICrashSnapshotReporterFactory snapshotReporterFactory,
            ICaptureLogFactory captureLogFactory,
            IDeviceLogCapturerFactory deviceLogCapturerFactory,
            ITestReporterFactory reporterFactory,
            IFileBackedLog mainLog,
            ILogs logs,
            IHelpers helpers,
            bool useXmlOutput,
            Action<string>? logCallback = null)
        {
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            _hardwareDeviceLoader = hardwareDeviceLoader ?? throw new ArgumentNullException(nameof(hardwareDeviceLoader));
            _simulatorLoader = simulatorLoader ?? throw new ArgumentNullException(nameof(simulatorLoader));
            _listenerFactory = simpleListenerFactory ?? throw new ArgumentNullException(nameof(simpleListenerFactory));
            _snapshotReporterFactory = snapshotReporterFactory ?? throw new ArgumentNullException(nameof(snapshotReporterFactory));
            _captureLogFactory = captureLogFactory ?? throw new ArgumentNullException(nameof(captureLogFactory));
            _deviceLogCapturerFactory = deviceLogCapturerFactory ?? throw new ArgumentNullException(nameof(deviceLogCapturerFactory));
            _testReporterFactory = reporterFactory ?? throw new ArgumentNullException(nameof(_testReporterFactory));
            _logs = logs ?? throw new ArgumentNullException(nameof(logs));
            _helpers = helpers ?? throw new ArgumentNullException(nameof(helpers));
            _useXmlOutput = useXmlOutput;
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

        public async Task<(string DeviceName, TestExecutingResult Result, string ResultMessage)> RunApp(
            AppBundleInformation appInformation,
            TestTarget target,
            TimeSpan timeout,
            TimeSpan testLaunchTimeout,
            string? deviceName = null,
            string? companionDeviceName = null,
            bool ensureCleanSimulatorState = false,
            int verbosity = 1,
            XmlResultJargon xmlResultJargon = XmlResultJargon.xUnit,
            CancellationToken cancellationToken = default)
        {
            var args = new MlaunchArguments
            {
                new SetAppArgumentArgument("-connection-mode"),
                new SetAppArgumentArgument("none"), // This will prevent the app from trying to connect to any IDEs
                new SetAppArgumentArgument("-autostart", true),
                new SetEnvVariableArgument(EnviromentVariables.AutoStart, true),
                new SetAppArgumentArgument("-autoexit", true),
                new SetEnvVariableArgument(EnviromentVariables.AutoExit, true),
                new SetAppArgumentArgument("-enablenetwork", true),
                new SetEnvVariableArgument(EnviromentVariables.EnableNetwork, true),

                // On macOS we can't edit the TCC database easily
                // (it requires adding the mac has to be using MDM: https://carlashley.com/2018/09/28/tcc-round-up/)
                // So by default ignore any tests that would pop up permission dialogs in CI.
                new SetEnvVariableArgument(EnviromentVariables.DisableSystemPermissionTests, 1),
            };

            for (int i = -1; i < verbosity; i++)
            {
                args.Add(new VerbosityArgument());
            }

            var isSimulator = target.IsSimulator();

            if (isSimulator)
            {
                args.Add(new SetAppArgumentArgument("-hostname:127.0.0.1", true));
                args.Add(new SetEnvVariableArgument(EnviromentVariables.HostName, "127.0.0.1"));
            }
            else
            {
                var ipAddresses = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList.Select(ip => ip.ToString());
                var ips = string.Join(",", ipAddresses);
                args.Add(new SetAppArgumentArgument($"-hostname:{ips}", true));
                args.Add(new SetEnvVariableArgument(EnviromentVariables.HostName, ips));
            }

            var listenerLog = _logs.Create($"test-{target.AsString()}-{_helpers.Timestamp}.log", LogType.TestLog.ToString(), timestamp: true);
            var (transport, listener, listenerTmpFile) = _listenerFactory.Create(target.ToRunMode(),
                log: _mainLog,
                testLog: listenerLog,
                isSimulator: isSimulator,
                autoExit: true,
                xmlOutput: true); // cli always uses xml

            // Initialize has to be called before we try to get Port (internal implementation of the listener says so)
            // TODO: Improve this to not get into a broken state - it was really hard to debug when I moved this lower
            listener.Initialize();

            args.Add(new SetAppArgumentArgument($"-transport:{transport}", true));
            args.Add(new SetEnvVariableArgument(EnviromentVariables.Transport, transport.ToString().ToUpper()));

            if (transport == ListenerTransport.File)
            {
                args.Add(new SetEnvVariableArgument(EnviromentVariables.LogFilePath, listenerTmpFile));
            }

            args.Add(new SetAppArgumentArgument($"-hostport:{listener.Port}", true));
            args.Add(new SetEnvVariableArgument(EnviromentVariables.HostPort, listener.Port));

            if (_listenerFactory.UseTunnel && !isSimulator) // simulators do not support tunnels
            {
                args.Add(new SetEnvVariableArgument(EnviromentVariables.UseTcpTunnel, true));
            }

            if (_useXmlOutput)
            {
                // let the runner now via envars that we want to get a xml output, else the runner will default to plain text
                args.Add(new SetEnvVariableArgument(EnviromentVariables.EnableXmlOutput, true));
                args.Add(new SetEnvVariableArgument(EnviromentVariables.XmlMode, "wrapped"));
                args.Add(new SetEnvVariableArgument(EnviromentVariables.XmlVersion, $"{xmlResultJargon}"));
            }

            listener.StartAsync();

            var crashLogs = new Logs(_logs.Directory);

            if (appInformation.Extension.HasValue)
            {
                switch (appInformation.Extension)
                {
                    case Extension.TodayExtension:
                        args.Add(isSimulator
                            ? (MlaunchArgument)new LaunchSimulatorExtensionArgument(appInformation.LaunchAppPath, appInformation.BundleIdentifier)
                            : new LaunchDeviceExtensionArgument(appInformation.LaunchAppPath, appInformation.BundleIdentifier));
                        break;
                    case Extension.WatchKit2:
                    default:
                        throw new NotImplementedException();
                }
            }
            else
            {
                args.Add(isSimulator
                    ? (MlaunchArgument)new LaunchSimulatorArgument(appInformation.LaunchAppPath)
                    : new LaunchDeviceArgument(appInformation.LaunchAppPath));
            }

            var runMode = target.ToRunMode();
            ICrashSnapshotReporter crashReporter;
            ITestReporter testReporter;

            if (isSimulator)
            {
                crashReporter = _snapshotReporterFactory.Create(_mainLog, crashLogs, isDevice: !isSimulator, deviceName: null!);
                testReporter = _testReporterFactory.Create(_mainLog,
                    _mainLog,
                    _logs,
                    crashReporter,
                    listener,
                    new XmlResultParser(),
                    appInformation,
                    runMode,
                    xmlResultJargon,
                    device: null,
                    timeout,
                    null,
                    (level, message) => _mainLog.WriteLine(message));

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(testReporter.CancellationToken, cancellationToken);

                listener.ConnectedTask
                    .TimeoutAfter(testLaunchTimeout)
                    .ContinueWith(testReporter.LaunchCallback)
                    .DoNotAwait();

                await _simulatorLoader.LoadDevices(_logs.Create($"simulator-list-{_helpers.Timestamp}.log", "Simulator list"), false, false);

                var simulators = await _simulatorLoader.FindSimulators(target, _mainLog);
                if (!(simulators?.Any() ?? false))
                {
                    _mainLog.WriteLine("Didn't find any suitable simulators");
                    throw new NoDeviceFoundException();
                }

                var simulator = string.IsNullOrEmpty(deviceName)
                    ? simulators.FirstOrDefault()
                    : simulators.FirstOrDefault(s => string.Equals(s.Name, deviceName, StringComparison.InvariantCultureIgnoreCase));

                if (simulator == null)
                {
                    throw new NoDeviceFoundException();
                }

                deviceName = simulator.Name;

                var systemLogs = new List<ICaptureLog>();
                foreach (var sim in simulators)
                {
                    // Upload the system log
                    _mainLog.WriteLine("System log for the '{1}' simulator is: {0}", sim.SystemLog, sim.Name);
                    bool isCompanion = sim != simulator;

                    var logDescription = isCompanion ? LogType.CompanionSystemLog.ToString() : LogType.SystemLog.ToString();
                    var log = _captureLogFactory.Create(
                        Path.Combine(_logs.Directory, sim.Name + ".log"),
                        sim.SystemLog,
                        true,
                        logDescription);

                    log.StartCapture();
                    _logs.Add(log);
                    systemLogs.Add(log);
                }

                try
                {
                    _mainLog.WriteLine("*** Executing {0}/{1} in the simulator ***", appInformation.AppName, target);

                    if (ensureCleanSimulatorState)
                    {
                        foreach (var sim in simulators)
                        {
                            await sim.PrepareSimulator(_mainLog, appInformation.BundleIdentifier);
                        }
                    }

                    args.Add(new SimulatorUDIDArgument(simulator.UDID));

                    await crashReporter.StartCaptureAsync();

                    _mainLog.WriteLine("Starting test run");

                    var result = _processManager.ExecuteCommandAsync(args, _mainLog, timeout, cancellationToken: linkedCts.Token);

                    await testReporter.CollectSimulatorResult(result);

                    // cleanup after us
                    if (ensureCleanSimulatorState)
                    {
                        await simulator.KillEverything(_mainLog);
                    }

                    foreach (var log in systemLogs)
                    {
                        log.StopCapture();
                    }
                }
                finally
                {
                    foreach (var log in systemLogs)
                    {
                        log.StopCapture();
                        log.Dispose();
                    }
                }
            }
            else
            {
                args.Add(new DisableMemoryLimitsArgument());

                if (deviceName == null)
                {
                    IHardwareDevice? companionDevice = null;
                    IHardwareDevice device = await _hardwareDeviceLoader.FindDevice(runMode, _mainLog, includeLocked: false, force: false);

                    if (target.IsWatchOSTarget())
                    {
                        companionDevice = await _hardwareDeviceLoader.FindCompanionDevice(_mainLog, device);
                    }

                    deviceName = companionDevice?.Name ?? device.Name;
                }

                if (deviceName == null)
                {
                    throw new NoDeviceFoundException();
                }

                crashReporter = _snapshotReporterFactory.Create(_mainLog, crashLogs, isDevice: !isSimulator, deviceName);
                testReporter = _testReporterFactory.Create(_mainLog,
                    _mainLog,
                    _logs,
                    crashReporter,
                    listener,
                    new XmlResultParser(),
                    appInformation,
                    runMode,
                    xmlResultJargon,
                    deviceName,
                    timeout,
                    null,
                    (level, message) => _mainLog.WriteLine(message));

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(testReporter.CancellationToken, cancellationToken);

                listener.ConnectedTask
                    .TimeoutAfter(testLaunchTimeout)
                    .ContinueWith(testReporter.LaunchCallback)
                    .DoNotAwait();

                _mainLog.WriteLine("*** Executing {0}/{1} on device '{2}' ***", appInformation.AppName, target, deviceName);

                if (target.IsWatchOSTarget())
                {
                    args.Add(new AttachNativeDebuggerArgument()); // this prevents the watch from backgrounding the app.
                }
                else
                {
                    args.Add(new WaitForExitArgument());
                }

                args.Add(new DeviceNameArgument(deviceName));

                var deviceSystemLog = _logs.Create($"device-{deviceName}-{_helpers.Timestamp}.log", "Device log");
                var deviceLogCapturer = _deviceLogCapturerFactory.Create(_mainLog, deviceSystemLog, deviceName);
                deviceLogCapturer.StartCapture();

                try
                {
                    await crashReporter.StartCaptureAsync();

                    // create a tunnel to communicate with the device
                    if (transport == ListenerTransport.Tcp && _listenerFactory.UseTunnel && listener is SimpleTcpListener tcpListener)
                    {
                        // create a new tunnel using the listener
                        var tunnel = _listenerFactory.TunnelBore.Create(deviceName, _mainLog);
                        tunnel.Open(deviceName, tcpListener, timeout, _mainLog);
                        // wait until we started the tunnel
                        await tunnel.Started;
                    }

                    _mainLog.WriteLine("Starting test run");

                    // We need to check for MT1111 (which means that mlaunch won't wait for the app to exit).
                    var aggregatedLog = Log.CreateReadableAggregatedLog(_mainLog, testReporter.CallbackLog);
                    Task<ProcessExecutionResult> runTestTask = _processManager.ExecuteCommandAsync(
                        args,
                        aggregatedLog,
                        timeout,
                        cancellationToken: linkedCts.Token);

                    await testReporter.CollectDeviceResult(runTestTask);
                }
                finally
                {
                    deviceLogCapturer.StopCapture();
                    deviceSystemLog.Dispose();

                    // close a tunnel if it was created
                    if (!isSimulator && _listenerFactory.UseTunnel)
                    {
                        await _listenerFactory.TunnelBore.Close(deviceName);
                    }
                }

                // Upload the system log
                if (File.Exists(deviceSystemLog.FullPath))
                {
                    _mainLog.WriteLine("A capture of the device log is: {0}", deviceSystemLog.FullPath);
                }
            }

            listener.Cancel();
            listener.Dispose();

            // check the final status, copy all the required data
            var (testResult, resultMessage) = await testReporter.ParseResult();

            return (deviceName, testResult, resultMessage);
        }
    }
}
