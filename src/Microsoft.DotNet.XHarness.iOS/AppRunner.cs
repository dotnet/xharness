using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution.Mlaunch;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Listeners;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

namespace Microsoft.DotNet.XHarness.iOS
{
    public class AppRunner
    {
        private readonly IProcessManager _processManager;
        private readonly IHardwareDeviceLoader _hardwareDeviceLoader;
        private readonly ISimulatorLoader _simulatorLoader;
        private readonly ISimpleListenerFactory _listenerFactory;
        private readonly ICrashSnapshotReporterFactory _snapshotReporterFactory;
        private readonly ICaptureLogFactory _captureLogFactory;
        private readonly IDeviceLogCapturerFactory _deviceLogCapturerFactory;
        private readonly ITestReporterFactory _testReporterFactory;
        private readonly ILogs _logs;
        private readonly ILog _mainLog;
        private readonly IHelpers _helpers;

        public AppRunner(IProcessManager processManager,
            IHardwareDeviceLoader hardwareDeviceLoader,
            ISimulatorLoader simulatorLoader,
            ISimpleListenerFactory simpleListenerFactory,
            ICrashSnapshotReporterFactory snapshotReporterFactory,
            ICaptureLogFactory captureLogFactory,
            IDeviceLogCapturerFactory deviceLogCapturerFactory,
            ITestReporterFactory reporterFactory,
            ILog mainLog,
            ILogs logs,
            IHelpers helpers)
        {
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            _hardwareDeviceLoader = hardwareDeviceLoader ?? throw new ArgumentNullException(nameof(hardwareDeviceLoader));
            _simulatorLoader = simulatorLoader ?? throw new ArgumentNullException(nameof(simulatorLoader));
            _listenerFactory = simpleListenerFactory ?? throw new ArgumentNullException(nameof(simpleListenerFactory));
            _snapshotReporterFactory = snapshotReporterFactory ?? throw new ArgumentNullException(nameof(snapshotReporterFactory));
            _captureLogFactory = captureLogFactory ?? throw new ArgumentNullException(nameof(captureLogFactory));
            _deviceLogCapturerFactory = deviceLogCapturerFactory ?? throw new ArgumentNullException(nameof(deviceLogCapturerFactory));
            _testReporterFactory = reporterFactory ?? throw new ArgumentNullException(nameof(_testReporterFactory));
            _mainLog = mainLog ?? throw new ArgumentNullException(nameof(mainLog));
            _logs = logs ?? throw new ArgumentNullException(nameof(logs));
            _helpers = helpers ?? throw new ArgumentNullException(nameof(helpers));
        }

