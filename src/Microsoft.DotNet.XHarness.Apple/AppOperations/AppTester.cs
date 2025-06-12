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

namespace Microsoft.DotNet.XHarness.Apple;

public interface IAppTester
{
    bool ListenerConnected { get; }

    Task<(TestExecutingResult Result, string ResultMessage)> TestApp(
        AppBundleInformation appInformation,
        TestTargetOs target,
        IDevice device,
        IDevice? companionDevice,
        TimeSpan timeout,
        TimeSpan testLaunchTimeout,
        bool signalAppEnd,
        IEnumerable<string> extraAppArguments,
        IEnumerable<(string, string)> extraEnvVariables,
        XmlResultJargon xmlResultJargon = XmlResultJargon.xUnit,
        string[]? skippedMethods = null,
        string[]? skippedTestClasses = null,
        CancellationToken cancellationToken = default);

    Task<(TestExecutingResult Result, string ResultMessage)> TestMacCatalystApp(
        AppBundleInformation appInformation,
        TimeSpan timeout,
        TimeSpan testLaunchTimeout,
        bool signalAppEnd,
        IEnumerable<string> extraAppArguments,
        IEnumerable<(string, string)> extraEnvVariables,
        XmlResultJargon xmlResultJargon = XmlResultJargon.xUnit,
        string[]? skippedMethods = null,
        string[]? skippedTestClasses = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Class that will run an app bundle that contains the TestRunner on a given simulator/device.
/// It will collect test results ran in the app and return results.
/// </summary>
public class AppTester : AppRunnerBase, IAppTester
{
    private readonly IMlaunchProcessManager _processManager;
    private readonly ISimpleListenerFactory _listenerFactory;
    private readonly ICrashSnapshotReporterFactory _snapshotReporterFactory;
    private readonly IDeviceLogCapturerFactory _deviceLogCapturerFactory;
    private readonly ITestReporterFactory _testReporterFactory;
    private readonly IResultParser _resultParser;
    private readonly IFileBackedLog _mainLog;
    private readonly ILogs _logs;
    private readonly IHelpers _helpers;

    /// <summary>
    /// Denotes whether we had a successful connection over TCP during the run.
    /// This is used later to determine if a cause for a failed run is a failing TCP connection.
    /// </summary>
    public bool ListenerConnected { get; private set; }

    public AppTester(
        IMlaunchProcessManager processManager,
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
        : base(processManager, captureLogFactory, logs, mainLog, helpers, logCallback)
    {
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _listenerFactory = simpleListenerFactory ?? throw new ArgumentNullException(nameof(simpleListenerFactory));
        _snapshotReporterFactory = snapshotReporterFactory ?? throw new ArgumentNullException(nameof(snapshotReporterFactory));
        _deviceLogCapturerFactory = deviceLogCapturerFactory ?? throw new ArgumentNullException(nameof(deviceLogCapturerFactory));
        _testReporterFactory = reporterFactory ?? throw new ArgumentNullException(nameof(reporterFactory));
        _resultParser = resultParser ?? throw new ArgumentNullException(nameof(resultParser));
        _mainLog = mainLog ?? throw new ArgumentNullException(nameof(mainLog));
        _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        _helpers = helpers ?? throw new ArgumentNullException(nameof(helpers));
    }

    public async Task<(TestExecutingResult Result, string ResultMessage)> TestMacCatalystApp(
        AppBundleInformation appInformation,
        TimeSpan timeout,
        TimeSpan testLaunchTimeout,
        bool signalAppEnd,
        IEnumerable<string> extraAppArguments,
        IEnumerable<(string, string)> extraEnvVariables,
        XmlResultJargon xmlResultJargon = XmlResultJargon.xUnit,
        string[]? skippedMethods = null,
        string[]? skippedTestClasses = null,
        CancellationToken cancellationToken = default)
    {
        var testLog = _logs.Create($"test-{TestTarget.MacCatalyst.AsString()}-{_helpers.Timestamp}.log", LogType.TestLog.ToString(), timestamp: false);
        var appOutputLog = _logs.Create(appInformation.BundleIdentifier + ".log", LogType.ApplicationLog.ToString(), timestamp: true);

        ListenerConnected = false;
        var (deviceListenerTransport, deviceListener, deviceListenerTmpFile) = _listenerFactory.Create(
            RunMode.MacOS,
            log: _mainLog,
            testLog: testLog,
            isSimulator: true,
            autoExit: true,
            xmlOutput: true);

        string? appEndTag = null;
        if (signalAppEnd)
        {
            WatchForAppEndTag(out appEndTag, ref appOutputLog, ref cancellationToken);
        }

        using (testLog)
        using (deviceListener)
        {
            var (catalystTestResult, catalystResultMessage) = await RunMacCatalystTests(
                deviceListenerTransport,
                deviceListener,
                deviceListenerTmpFile,
                appInformation,
                appOutputLog,
                timeout,
                testLaunchTimeout,
                xmlResultJargon,
                skippedMethods,
                skippedTestClasses,
                extraAppArguments,
                extraEnvVariables,
                appEndTag,
                cancellationToken);

            return (catalystTestResult, catalystResultMessage);
        }
    }

    public async Task<(TestExecutingResult Result, string ResultMessage)> TestApp(
        AppBundleInformation appInformation,
        TestTargetOs target,
        IDevice device,
        IDevice? companionDevice,
        TimeSpan timeout,
        TimeSpan testLaunchTimeout,
        bool signalAppEnd,
        IEnumerable<string> extraAppArguments,
        IEnumerable<(string, string)> extraEnvVariables,
        XmlResultJargon xmlResultJargon = XmlResultJargon.xUnit,
        string[]? skippedMethods = null,
        string[]? skippedTestClasses = null,
        CancellationToken cancellationToken = default)
    {
        var runMode = target.Platform.ToRunMode();
        var isSimulator = target.Platform.IsSimulator();

        var testLog = _logs.Create($"test-{target.AsString()}-{_helpers.Timestamp}.log", LogType.TestLog.ToString(), timestamp: false);

        ListenerConnected = false;
        var (deviceListenerTransport, deviceListener, deviceListenerTmpFile) = _listenerFactory.Create(
            runMode,
            log: _mainLog,
            testLog: testLog,
            isSimulator: isSimulator,
            autoExit: true,
            xmlOutput: true); // cli always uses xml

        using (testLog)
        using (deviceListener)
        {
            var deviceListenerPort = deviceListener.InitializeAndGetPort();
            deviceListener.StartAsync();

            using var crashLogs = new Logs(_logs.Directory);

            ICrashSnapshotReporter crashReporter = _snapshotReporterFactory.Create(_mainLog, crashLogs, isDevice: !isSimulator, device.Name);
            using ITestReporter testReporter = _testReporterFactory.Create(
                _mainLog,
                _mainLog,
                _logs,
                crashReporter,
                deviceListener,
                _resultParser,
                appInformation,
                runMode,
                xmlResultJargon,
                device.Name,
                timeout,
                null,
                (level, message) => _mainLog.WriteLine(message));

            deviceListener.ConnectedTask
                .TimeoutAfter(testLaunchTimeout)
                .ContinueWith(async (Task<bool> task) =>
                {
                    testReporter.LaunchCallback(task);

                    // Stop listening so that TCP doesn't get connected before here and when we evaluate why we failed
                    // If no TCP happens, app didn't start in time => APP_LAUNCH_TIMEOUT
                    // If TCP connects during this method or right after - a very narrow race condition - we would categorize it as TIMED_OUT
                    // because we would consider the app run started and actually timing out.
                    if (!deviceListener.ConnectedTask.IsCompleted)
                    {
                        await deviceListener.StopAsync();
                    }
                    else if (task.IsCompleted && task.Result)
                    {
                        ListenerConnected = true;
                    }
                }, cancellationToken)
                .DoNotAwait();

            _mainLog.WriteLine($"*** Executing '{appInformation.AppName}' on {target.AsString()} '{device.Name}' ***");

            using var combinedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(
                testReporter.CancellationToken,
                cancellationToken);
            cancellationToken = combinedCancellationToken.Token;

            if (isSimulator)
            {
                var mlaunchArguments = GetSimulatorArguments(
                    appInformation,
                    device,
                    xmlResultJargon,
                    skippedMethods,
                    skippedTestClasses,
                    deviceListenerTransport,
                    deviceListenerPort,
                    deviceListenerTmpFile,
                    extraAppArguments,
                    extraEnvVariables);

                await RunSimulatorTests(
                    appInformation,
                    mlaunchArguments,
                    crashReporter,
                    testReporter,
                    deviceListener,
                    (ISimulatorDevice)device,
                    companionDevice as ISimulatorDevice,
                    timeout,
                    cancellationToken,
                    runMode);
            }
            else
            {
                var appOutputLog = _logs.Create(appInformation.BundleIdentifier + ".log", LogType.ApplicationLog.ToString(), timestamp: true);
                string? appEndTag = null;
                if (signalAppEnd)
                {
                    WatchForAppEndTag(out appEndTag, ref appOutputLog, ref cancellationToken);
                }

                using (appOutputLog)
                {
                    var mlaunchArguments = GetDeviceArguments(
                        appInformation,
                        device,
                        target.Platform.IsWatchOSTarget(),
                        xmlResultJargon,
                        skippedMethods,
                        skippedTestClasses,
                        deviceListenerTransport,
                        deviceListenerPort,
                        deviceListenerTmpFile,
                        extraAppArguments,
                        extraEnvVariables,
                        appEndTag);

                    await RunDeviceTests(
                        appInformation,
                        mlaunchArguments,
                        crashReporter,
                        testReporter,
                        deviceListener,
                        device,
                        appOutputLog,
                        timeout,
                        extraEnvVariables,
                        cancellationToken,
                        runMode);
                }
            }

            // Check the final status, copy all the required data
            return await testReporter.ParseResult();
        }
    }

    private async Task RunSimulatorTests(
        AppBundleInformation appInformation,
        MlaunchArguments mlaunchArguments,
        ICrashSnapshotReporter crashReporter,
        ITestReporter testReporter,
        ISimpleListener deviceListener,
        ISimulatorDevice simulator,
        ISimulatorDevice? companionSimulator,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        RunMode runMode)
    {
        var result = await RunSimulatorApp(
            appInformation,
            mlaunchArguments,
            crashReporter,
            simulator,
            companionSimulator,
            timeout,
            waitForExit: true,
            cancellationToken);

        await testReporter.CollectSimulatorResult(result);
        if (simulator.OSVersion == null)
        {
            _mainLog.WriteLine("Simulator OS version is not set, skipping result copying.");
            return;
        }

        var osVersionParts = simulator.OSVersion.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (osVersionParts.Length < 2)
        {
            _mainLog.WriteLine("Simulator OS version is not in the expected format, skipping result copying.");
            return;
        }

        bool versionParsed = Version.TryParse(osVersionParts[1], out var osVersion);

        // On iOS 18 and later, transferring results over a TCP tunnel isn’t supported.
        // Instead, copy the results file from the device to the host machine.
        _mainLog.WriteLine("Copying test results from simulator...");
        _mainLog.WriteLine($"Simulator OS version: {osVersion}");
        if (versionParsed && osVersion!.Major >= 18)
        {
            try
            {
                var resultsFilePathOnDevice = runMode == RunMode.iOS
                    ? "/Documents/test-results.xml"
                    : "/Library/Caches/Documents/test-results.xml";
                var resultsFilePathOnHost = deviceListener.TestLog.FullPath;
                var simCtlCmd = $"cp \"$(xcrun simctl get_app_container {simulator.UDID} {appInformation.BundleIdentifier} data){resultsFilePathOnDevice}\" \"{resultsFilePathOnHost}\"";

                var _ = await _processManager.ExecuteCommandAsync(
                    "/bin/bash",
                    new[] { "-c", simCtlCmd },
                    _mainLog,
                    _mainLog,
                    _mainLog,
                    TimeSpan.FromMinutes(1),
                    null,
                    cancellationToken: cancellationToken);

                if (File.Exists(resultsFilePathOnHost))
                {
                    _mainLog.WriteLine($"Test results copied from simulator to {resultsFilePathOnHost}");
                }
                else
                {
                    _mainLog.WriteLine($"Failed to copy test results from simulator");
                }
            }
            catch (Exception ex)
            {
                _mainLog.WriteLine($"Exception while copying test results from simulator: {ex}");
                throw;
            }
        }
    }

    private async Task RunDeviceTests(
        AppBundleInformation appInformation,
        MlaunchArguments mlaunchArguments,
        ICrashSnapshotReporter crashReporter,
        ITestReporter testReporter,
        ISimpleListener deviceListener,
        IDevice device,
        ILog appOutputLog,
        TimeSpan timeout,
        IEnumerable<(string, string)> extraEnvVariables,
        CancellationToken cancellationToken,
        RunMode runMode)
    {
        var deviceSystemLog = _logs.Create($"device-{device.Name}-{_helpers.Timestamp}.log", LogType.SystemLog.ToString());
        deviceSystemLog.Timestamp = false;

        var deviceLogCapturer = _deviceLogCapturerFactory.Create(_mainLog, deviceSystemLog, device.Name);
        deviceLogCapturer.StartCapture();

        bool versionParsed = Version.TryParse(device.OSVersion, out var osVersion);

        try
        {
            await crashReporter.StartCaptureAsync();

            // create a tunnel to communicate with the device
            if (_listenerFactory.UseTunnel && deviceListener is SimpleTcpListener tcpListener)
            {
                // create a new tunnel using the listener
                var tunnel = _listenerFactory.TunnelBore.Create(device.UDID, _mainLog);
                tunnel.Open(device.UDID, tcpListener, timeout, _mainLog);
                // wait until we started the tunnel
                await tunnel.Started;
            }

            _mainLog.WriteLine("Starting the application");

            var envVars = new Dictionary<string, string>();
            AddExtraEnvVars(envVars, extraEnvVariables);

            // We need to check for MT1111 (which means that mlaunch won't wait for the app to exit)
            IFileBackedLog aggregatedLog = Log.CreateReadableAggregatedLog(_mainLog, testReporter.CallbackLog);


            var result = await RunAndWatchForAppSignal(() => _processManager.ExecuteCommandAsync(
                mlaunchArguments,
                aggregatedLog,
                appOutputLog,
                appOutputLog,
                timeout,
                envVars,
                cancellationToken: cancellationToken));

            await testReporter.CollectDeviceResult(result);
        }
        finally
        {
            deviceLogCapturer.StopCapture();
            deviceSystemLog.Dispose();

            // close a tunnel if it was created
            if (_listenerFactory.UseTunnel)
            {
                await _listenerFactory.TunnelBore.Close(device.UDID);
            }
        }

        // Upload the system log
        if (File.Exists(deviceSystemLog.FullPath))
        {
            _mainLog.WriteLine("Device log captured in {0}", deviceSystemLog.FullPath);
        }

        // On iOS 18 and later, transferring results over a TCP tunnel isn’t supported.
        // Instead, copy the results file from the device to the host machine.
        if (versionParsed && osVersion!.Major >= 18)
        {
            var resultsFilePathOnDevice = runMode == RunMode.iOS
                ? "/Documents/test-results.xml"
                : "/Library/Caches/Documents/test-results.xml";
            var resultsFilePathOnHost = deviceListener.TestLog.FullPath;
            var devicectlCmd = $"xcrun devicectl device copy from --device {device.UDID} --source {resultsFilePathOnDevice} --destination {resultsFilePathOnHost} --domain-type appDataContainer --domain-identifier {appInformation.BundleIdentifier}";
            try
            {
                var result = await _processManager.ExecuteCommandAsync(
                    "/bin/bash",
                    new List<string> { "-c", devicectlCmd },
                    _mainLog,
                    _mainLog,
                    _mainLog,
                    TimeSpan.FromMinutes(1),
                    null,
                    cancellationToken: cancellationToken);
                if (File.Exists(resultsFilePathOnHost))
                {
                    _mainLog.WriteLine($"Test results copied from device to {resultsFilePathOnHost}");
                }
                else
                {
                    _mainLog.WriteLine($"Failed to copy test results from device");
                }
            }
            catch (Exception ex)
            {
                _mainLog.WriteLine($"Exception while copying test results from device: {ex}");
                throw;
            }
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
        ILog appOutputLog,
        TimeSpan timeout,
        TimeSpan testLaunchTimeout,
        XmlResultJargon xmlResultJargon,
        string[]? skippedMethods,
        string[]? skippedTestClasses,
        IEnumerable<string> extraAppArguments,
        IEnumerable<(string, string)> extraEnvVariables,
        string? appEndTag,
        CancellationToken cancellationToken)
    {
        var deviceListenerPort = deviceListener.InitializeAndGetPort();
        deviceListener.StartAsync();

        using var crashLogs = new Logs(_logs.Directory);

        ICrashSnapshotReporter crashReporter = _snapshotReporterFactory.Create(_mainLog, crashLogs, isDevice: false, null);
        using ITestReporter testReporter = _testReporterFactory.Create(
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
            .ContinueWith(async (Task<bool> task) =>
            {
                testReporter.LaunchCallback(task);

                // Stop listening so that TCP doesn't get connected before here and when we evaluate why we failed
                // If no TCP happens, app didn't start in time => APP_LAUNCH_TIMEOUT
                // If TCP connects during this method or right after - a very narrow race condition - we would categorize it as TIMED_OUT
                // because we would consider the app run started and actually timing out.
                if (!deviceListener.ConnectedTask.IsCompleted)
                {
                    await deviceListener.StopAsync();
                }
                else if (task.IsCompleted && task.Result)
                {
                    ListenerConnected = true;
                }
            }, cancellationToken)
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
                extraEnvVariables,
                appEndTag);

            envVariables[EnviromentVariables.HostName] = "127.0.0.1";

            AddExtraEnvVars(envVariables, extraEnvVariables);

            await crashReporter.StartCaptureAsync();

            var result = await RunMacCatalystApp(appInformation, appOutputLog, timeout, waitForExit: true, extraAppArguments, envVariables, combinedCancellationToken.Token);
            await testReporter.CollectSimulatorResult(result);
        }
        finally
        {
            deviceListener.Cancel();
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
        IEnumerable<(string, string)> extraEnvVariables,
        string? appEndTag)
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

        if (appEndTag != null)
        {
            variables.Add(EnviromentVariables.AppEndTag, appEndTag);
        }

        AddExtraEnvVars(variables, extraEnvVariables);

        return variables;
    }

    private MlaunchArguments GetCommonArguments(
        XmlResultJargon xmlResultJargon,
        string[]? skippedMethods,
        string[]? skippedTestClasses,
        ListenerTransport listenerTransport,
        int listenerPort,
        string listenerTmpFile,
        IEnumerable<string> extraAppArguments,
        IEnumerable<(string, string)> extraEnvVariables,
        string? appEndTag)
    {
        var args = new MlaunchArguments();

        // Environment variables
        var envVariables = GetEnvVariables(
            xmlResultJargon,
            skippedMethods,
            skippedTestClasses,
            listenerTransport,
            listenerPort,
            listenerTmpFile,
            extraEnvVariables,
            appEndTag);

        // Variables passed through --set-env
        args.AddRange(envVariables.Select(pair => new SetEnvVariableArgument(pair.Key, pair.Value)));

        // Arguments passed to the iOS app bundle
        args.AddRange(extraAppArguments.Select(arg => new SetAppArgumentArgument(arg)));

        return args;
    }

    private MlaunchArguments GetSimulatorArguments(
        AppBundleInformation appInformation,
        IDevice simulator,
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
            xmlResultJargon,
            skippedMethods,
            skippedTestClasses,
            deviceListenerTransport,
            deviceListenerPort,
            deviceListenerTmpFile,
            extraAppArguments,
            extraEnvVariables,
            appEndTag: null);

        args.Add(new SetEnvVariableArgument(EnviromentVariables.HostName, "127.0.0.1"));
        args.Add(new SimulatorUDIDArgument(simulator));

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

    private MlaunchArguments GetDeviceArguments(
        AppBundleInformation appInformation,
        IDevice device,
        bool isWatchTarget,
        XmlResultJargon xmlResultJargon,
        string[]? skippedMethods,
        string[]? skippedTestClasses,
        ListenerTransport deviceListenerTransport,
        int deviceListenerPort,
        string deviceListenerTmpFile,
        IEnumerable<string> extraAppArguments,
        IEnumerable<(string, string)> extraEnvVariables,
        string? appEndTag)
    {
        var args = GetCommonArguments(
            xmlResultJargon,
            skippedMethods,
            skippedTestClasses,
            deviceListenerTransport,
            deviceListenerPort,
            deviceListenerTmpFile,
            extraAppArguments,
            extraEnvVariables,
            appEndTag);

        var ips = string.Join(",", _helpers.GetLocalIpAddresses().Select(ip => ip.ToString()));

        args.Add(new SetEnvVariableArgument(EnviromentVariables.HostName, ips));
        args.Add(new DisableMemoryLimitsArgument());
        args.Add(new DeviceNameArgument(device));

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
