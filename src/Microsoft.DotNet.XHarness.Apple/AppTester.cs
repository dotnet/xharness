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

namespace Microsoft.DotNet.XHarness.Apple
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
        private readonly IFileBackedLog _mainLog;
        private readonly ILogs _logs;
        private readonly IHelpers _helpers;

        public AppTester(
            IMlaunchProcessManager processManager,
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
            : base(processManager, hardwareDeviceLoader, captureLogFactory, logs, mainLog, logCallback)
        {
            _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
            _simulatorLoader = simulatorLoader ?? throw new ArgumentNullException(nameof(simulatorLoader));
            _listenerFactory = simpleListenerFactory ?? throw new ArgumentNullException(nameof(simpleListenerFactory));
            _snapshotReporterFactory = snapshotReporterFactory ?? throw new ArgumentNullException(nameof(snapshotReporterFactory));
            _captureLogFactory = captureLogFactory ?? throw new ArgumentNullException(nameof(captureLogFactory));
            _deviceLogCapturerFactory = deviceLogCapturerFactory ?? throw new ArgumentNullException(nameof(deviceLogCapturerFactory));
            _testReporterFactory = reporterFactory ?? throw new ArgumentNullException(nameof(reporterFactory));
            _resultParser = resultParser ?? throw new ArgumentNullException(nameof(resultParser));
            _mainLog = mainLog ?? throw new ArgumentNullException(nameof(mainLog));
            _logs = logs ?? throw new ArgumentNullException(nameof(logs));
            _helpers = helpers ?? throw new ArgumentNullException(nameof(helpers));
        }

        public async Task<(string DeviceName, TestExecutingResult Result, string ResultMessage)> TestApp(
            AppBundleInformation appInformation,
            TestTargetOs target,
            TimeSpan timeout,
            TimeSpan testLaunchTimeout,
            IEnumerable<string> extraAppArguments,
            IEnumerable<(string, string)> extraEnvVariables,
            string? deviceName = null,
            string? companionDeviceName = null,
            bool resetSimulator = false,
            int verbosity = 1,
            XmlResultJargon xmlResultJargon = XmlResultJargon.xUnit,
            string[]? skippedMethods = null,
            string[]? skippedTestClasses = null,
            CancellationToken cancellationToken = default)
        {
            var runMode = target.Platform.ToRunMode();
            var isSimulator = target.Platform.IsSimulator();

            var deviceListenerLog = _logs.Create($"test-{target.AsString()}-{_helpers.Timestamp}.log", LogType.TestLog.ToString(), timestamp: true);

            var (deviceListenerTransport, deviceListener, deviceListenerTmpFile) = _listenerFactory.Create(
                runMode,
                log: _mainLog,
                testLog: deviceListenerLog,
                isSimulator: isSimulator,
                autoExit: true,
                xmlOutput: true); // cli always uses xml

            if (target.Platform == TestTarget.MacCatalyst)
            {
                try
                {
                    var (catalystTestResult, catalystResultMessage) = await RunMacCatalystTests(
                        deviceListenerTransport,
                        deviceListener,
                        deviceListenerTmpFile,
                        appInformation,
                        timeout,
                        testLaunchTimeout,
                        xmlResultJargon,
                        skippedMethods,
                        skippedTestClasses,
                        extraAppArguments,
                        extraEnvVariables,
                        cancellationToken);

                    return ("MacCatalyst", catalystTestResult, catalystResultMessage);
                }
                finally
                {
                    deviceListener.Cancel();
                    deviceListener.Dispose();
                }
            }

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

            var deviceListenerPort = deviceListener.InitializeAndGetPort();
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
                        deviceListenerTmpFile,
                        extraAppArguments,
                        extraEnvVariables);

                    await RunSimulatorTests(
                        mlaunchArguments,
                        appInformation,
                        crashReporter,
                        testReporter,
                        simulator,
                        companionSimulator,
                        resetSimulator,
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
                        deviceListenerTmpFile,
                        extraAppArguments,
                        extraEnvVariables);

                    await RunDeviceTests(
                        mlaunchArguments,
                        crashReporter,
                        testReporter,
                        deviceListener,
                        deviceName,
                        timeout,
                        extraEnvVariables,
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
                await testReporter.CollectSimulatorResult(result);

                // Cleanup after us
                if (resetSimulator)
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
            IEnumerable<(string, string)> extraEnvVariables,
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

                var envVars = new Dictionary<string, string>();
                AddExtraEnvVars(envVars, extraEnvVariables);

                // We need to check for MT1111 (which means that mlaunch won't wait for the app to exit).
                var aggregatedLog = Log.CreateReadableAggregatedLog(_mainLog, testReporter.CallbackLog);
                var result = await _processManager.ExecuteCommandAsync(
                    mlaunchArguments,
                    aggregatedLog,
                    timeout,
                    envVars,
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

        /// <summary>
        /// Runs the MacCatalyst app by executing its binary (or if not found, via `open -W path.to.app`).
        /// </summary>
        private async Task<(TestExecutingResult Result, string ResultMessage)> RunMacCatalystTests(
            ListenerTransport deviceListenerTransport,
            ISimpleListener deviceListener,
            string deviceListenerTmpFile,
            AppBundleInformation appInformation,
            TimeSpan timeout,
            TimeSpan testLaunchTimeout,
            XmlResultJargon xmlResultJargon,
            string[]? skippedMethods,
            string[]? skippedTestClasses,
            IEnumerable<string> extraAppArguments,
            IEnumerable<(string, string)> extraEnvVariables,
            CancellationToken cancellationToken)
        {
            var deviceListenerPort = deviceListener.InitializeAndGetPort();
            deviceListener.StartAsync();

            var crashLogs = new Logs(_logs.Directory);

            ICrashSnapshotReporter crashReporter = _snapshotReporterFactory.Create(_mainLog, crashLogs, isDevice: false, null);
            ITestReporter testReporter = _testReporterFactory.Create(
                _mainLog,
                _mainLog,
                _logs,
                crashReporter,
                deviceListener,
                _resultParser,
                appInformation,
                RunMode.MacOS,
                xmlResultJargon,
                null,
                timeout,
                null,
                (level, message) => _mainLog.WriteLine(message));

            deviceListener.ConnectedTask
                .TimeoutAfter(testLaunchTimeout)
                .ContinueWith(testReporter.LaunchCallback)
                .DoNotAwait();

            _mainLog.WriteLine($"*** Executing '{appInformation.AppName}' on MacCatalyst ***");

            try
            {
                using var combinedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(testReporter.CancellationToken, cancellationToken);

                var envVariables = GetEnvVariables(
                    xmlResultJargon,
                    skippedMethods,
                    skippedTestClasses,
                    deviceListenerTransport,
                    deviceListenerPort,
                    deviceListenerTmpFile,
                    extraEnvVariables);

                envVariables[EnviromentVariables.HostName] = "127.0.0.1";

                AddExtraEnvVars(envVariables, extraEnvVariables);

                await crashReporter.StartCaptureAsync();

                var result = await RunMacCatalystApp(appInformation, timeout, extraAppArguments, envVariables, combinedCancellationToken.Token);
                await testReporter.CollectSimulatorResult(result);
            }
            finally
            {
                deviceListener.Cancel();
                deviceListener.Dispose();
            }

            return await testReporter.ParseResult();
        }

        private Dictionary<string, string> GetEnvVariables(
            XmlResultJargon xmlResultJargon,
            string[]? skippedMethods,
            string[]? skippedTestClasses,
            ListenerTransport listenerTransport,
            int listenerPort,
            string listenerTmpFile,
            IEnumerable<(string, string)> extraEnvVariables)
        {
            var variables = new Dictionary<string, string>
            {
                { EnviromentVariables.AutoExit, "true" },
                { EnviromentVariables.HostPort, listenerPort.ToString() },

                // Let the runner know that we want to get an XML output and not plain text
                {  EnviromentVariables.EnableXmlOutput, "true" },
                {  EnviromentVariables.XmlVersion, xmlResultJargon.ToString() },
            };

            if (skippedMethods?.Any() ?? skippedTestClasses?.Any() ?? false)
            {
                // Do not run all the tests, we are using filters
                variables.Add(EnviromentVariables.RunAllTestsByDefault, "false");

                // Add the skipped test classes and methods
                if (skippedMethods != null && skippedMethods.Length > 0)
                {
                    var skippedMethodsValue = string.Join(',', skippedMethods);
                    variables.Add(EnviromentVariables.SkippedMethods, skippedMethodsValue);
                }

                if (skippedTestClasses != null && skippedTestClasses!.Length > 0)
                {
                    var skippedClassesValue = string.Join(',', skippedTestClasses);
                    variables.Add(EnviromentVariables.SkippedClasses, skippedClassesValue);
                }
            }

            if (listenerTransport == ListenerTransport.File)
            {
                variables.Add(EnviromentVariables.LogFilePath, listenerTmpFile);
            }

            AddExtraEnvVars(variables, extraEnvVariables);

            return variables;
        }

        private MlaunchArguments GetCommonArguments(
            int verbosity,
            XmlResultJargon xmlResultJargon,
            string[]? skippedMethods,
            string[]? skippedTestClasses,
            ListenerTransport listenerTransport,
            int listenerPort,
            string listenerTmpFile,
            IEnumerable<string> extraAppArguments,
            IEnumerable<(string, string)> extraEnvVariables)
        {
            var args = new MlaunchArguments();

            for (var i = -1; i < verbosity; i++)
            {
                args.Add(new VerbosityArgument());
            }

            // Environment variables
            var envVariables = GetEnvVariables(
                xmlResultJargon,
                skippedMethods,
                skippedTestClasses,
                listenerTransport,
                listenerPort,
                listenerTmpFile,
                extraEnvVariables);

            // Variables passed through --set-env
            args.AddRange(envVariables.Select(pair => new SetEnvVariableArgument(pair.Key, pair.Value)));

            // Arguments passed to the iOS app bundle
            args.AddRange(extraAppArguments.Select(arg => new SetAppArgumentArgument(arg)));

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
            string deviceListenerTmpFile,
            IEnumerable<string> extraAppArguments,
            IEnumerable<(string, string)> extraEnvVariables)
        {
            var args = GetCommonArguments(
                verbosity,
                xmlResultJargon,
                skippedMethods,
                skippedTestClasses,
                deviceListenerTransport,
                deviceListenerPort,
                deviceListenerTmpFile,
                extraAppArguments,
                extraEnvVariables);

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
            string deviceListenerTmpFile,
            IEnumerable<string> extraAppArguments,
            IEnumerable<(string, string)> extraEnvVariables)
        {
            var args = GetCommonArguments(
                verbosity,
                xmlResultJargon,
                skippedMethods,
                skippedTestClasses,
                deviceListenerTransport,
                deviceListenerPort,
                deviceListenerTmpFile,
                extraAppArguments,
                extraEnvVariables);

            var ips = string.Join(",", _helpers.GetLocalIpAddresses().Select(ip => ip.ToString()));

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