        public async Task<(string deviceName, int exitCode)> RunApp(
            AppBundleInformation appInformation,
            TestTarget target,
            TimeSpan timeout,
            TimeSpan testLaunchTimeout,
            string deviceName = null,
            string companionDeviceName = null,
            bool ensureCleanSimulatorState = false,
            string variation = null,
            int verbosity = 1,
            XmlResultJargon xmlResultJargon = XmlResultJargon.xUnit)
        {
            var args = new MlaunchArguments
            {
                new SetAppArgumentArgument("-connection-mode"),
                new SetAppArgumentArgument("none"), // This will prevent the app from trying to connect to any IDEs
                new SetAppArgumentArgument("-autostart", true),
                new SetEnvVariableArgument("NUNIT_AUTOSTART", true),
                new SetAppArgumentArgument("-autoexit", true),
                new SetEnvVariableArgument("NUNIT_AUTOEXIT", true),
                new SetAppArgumentArgument("-enablenetwork", true),
                new SetEnvVariableArgument("NUNIT_ENABLE_NETWORK", true),
                
                // On macOS we can't edit the TCC database easily
                // (it requires adding the mac has to be using MDM: https://carlashley.com/2018/09/28/tcc-round-up/)
                // So by default ignore any tests that would pop up permission dialogs in CI.
                new SetEnvVariableArgument("DISABLE_SYSTEM_PERMISSION_TESTS", 1),
            };

            for (int i = -1; i < verbosity; i++)
            {
                args.Add(new VerbosityArgument());
            }

            var isSimulator = target.IsSimulator();

            if (isSimulator)
            {
                args.Add(new SetAppArgumentArgument("-hostname:127.0.0.1", true));
                args.Add(new SetEnvVariableArgument("NUNIT_HOSTNAME", "127.0.0.1"));
            }
            else
            {
                var ipAddresses = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList.Select(ip => ip.ToString());
                var ips = string.Join(",", ipAddresses);
                args.Add(new SetAppArgumentArgument($"-hostname:{ips}", true));
                args.Add(new SetEnvVariableArgument("NUNIT_HOSTNAME", ips));
            }

            var listener_log = _logs.Create($"test-{target.AsString()}-{_helpers.Timestamp}.log", LogType.TestLog.ToString(), timestamp: true);
            var (transport, listener, listenerTmpFile) = _listenerFactory.Create(target.ToRunMode(), _mainLog, listener_log, isSimulator, true, false);

            // Initialize has to be called before we try to get Port (internal implementation of the listener says so)
            // TODO: Improve this to not get into a broken state - it was really hard to debug when I moved this lower
            listener.Initialize();

            args.Add(new SetAppArgumentArgument($"-transport:{transport}", true));
            args.Add(new SetEnvVariableArgument("NUNIT_TRANSPORT", transport.ToString().ToUpper()));

            if (transport == ListenerTransport.File)
            {
                args.Add(new SetEnvVariableArgument("NUNIT_LOG_FILE", listenerTmpFile));
            }

            args.Add(new SetAppArgumentArgument($"-hostport:{listener.Port}", true));
            args.Add(new SetEnvVariableArgument("NUNIT_HOSTPORT", listener.Port));

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
            if (!isSimulator)
            {
                args.Add(new DisableMemoryLimitsArgument());
            }

            var runMode = target.ToRunMode();
            ICrashSnapshotReporter crashReporter;
            ITestReporter testReporter;

            if (isSimulator)
            {
                crashReporter = _snapshotReporterFactory.Create(_mainLog, crashLogs, isDevice: !isSimulator, deviceName: null);
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

                if (!target.IsWatchOSTarget())
                {
                    var stderr_tty = _helpers.GetTerminalName(2);
                    if (!string.IsNullOrEmpty(stderr_tty))
                    {
                        args.Add(new SetStdoutArgument(stderr_tty));
                        args.Add(new SetStderrArgument(stderr_tty));
                    }
                    else
                    {
                        var stdout_log = _logs.CreateFile($"stdout-{_helpers.Timestamp}.log", "Standard output");
                        var stderr_log = _logs.CreateFile($"stderr-{_helpers.Timestamp}.log", "Standard error");
                        args.Add(new SetStdoutArgument(stdout_log));
                        args.Add(new SetStderrArgument(stderr_log));
                    }
                }

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

                var result = _processManager.ExecuteCommandAsync(args, _mainLog, timeout, cancellation_token: testReporter.CancellationToken);

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
            else
            {
                if (deviceName == null)
                {
                    IHardwareDevice companionDevice = null;
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

                    _mainLog.WriteLine("Starting test run");

                    // We need to check for MT1111 (which means that mlaunch won't wait for the app to exit).
                    var aggregatedLog = Log.CreateAggregatedLog(testReporter.CallbackLog, _mainLog);
                    Task<ProcessExecutionResult> runTestTask = _processManager.ExecuteCommandAsync(
                        args,
                        aggregatedLog,
                        timeout,
                        cancellation_token: testReporter.CancellationToken);

                    await testReporter.CollectDeviceResult(runTestTask);
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

            listener.Cancel();
            listener.Dispose();

            // check the final status, copy all the required data
            await testReporter.ParseResult();

            return (deviceName, testReporter.Success.Value ? 0 : 1);
        }
    }
}
