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
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Listeners;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

namespace Microsoft.DotNet.XHarness.iOS
{
    /// <summary>
    /// Class that will run an app bundle that contains the TestRunner on a target device.
    /// It will collect test results ran in the app and return results.
    /// </summary>
    public class AppTester : AppRunnerBase
    {
        private readonly IMlaunchProcessManager _processManager;
        private readonly ISimulatorLoader _simulatorLoader;
        private readonly ISimpleListenerFactory _listenerFactory;
        private readonly ICrashSnapshotReporterFactory _snapshotReporterFactory;
        private readonly ICaptureLogFactory _captureLogFactory;
        private readonly IDeviceLogCapturerFactory _deviceLogCapturerFactory;
        private readonly ITestReporterFactory _testReporterFactory;
        private readonly IResultParser _resultParser;
        private readonly ILogs _logs;
        private readonly IHelpers _helpers;
        private readonly IEnumerable<string> _appArguments; // Arguments that will be passed to the iOS application

        public AppTester(IMlaunchProcessManager processManager,
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
            IEnumerable<string> appArguments,
            Action<string>? logCallback = null)
            : base(hardwareDeviceLoader, mainLog, logCallback)
        {
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            _simulatorLoader = simulatorLoader ?? throw new ArgumentNullException(nameof(simulatorLoader));
            _listenerFactory = simpleListenerFactory ?? throw new ArgumentNullException(nameof(simpleListenerFactory));
            _snapshotReporterFactory = snapshotReporterFactory ?? throw new ArgumentNullException(nameof(snapshotReporterFactory));
            _captureLogFactory = captureLogFactory ?? throw new ArgumentNullException(nameof(captureLogFactory));
            _deviceLogCapturerFactory = deviceLogCapturerFactory ?? throw new ArgumentNullException(nameof(deviceLogCapturerFactory));
            _testReporterFactory = reporterFactory ?? throw new ArgumentNullException(nameof(_testReporterFactory));
            _resultParser = resultParser ?? throw new ArgumentNullException(nameof(resultParser));
            _logs = logs ?? throw new ArgumentNullException(nameof(logs));
            _helpers = helpers ?? throw new ArgumentNullException(nameof(helpers));
            _appArguments = appArguments;
        }

        public async Task<(string DeviceName, TestExecutingResult Result, string ResultMessage)> TestApp(
            AppBundleInformation appInformation,
            TestTargetOs target,
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
            var runMode = target.Platform.ToRunMode();
            bool isSimulator = target.Platform.IsSimulator();

            var deviceListenerLog = _logs.Create($"test-{target.AsString()}-{_helpers.Timestamp}.log", LogType.TestLog.ToString(), timestamp: true);
            var (deviceListenerTransport, deviceListener, deviceListenerTmpFile) = _listenerFactory.Create(
                runMode,
                log: _mainLog,
                testLog: deviceListenerLog,
                isSimulator: isSimulator,
                autoExit: true,
                xmlOutput: true); // cli always uses xml

            ISimulatorDevice? simulator = null;
            ISimulatorDevice? companionSimulator = null;

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

            int deviceListenerPort = deviceListener.InitializeAndGetPort();
            deviceListener.StartAsync();

            var crashLogs = new Logs(_logs.Directory);

            ICrashSnapshotReporter crashReporter = _snapshotReporterFactory.Create(_mainLog, crashLogs, isDevice: !isSimulator, deviceName);
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

            deviceListener.ConnectedTask
                .TimeoutAfter(testLaunchTimeout)
                .ContinueWith(testReporter.LaunchCallback)
                .DoNotAwait();

            _mainLog.WriteLine($"*** Executing '{appInformation.AppName}' on {target.AsString()} '{deviceName}' ***");

            try
            {
                using var combinedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(testReporter.CancellationToken, cancellationToken);

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
                        verbosity,
                        xmlResultJargon,
                        skippedMethods,
                        skippedTestClasses,
                        deviceListenerTransport,
                        deviceListenerPort,
                        deviceListenerTmpFile);

                    await RunSimulatorTests(
                        mlaunchArguments,
                        appInformation,
                        crashReporter,
                        testReporter,
                        simulator,
                        companionSimulator,
                        ensureCleanSimulatorState,
                        timeout,
                        combinedCancellationToken.Token);
                }
                else
                {
                    var mlaunchArguments = GetDeviceArguments(
                        appInformation,
                        deviceName,
                        target.Platform.IsWatchOSTarget(),
                        verbosity,
                        xmlResultJargon,
                        skippedMethods,
                        skippedTestClasses,
                        deviceListenerTransport,
                        deviceListenerPort,
                        deviceListenerTmpFile);

                    await RunDeviceTests(
                        mlaunchArguments,
                        crashReporter,
                        testReporter,
                        deviceListener,
                        deviceName,
                        timeout,
                        combinedCancellationToken.Token);
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
                    entireFile: false,
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
                        entireFile: false,
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
            var deviceSystemLog = _logs.Create($"device-{deviceName}-{_helpers.Timestamp}.log", LogType.SystemLog.ToString());
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

        private MlaunchArguments GetCommonArguments(
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

            // Arguments passed to the iOS app bundle
            args.AddRange(_appArguments.Select(arg => new SetAppArgumentArgument(arg, true)));

            return args;
        }

        private MlaunchArguments GetSimulatorArguments(
            AppBundleInformation appInformation,
            ISimulatorDevice simulator,
            int verbosity,
            XmlResultJargon xmlResultJargon,
            string[]? skippedMethods,
            string[]? skippedTestClasses,
            ListenerTransport deviceListenerTransport,
            int deviceListenerPort,
            string deviceListenerTmpFile)
        {
            var args = GetCommonArguments(
                verbosity,
                xmlResultJargon,
                skippedMethods,
                skippedTestClasses,
                deviceListenerTransport,
                deviceListenerPort,
                deviceListenerTmpFile);

            args.Add(new SetAppArgumentArgument("-hostname:127.0.0.1", true));
            args.Add(new SetEnvVariableArgument(EnviromentVariables.HostName, "127.0.0.1"));
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
            int verbosity,
            XmlResultJargon xmlResultJargon,
            string[]? skippedMethods,
            string[]? skippedTestClasses,
            ListenerTransport deviceListenerTransport,
            int deviceListenerPort,
            string deviceListenerTmpFile)
        {
            var args = GetCommonArguments(
                verbosity,
                xmlResultJargon,
                skippedMethods,
                skippedTestClasses,
                deviceListenerTransport,
                deviceListenerPort,
                deviceListenerTmpFile);

            var ips = string.Join(",", _helpers.GetLocalIpAddresses().Select(ip => ip.ToString()));

            args.Add(new SetAppArgumentArgument($"-hostname:{ips}", true));
            args.Add(new SetEnvVariableArgument(EnviromentVariables.HostName, ips));
            args.Add(new DisableMemoryLimitsArgument());
            args.Add(new DeviceNameArgument(deviceName));

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
