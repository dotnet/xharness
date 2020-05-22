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
        private readonly IResultParser _resultParser;
        private readonly ILogs _logs;
        private readonly IFileBackedLog _mainLog;
        private readonly IHelpers _helpers;

        public AppRunner(IMLaunchProcessManager processManager,
            IHardwareDeviceLoader hardwareDeviceLoader,
            ISimulatorLoader simulatorLoader,
            ISimpleListenerFactory simpleListenerFactory,
            ICrashSnapshotReporterFactory snapshotReporterFactory,
            ICaptureLogFactory captureLogFactory,
            IDeviceLogCapturerFactory deviceLogCapturerFactory,
            ITestReporterFactory reporterFactory,
            IResultParser resultParser,
            IFileBackedLog mainLog,
            ILogs logs,
            IHelpers helpers,
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
            _resultParser = resultParser ?? throw new ArgumentNullException(nameof(resultParser));
            _logs = logs ?? throw new ArgumentNullException(nameof(logs));
            _helpers = helpers ?? throw new ArgumentNullException(nameof(helpers));

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
            string[]? skippedMethods = null,
            string[]? skippedTestClasses = null,
            CancellationToken cancellationToken = default)
        {
            var runMode = target.ToRunMode();
            var isSimulator = target.IsSimulator();

            var deviceListenerLog = _logs.Create($"test-{target.AsString()}-{_helpers.Timestamp}.log", LogType.TestLog.ToString(), timestamp: true);
            var (deviceListenerTransport, deviceListener, deviceListenerTmpFile) = _listenerFactory.Create(
                target.ToRunMode(),
                log: _mainLog,
                testLog: deviceListenerLog,
                isSimulator: isSimulator,
                autoExit: true,
                xmlOutput: true); // cli always uses xml

            var listenerPort = deviceListener.InitializeAndGetPort();

            var mlaunchArguments = GetCommonArguments(
                verbosity,
                xmlResultJargon,
                skippedMethods,
                skippedTestClasses,
                deviceListenerTransport,
                listenerPort,
                deviceListenerTmpFile);

            ISimulatorDevice? simulator = null;
            ISimulatorDevice? companionSimulator = null;

            if (isSimulator)
            {
                IFileBackedLog simulatorLoadingLogs = _logs.Create($"simulator-list-{_helpers.Timestamp}.log", "Simulator list");

                try
                {
                    await _simulatorLoader.LoadDevices(simulatorLoadingLogs, false, false);
                }
                catch
                {
                    _mainLog.WriteLine("Failed to load simulators!");
                    throw;
                }

                try
                {
                    (simulator, companionSimulator) = await _simulatorLoader.FindSimulators(target, _mainLog);
                    deviceName = simulator.Name;
                }
                catch (NoDeviceFoundException e)
                {
                    _mainLog.WriteLine($"Didn't find any suitable simulator: {e.Message}");
                    throw;
                }

                mlaunchArguments.AddRange(GetSimulatorArguments(appInformation, simulator));
            }
            else
            {
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

                mlaunchArguments.AddRange(GetDeviceArguments(appInformation, deviceName, target.IsWatchOSTarget()));
            }

            deviceListener.StartAsync();

            var crashLogs = new Logs(_logs.Directory);

            ICrashSnapshotReporter crashReporter = _snapshotReporterFactory.Create(_mainLog, crashLogs, isDevice: !isSimulator, deviceName!); // TODO: nullability
            ITestReporter testReporter = _testReporterFactory.Create(_mainLog,
                _mainLog,
                _logs,
                crashReporter,
                deviceListener,
                _resultParser,
                appInformation,
                runMode,
                xmlResultJargon,
                deviceName,
                timeout,
                null,
                (level, message) => _mainLog.WriteLine(message));

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(testReporter.CancellationToken, cancellationToken);

            deviceListener.ConnectedTask
                .TimeoutAfter(testLaunchTimeout)
                .ContinueWith(testReporter.LaunchCallback)
                .DoNotAwait();

            _mainLog.WriteLine($"*** Executing {appInformation.AppName} on {target} '{deviceName}' ***");

            try
            {
                if (isSimulator)
                {
                    if (simulator == null)
                    {
                        _mainLog.WriteLine("Didn't find any suitable simulator");
                        throw new NoDeviceFoundException();
                    }

                    await RunSimulatorTests(
                        mlaunchArguments,
                        appInformation,
                        crashReporter,
                        testReporter,
                        simulator,
                        companionSimulator,
                        ensureCleanSimulatorState,
                        timeout,
                        cancellationToken);
                }
                else
                {
                    await RunDeviceTests(
                        mlaunchArguments,
                        crashReporter,
                        testReporter,
                        deviceListener,
                        deviceName,
                        timeout,
                        cancellationToken);
                }
            }
            finally
            {
                deviceListener.Cancel();
                deviceListener.Dispose();
            }

            // Check the final status, copy all the required data
            var (testResult, resultMessage) = await testReporter.ParseResult();

            return (deviceName, testResult, resultMessage);
        }

        private async Task RunSimulatorTests(
            MlaunchArguments mlaunchArguments,
            AppBundleInformation appInformation,
            ICrashSnapshotReporter crashReporter,
            ITestReporter testReporter,
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

                    if (companionSimulator != null) {
                        await companionSimulator.PrepareSimulator(_mainLog, appInformation.BundleIdentifier);
                    }
                }

                await crashReporter.StartCaptureAsync();

                _mainLog.WriteLine("Starting test run");

                var result = _processManager.ExecuteCommandAsync(mlaunchArguments, _mainLog, timeout, cancellationToken: cancellationToken);

                await testReporter.CollectSimulatorResult(result);

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

        private async Task RunDeviceTests(
            MlaunchArguments mlaunchArguments,
            ICrashSnapshotReporter crashReporter,
            ITestReporter testReporter,
            ISimpleListener deviceListener,
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

                // create a tunnel to communicate with the device
                if (_listenerFactory.UseTunnel && deviceListener is SimpleTcpListener tcpListener)
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
                var result = _processManager.ExecuteCommandAsync(
                    mlaunchArguments,
                    aggregatedLog,
                    timeout,
                    cancellationToken: cancellationToken);

                await testReporter.CollectDeviceResult(result);
            }
            finally
            {
                deviceLogCapturer.StopCapture();
                deviceSystemLog.Dispose();

                // close a tunnel if it was created
                if (_listenerFactory.UseTunnel)
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

        private static MlaunchArguments GetCommonArguments(
            int verbosity,
            XmlResultJargon xmlResultJargon,
            string[]? skippedMethods,
            string[]? skippedTestClasses,
            ListenerTransport listenerTransport,
            int listenerPort,
            string listenerTmpFile)
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

            if (skippedMethods?.Any() ?? skippedTestClasses?.Any() ?? false)
            {
                // do not run all the tests, we are using filters
                args.Add(new SetEnvVariableArgument(EnviromentVariables.RunAllTestsByDefault, false));

                // add the skipped test classes and methods
                if (skippedMethods != null && skippedMethods.Length > 0)
                {
                    var skippedMethodsValue = string.Join(',', skippedMethods);
                    args.Add(new SetEnvVariableArgument(EnviromentVariables.SkippedMethods, skippedMethodsValue));
                }

                if (skippedTestClasses != null && skippedTestClasses!.Length > 0)
                {
                    var skippedClassesValue = string.Join(',', skippedTestClasses);
                    args.Add(new SetEnvVariableArgument(EnviromentVariables.SkippedClasses, skippedClassesValue));
                }
            }

            for (int i = -1; i < verbosity; i++)
            {
                args.Add(new VerbosityArgument());
            }

            // let the runner now via envars that we want to get a xml output, else the runner will default to plain text
            args.Add(new SetEnvVariableArgument(EnviromentVariables.EnableXmlOutput, true));
            args.Add(new SetEnvVariableArgument(EnviromentVariables.XmlMode, "wrapped"));
            args.Add(new SetEnvVariableArgument(EnviromentVariables.XmlVersion, $"{xmlResultJargon}"));
            args.Add(new SetAppArgumentArgument($"-transport:{listenerTransport}", true));
            args.Add(new SetEnvVariableArgument(EnviromentVariables.Transport, listenerTransport.ToString().ToUpper()));

            if (listenerTransport == ListenerTransport.File)
            {
                args.Add(new SetEnvVariableArgument(EnviromentVariables.LogFilePath, listenerTmpFile));
            }

            args.Add(new SetAppArgumentArgument($"-hostport:{listenerPort}", true));
            args.Add(new SetEnvVariableArgument(EnviromentVariables.HostPort, listenerPort));

            return args;
        }

        private IEnumerable<MlaunchArgument> GetSimulatorArguments(AppBundleInformation appInformation, ISimulatorDevice simulator)
        {
            var args = new List<MlaunchArgument>
            {
                new SetAppArgumentArgument("-hostname:127.0.0.1", true),
                new SetEnvVariableArgument(EnviromentVariables.HostName, "127.0.0.1"),
                new SimulatorUDIDArgument(simulator.UDID),
            };

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

        private IEnumerable<MlaunchArgument> GetDeviceArguments(AppBundleInformation appInformation, string deviceName, bool isWatchTarget)
        {
            var ipAddresses = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList.Select(ip => ip.ToString());
            var ips = string.Join(",", ipAddresses);

            var args = new List<MlaunchArgument>
            {
                new SetAppArgumentArgument($"-hostname:{ips}", true),
                new SetEnvVariableArgument(EnviromentVariables.HostName, ips),
                new DisableMemoryLimitsArgument(),
                new DeviceNameArgument(deviceName),
            };

            if (_listenerFactory.UseTunnel)
            {
                args.Add(new SetEnvVariableArgument(EnviromentVariables.UseTcpTunnel, true));
            }

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
