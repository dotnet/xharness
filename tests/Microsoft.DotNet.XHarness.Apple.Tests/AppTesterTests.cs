// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.Common.Utilities;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Listeners;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Microsoft.DotNet.XHarness.iOS.Shared.XmlResults;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.Apple.Tests
{
    public class AppTesterTests : IDisposable
    {
        private const string AppName = "com.xamarin.bcltests.SystemXunit";
        private const string AppBundleIdentifier = AppName + ".ID";
        private const string SimulatorDeviceName = "Test iPhone simulator";
        private const string DeviceName = "Test iPhone";
        private const int Port = 1020;

        private static readonly string s_outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        private static readonly string s_appPath = Path.Combine(s_outputPath, AppName);

        private static readonly IHardwareDevice s_mockDevice = new Device(
            buildVersion: "17A577",
            deviceClass: DeviceClass.iPhone,
            deviceIdentifier: "8A450AA31EA94191AD6B02455F377CC1",
            interfaceType: "Usb",
            isUsableForDebugging: true,
            name: DeviceName,
            productType: "iPhone12,1",
            productVersion: "13.0");

        private readonly string _simulatorLogPath = Path.Combine(Path.GetTempPath(), "simulator-logs");
        private readonly Mock<ISimulatorDevice> _mockSimulator;

        private readonly Mock<IMlaunchProcessManager> _processManager;
        private readonly Mock<ILogs> _logs;
        private readonly Mock<IFileBackedLog> _mainLog;
        private readonly Mock<ISimulatorLoader> _simulatorLoader;
        private readonly Mock<ISimpleListener> _listener;
        private readonly Mock<ICrashSnapshotReporter> _snapshotReporter;
        private readonly Mock<ITestReporter> _testReporter;
        private readonly Mock<IHelpers> _helpers;
        private readonly Mock<ITunnelBore> _tunnelBore;
        private readonly Mock<ISimpleListenerFactory> _listenerFactory;

        private readonly ICrashSnapshotReporterFactory _snapshotReporterFactory;
        private readonly ITestReporterFactory _testReporterFactory;

        private Mock<IHardwareDeviceLoader> _hardwareDeviceLoader;

        public AppTesterTests()
        {
            _mainLog = new Mock<IFileBackedLog>();

            _processManager = new Mock<IMlaunchProcessManager>();
            _processManager.SetReturnsDefault(Task.FromResult(new ProcessExecutionResult() { ExitCode = 0 }));

            _hardwareDeviceLoader = new Mock<IHardwareDeviceLoader>();
            _hardwareDeviceLoader
                .Setup(x => x.FindDevice(RunMode.iOS, _mainLog.Object, false, false))
                .ReturnsAsync(s_mockDevice);

            _simulatorLoader = new Mock<ISimulatorLoader>();

            _mockSimulator = new Mock<ISimulatorDevice>();
            _mockSimulator.SetupGet(x => x.Name).Returns(SimulatorDeviceName);
            _mockSimulator.SetupGet(x => x.UDID).Returns("58F21118E4D34FD69EAB7860BB9B38A0");
            _mockSimulator.SetupGet(x => x.LogPath).Returns(_simulatorLogPath);
            _mockSimulator.SetupGet(x => x.SystemLog).Returns(Path.Combine(_simulatorLogPath, "system.log"));

            _listener = new Mock<ISimpleListener>();
            _listener
                .SetupGet(x => x.ConnectedTask)
                .Returns(Task.FromResult(true));

            _snapshotReporter = new Mock<ICrashSnapshotReporter>();

            _testReporter = new Mock<ITestReporter>();
            _testReporter
                .Setup(r => r.Success)
                .Returns(true);
            _testReporter
                .Setup(r => r.ParseResult())
                .ReturnsAsync((TestExecutingResult.Succeeded, "Tests run: 1194 Passed: 1191 Inconclusive: 0 Failed: 0 Ignored: 0"));
            _testReporter
                .Setup(x => x.CollectSimulatorResult(It.IsAny<Task<ProcessExecutionResult>>()))
                .Returns(Task.CompletedTask);

            _logs = new Mock<ILogs>();
            _logs.SetupGet(x => x.Directory).Returns(Path.Combine(s_outputPath, "logs"));

            _tunnelBore = new Mock<ITunnelBore>();
            _tunnelBore.Setup(t => t.Close(It.IsAny<string>()));

            _listenerFactory = new Mock<ISimpleListenerFactory>();
            _listenerFactory.SetReturnsDefault((ListenerTransport.Tcp, _listener.Object, "listener-temp-file"));
            _listenerFactory.Setup(f => f.TunnelBore).Returns(_tunnelBore.Object);
            _listener.Setup(x => x.InitializeAndGetPort()).Returns(Port);

            var factory2 = new Mock<ICrashSnapshotReporterFactory>();
            factory2.SetReturnsDefault(_snapshotReporter.Object);
            _snapshotReporterFactory = factory2.Object;

            var factory3 = new Mock<ITestReporterFactory>();
            factory3.SetReturnsDefault(_testReporter.Object);
            _testReporterFactory = factory3.Object;

            _helpers = new Mock<IHelpers>();
            _helpers
                .Setup(x => x.GetTerminalName(It.IsAny<int>()))
                .Returns("tty1");
            _helpers
                .Setup(x => x.GenerateStableGuid(It.IsAny<string>()))
                .Returns(Guid.NewGuid());
            _helpers
                .SetupGet(x => x.Timestamp)
                .Returns("mocked_timestamp");
            _helpers
                .Setup(x => x.GetLocalIpAddresses())
                .Returns(new[] { IPAddress.Loopback, IPAddress.IPv6Loopback });

            Directory.CreateDirectory(s_outputPath);
        }

        public void Dispose() => Directory.Delete(s_outputPath, true);

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task TestOnSimulatorWithNoAvailableSimulatorTest(bool useTcpTunnel)
        {
            // Mock finding simulators
            string simulatorLogPath = Path.Combine(Path.GetTempPath(), "simulator-logs");

            _simulatorLoader
                .Setup(x => x.FindSimulators(It.Is<TestTargetOs>(t => t.Platform == TestTarget.Simulator_tvOS), _mainLog.Object, It.IsAny<int>(), true, false))
                .ThrowsAsync(new NoDeviceFoundException("Failed to find simulator"));

            var listenerLogFile = new Mock<IFileBackedLog>();

            _logs
                .Setup(x => x.Create(It.IsAny<string>(), "TestLog", It.IsAny<bool>()))
                .Returns(listenerLogFile.Object);

            var captureLog = new Mock<ICaptureLog>();
            captureLog
                .SetupGet(x => x.FullPath)
                .Returns(simulatorLogPath);

            var captureLogFactory = new Mock<ICaptureLogFactory>();
            captureLogFactory
                .Setup(x => x.Create(
                   Path.Combine(_logs.Object.Directory, "tvos.log"),
                   "/path/to/_mockSimulator.log",
                   false,
                   It.IsAny<string>()))
                .Returns(captureLog.Object);

            _listenerFactory.Setup(f => f.UseTunnel).Returns(useTcpTunnel);
            // Act
            var appTester = new AppTester(_processManager.Object,
                _hardwareDeviceLoader.Object,
                _simulatorLoader.Object,
                _listenerFactory.Object,
                _snapshotReporterFactory,
                captureLogFactory.Object,
                Mock.Of<IDeviceLogCapturerFactory>(),
                _testReporterFactory,
                new XmlResultParser(),
                _mainLog.Object,
                _logs.Object,
                _helpers.Object,
                Enumerable.Empty<string>());

            var appInformation = new AppBundleInformation(
                appName: AppName,
                bundleIdentifier: AppBundleIdentifier,
                appPath: s_appPath,
                launchAppPath: s_appPath,
                supports32b: false,
                extension: null);

            await Assert.ThrowsAsync<NoDeviceFoundException>(
                async () => await appTester.TestApp(
                    appInformation,
                    new TestTargetOs(TestTarget.Simulator_tvOS, null),
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(30)));

            // Verify

            _mainLog.Verify(x => x.WriteLine("Test run completed"), Times.Never);

            _simulatorLoader.VerifyAll();

            _listener.Verify(x => x.StartAsync(), Times.Never);

            _tunnelBore.Verify(t => t.Create(It.IsAny<string>(), It.IsAny<ILog>()), Times.Never); // never create tunnels on simulators
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task TestOnSimulatorSuccessfullyTest(bool useTunnel)
        {
            _simulatorLoader
                .Setup(x => x.FindSimulators(It.Is<TestTargetOs>(t => t.Platform == TestTarget.Simulator_tvOS), _mainLog.Object, It.IsAny<int>(), true, false))
                .ReturnsAsync((_mockSimulator.Object, null));

            var testResultFilePath = Path.GetTempFileName();
            var listenerLogFile = Mock.Of<IFileBackedLog>(x => x.FullPath == testResultFilePath);
            File.WriteAllLines(testResultFilePath, new[] { "Some result here", "Tests run: 124", "Some result there" });

            _logs
                .Setup(x => x.Create("test-Simulator_tvOS-mocked_timestamp.log", "TestLog", It.IsAny<bool?>()))
                .Returns(listenerLogFile);

            var captureLog = new Mock<ICaptureLog>();
            captureLog.SetupGet(x => x.FullPath).Returns(_simulatorLogPath);

            var captureLogFactory = new Mock<ICaptureLogFactory>();
            captureLogFactory
                .Setup(x => x.Create(
                   Path.Combine(_logs.Object.Directory, _mockSimulator.Object.Name + ".log"),
                   _mockSimulator.Object.SystemLog,
                   false,
                   It.IsAny<string>()))
                .Returns(captureLog.Object);

            _listenerFactory.Setup(f => f.UseTunnel).Returns(useTunnel);

            // Act
            var appTester = new AppTester(_processManager.Object,
                _hardwareDeviceLoader.Object,
                _simulatorLoader.Object,
                _listenerFactory.Object,
                _snapshotReporterFactory,
                captureLogFactory.Object,
                Mock.Of<IDeviceLogCapturerFactory>(),
                _testReporterFactory,
                new XmlResultParser(),
                _mainLog.Object,
                _logs.Object,
                _helpers.Object,
                Array.Empty<string>());

            var appInformation = new AppBundleInformation(
                appName: AppName,
                bundleIdentifier: AppBundleIdentifier,
                appPath: s_appPath,
                launchAppPath: s_appPath,
                supports32b: false,
                extension: null);

            var (deviceName, result, resultMessage) = await appTester.TestApp(
                appInformation,
                new TestTargetOs(TestTarget.Simulator_tvOS, null),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30),
                ensureCleanSimulatorState: true);

            // Verify
            Assert.Equal(SimulatorDeviceName, deviceName);
            Assert.Equal(TestExecutingResult.Succeeded, result);
            Assert.Equal("Tests run: 1194 Passed: 1191 Inconclusive: 0 Failed: 0 Ignored: 0", resultMessage);

            var expectedArgs = GetExpectedSimulatorMlaunchArgs();

            _processManager
                .Verify(
                    x => x.ExecuteCommandAsync(
                       It.Is<MlaunchArguments>(args => args.AsCommandLine() == expectedArgs),
                       _mainLog.Object,
                       It.IsAny<TimeSpan>(),
                       null,
                       It.IsAny<CancellationToken>()),
                    Times.Once);

            _listener.Verify(x => x.InitializeAndGetPort(), Times.AtLeastOnce);
            _listener.Verify(x => x.StartAsync(), Times.AtLeastOnce);
            _listener.Verify(x => x.Cancel(), Times.AtLeastOnce);
            _listener.Verify(x => x.Dispose(), Times.AtLeastOnce);

            _simulatorLoader.VerifyAll();

            captureLog.Verify(x => x.StartCapture(), Times.AtLeastOnce);
            captureLog.Verify(x => x.StopCapture(), Times.AtLeastOnce);

            // When ensureCleanSimulatorState == true
            _mockSimulator.Verify(x => x.PrepareSimulator(_mainLog.Object, AppBundleIdentifier));
            _mockSimulator.Verify(x => x.KillEverything(_mainLog.Object));
        }

        [Fact]
        public async Task TestOnDeviceWithNoAvailableSimulatorTest()
        {
            _hardwareDeviceLoader = new Mock<IHardwareDeviceLoader>();
            _hardwareDeviceLoader
                .Setup(x => x.FindDevice(RunMode.iOS, _mainLog.Object, false, false))
                .ThrowsAsync(new NoDeviceFoundException());

            _listenerFactory.Setup(f => f.UseTunnel).Returns(false);

            // Act
            var appTester = new AppTester(_processManager.Object,
                _hardwareDeviceLoader.Object,
                _simulatorLoader.Object,
                _listenerFactory.Object,
                _snapshotReporterFactory,
                Mock.Of<ICaptureLogFactory>(),
                Mock.Of<IDeviceLogCapturerFactory>(),
                _testReporterFactory,
                new XmlResultParser(),
                _mainLog.Object,
                _logs.Object,
                _helpers.Object,
                Enumerable.Empty<string>());

            var appInformation = new AppBundleInformation(
                appName: AppName,
                bundleIdentifier: AppBundleIdentifier,
                appPath: s_appPath,
                launchAppPath: s_appPath,
                supports32b: false,
                extension: null);

            await Assert.ThrowsAsync<NoDeviceFoundException>(
                async () => await appTester.TestApp(
                    appInformation,
                    new TestTargetOs(TestTarget.Device_iOS, null),
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(30),
                    ensureCleanSimulatorState: true));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task TestOnDeviceSuccessfullyTest(bool useTunnel)
        {
            var deviceSystemLog = new Mock<IFileBackedLog>();
            deviceSystemLog.SetupGet(x => x.FullPath).Returns(Path.GetTempFileName());

            var deviceLogCapturer = new Mock<IDeviceLogCapturer>();

            var deviceLogCapturerFactory = new Mock<IDeviceLogCapturerFactory>();
            deviceLogCapturerFactory
                .Setup(x => x.Create(_mainLog.Object, deviceSystemLog.Object, DeviceName))
                .Returns(deviceLogCapturer.Object);

            var testResultFilePath = Path.GetTempFileName();
            var listenerLogFile = Mock.Of<IFileBackedLog>(x => x.FullPath == testResultFilePath);
            File.WriteAllLines(testResultFilePath, new[] { "Some result here", "Tests run: 124", "Some result there" });

            _logs
                .Setup(x => x.Create("test-Device_iOS-mocked_timestamp.log", "TestLog", It.IsAny<bool?>()))
                .Returns(listenerLogFile);

            _logs
                .Setup(x => x.Create($"device-{DeviceName}-mocked_timestamp.log", LogType.SystemLog.ToString(), It.IsAny<bool?>()))
                .Returns(deviceSystemLog.Object);

            // set tunnel bore expectation
            if (useTunnel)
            {
                _tunnelBore.Setup(t => t.Create(DeviceName, It.IsAny<ILog>()));
            }

            _listenerFactory.Setup(f => f.UseTunnel).Returns((useTunnel));
            // Act
            var appTester = new AppTester(_processManager.Object,
                _hardwareDeviceLoader.Object,
                _simulatorLoader.Object,
                _listenerFactory.Object,
                _snapshotReporterFactory,
                Mock.Of<ICaptureLogFactory>(),
                deviceLogCapturerFactory.Object,
                _testReporterFactory,
                new XmlResultParser(),
                _mainLog.Object,
                _logs.Object,
                _helpers.Object,
                new[] { "--appArg1=value1", "-f" });

            var appInformation = new AppBundleInformation(
                appName: AppName,
                bundleIdentifier: AppBundleIdentifier,
                appPath: s_appPath,
                launchAppPath: s_appPath,
                supports32b: false,
                extension: null);

            var (deviceName, result, resultMessage) = await appTester.TestApp(
                appInformation,
                new TestTargetOs(TestTarget.Device_iOS, null),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30));

            // Verify
            Assert.Equal(DeviceName, deviceName);
            Assert.Equal(TestExecutingResult.Succeeded, result);
            Assert.Equal("Tests run: 1194 Passed: 1191 Inconclusive: 0 Failed: 0 Ignored: 0", resultMessage);

            var expectedArgs = GetExpectedDeviceMlaunchArgs(
                useTunnel: useTunnel,
                extraArgs: "-argument=--appArg1=value1 -argument=-f ");

            _processManager
                .Verify(
                    x => x.ExecuteCommandAsync(
                       It.Is<MlaunchArguments>(args => args.AsCommandLine() == expectedArgs),
                       It.IsAny<ILog>(),
                       It.IsAny<TimeSpan>(),
                       null,
                       It.IsAny<CancellationToken>()),
                    Times.Once);

            _listener.Verify(x => x.InitializeAndGetPort(), Times.AtLeastOnce);
            _listener.Verify(x => x.StartAsync(), Times.AtLeastOnce);
            _listener.Verify(x => x.Cancel(), Times.AtLeastOnce);
            _listener.Verify(x => x.Dispose(), Times.AtLeastOnce);

            // verify that we do close the tunnel when it was used
            // we dont want to leak a process
            if (useTunnel)
            {
                _tunnelBore.Verify(t => t.Close(DeviceName));
            }

            _hardwareDeviceLoader.VerifyAll();

            _snapshotReporter.Verify(x => x.StartCaptureAsync(), Times.AtLeastOnce);
            _snapshotReporter.Verify(x => x.StartCaptureAsync(), Times.AtLeastOnce);

            deviceSystemLog.Verify(x => x.Dispose(), Times.AtLeastOnce);
        }

        [Theory]
        [InlineData("MyClass.MyMethod")]
        [InlineData("MyClass.MyMethod", "MyClass.MySecondMethod")]
        public async Task TestOnDeviceWithSkippedTestsTest(params string[] skippedTests)
        {
            var deviceSystemLog = new Mock<IFileBackedLog>();
            deviceSystemLog.SetupGet(x => x.FullPath).Returns(Path.GetTempFileName());

            var deviceLogCapturer = new Mock<IDeviceLogCapturer>();

            var deviceLogCapturerFactory = new Mock<IDeviceLogCapturerFactory>();
            deviceLogCapturerFactory
                .Setup(x => x.Create(_mainLog.Object, deviceSystemLog.Object, DeviceName))
                .Returns(deviceLogCapturer.Object);

            var testResultFilePath = Path.GetTempFileName();
            var listenerLogFile = Mock.Of<IFileBackedLog>(x => x.FullPath == testResultFilePath);
            File.WriteAllLines(testResultFilePath, new[] { "Some result here", "Tests run: 124", "Some result there" });

            _logs
                .Setup(x => x.Create("test-Device_iOS-mocked_timestamp.log", "TestLog", It.IsAny<bool?>()))
                .Returns(listenerLogFile);

            _logs
                .Setup(x => x.Create($"device-{DeviceName}-mocked_timestamp.log", LogType.SystemLog.ToString(), It.IsAny<bool?>()))
                .Returns(deviceSystemLog.Object);

            // Act
            var appTester = new AppTester(_processManager.Object,
                _hardwareDeviceLoader.Object,
                _simulatorLoader.Object,
                _listenerFactory.Object,
                _snapshotReporterFactory,
                Mock.Of<ICaptureLogFactory>(),
                deviceLogCapturerFactory.Object,
                _testReporterFactory,
                new XmlResultParser(),
                _mainLog.Object,
                _logs.Object,
                _helpers.Object,
                Enumerable.Empty<string>());

            var appInformation = new AppBundleInformation(
                appName: AppName,
                bundleIdentifier: AppBundleIdentifier,
                appPath: s_appPath,
                launchAppPath: s_appPath,
                supports32b: false,
                extension: null);

            var (deviceName, result, resultMessage) = await appTester.TestApp(
                appInformation,
                new TestTargetOs(TestTarget.Device_iOS, null),
                timeout: TimeSpan.FromSeconds(30),
                testLaunchTimeout: TimeSpan.FromSeconds(30),
                skippedMethods: skippedTests);

            // Verify
            Assert.Equal(DeviceName, deviceName);
            Assert.Equal(TestExecutingResult.Succeeded, result);
            Assert.Equal("Tests run: 1194 Passed: 1191 Inconclusive: 0 Failed: 0 Ignored: 0", resultMessage);

            var skippedTestsArg = $"-setenv=NUNIT_RUN_ALL=false -setenv=NUNIT_SKIPPED_METHODS={string.Join(',', skippedTests)} ";

            var expectedArgs = GetExpectedDeviceMlaunchArgs(skippedTestsArg);

            _processManager
                .Verify(
                    x => x.ExecuteCommandAsync(
                       It.Is<MlaunchArguments>(args => args.AsCommandLine() == expectedArgs),
                       It.IsAny<ILog>(),
                       It.IsAny<TimeSpan>(),
                       null,
                       It.IsAny<CancellationToken>()),
                    Times.Once);

            _listener.Verify(x => x.InitializeAndGetPort(), Times.AtLeastOnce);
            _listener.Verify(x => x.StartAsync(), Times.AtLeastOnce);
            _listener.Verify(x => x.Cancel(), Times.AtLeastOnce);
            _listener.Verify(x => x.Dispose(), Times.AtLeastOnce);

            _hardwareDeviceLoader.VerifyAll();

            _snapshotReporter.Verify(x => x.StartCaptureAsync(), Times.AtLeastOnce);
            _snapshotReporter.Verify(x => x.StartCaptureAsync(), Times.AtLeastOnce);

            deviceSystemLog.Verify(x => x.Dispose(), Times.AtLeastOnce);
        }

        [Theory]
        [InlineData("MyClass")]
        [InlineData("MyClass", "MySecondClass")]
        public async Task TestOnDeviceWithSkippedClassesTestTest(params string[] skippedClasses)
        {
            var deviceSystemLog = new Mock<IFileBackedLog>();
            deviceSystemLog.SetupGet(x => x.FullPath).Returns(Path.GetTempFileName());

            var deviceLogCapturer = new Mock<IDeviceLogCapturer>();

            var deviceLogCapturerFactory = new Mock<IDeviceLogCapturerFactory>();
            deviceLogCapturerFactory
                .Setup(x => x.Create(_mainLog.Object, deviceSystemLog.Object, DeviceName))
                .Returns(deviceLogCapturer.Object);

            var testResultFilePath = Path.GetTempFileName();
            var listenerLogFile = Mock.Of<IFileBackedLog>(x => x.FullPath == testResultFilePath);
            File.WriteAllLines(testResultFilePath, new[] { "Some result here", "Tests run: 124", "Some result there" });

            _logs
                .Setup(x => x.Create("test-Device_iOS-mocked_timestamp.log", "TestLog", It.IsAny<bool?>()))
                .Returns(listenerLogFile);

            _logs
                .Setup(x => x.Create($"device-{DeviceName}-mocked_timestamp.log", LogType.SystemLog.ToString(), It.IsAny<bool?>()))
                .Returns(deviceSystemLog.Object);

            // Act
            var appTester = new AppTester(_processManager.Object,
                _hardwareDeviceLoader.Object,
                _simulatorLoader.Object,
                _listenerFactory.Object,
                _snapshotReporterFactory,
                Mock.Of<ICaptureLogFactory>(),
                deviceLogCapturerFactory.Object,
                _testReporterFactory,
                new XmlResultParser(),
                _mainLog.Object,
                _logs.Object,
                _helpers.Object,
                Enumerable.Empty<string>());

            var appInformation = new AppBundleInformation(
                appName: AppName,
                bundleIdentifier: AppBundleIdentifier,
                appPath: s_appPath,
                launchAppPath: s_appPath,
                supports32b: false,
                extension: null);

            var (deviceName, result, resultMessage) = await appTester.TestApp(
                appInformation,
                new TestTargetOs(TestTarget.Device_iOS, null),
                timeout: TimeSpan.FromSeconds(30),
                testLaunchTimeout: TimeSpan.FromSeconds(30),
                skippedTestClasses: skippedClasses);

            // Verify
            Assert.Equal(DeviceName, deviceName);
            Assert.Equal(TestExecutingResult.Succeeded, result);
            Assert.Equal("Tests run: 1194 Passed: 1191 Inconclusive: 0 Failed: 0 Ignored: 0", resultMessage);

            var skippedTestsArg = $"-setenv=NUNIT_RUN_ALL=false -setenv=NUNIT_SKIPPED_CLASSES={string.Join(',', skippedClasses)} ";

            var expectedArgs = GetExpectedDeviceMlaunchArgs(skippedTestsArg);

            _processManager
                .Verify(
                    x => x.ExecuteCommandAsync(
                       It.Is<MlaunchArguments>(args => args.AsCommandLine() == expectedArgs),
                       It.IsAny<ILog>(),
                       It.IsAny<TimeSpan>(),
                       null,
                       It.IsAny<CancellationToken>()),
                    Times.Once);

            _listener.Verify(x => x.InitializeAndGetPort(), Times.AtLeastOnce);
            _listener.Verify(x => x.StartAsync(), Times.AtLeastOnce);
            _listener.Verify(x => x.Cancel(), Times.AtLeastOnce);
            _listener.Verify(x => x.Dispose(), Times.AtLeastOnce);

            _hardwareDeviceLoader.VerifyAll();

            _snapshotReporter.Verify(x => x.StartCaptureAsync(), Times.AtLeastOnce);
            _snapshotReporter.Verify(x => x.StartCaptureAsync(), Times.AtLeastOnce);

            deviceSystemLog.Verify(x => x.Dispose(), Times.AtLeastOnce);
        }

        [Fact]
        public async Task TestOnMacCatalystSuccessfullyTest()
        {
            var testResultFilePath = Path.GetTempFileName();
            var listenerLogFile = Mock.Of<IFileBackedLog>(x => x.FullPath == testResultFilePath);
            File.WriteAllLines(testResultFilePath, new[] { "Some result here", "Tests run: 124", "Some result there" });

            _logs
                .Setup(x => x.Create("test-maccatalyst-mocked_timestamp.log", "TestLog", It.IsAny<bool?>()))
                .Returns(listenerLogFile);

            var captureLog = new Mock<ICaptureLog>();
            captureLog.SetupGet(x => x.FullPath).Returns(_simulatorLogPath);

            var captureLogFactory = new Mock<ICaptureLogFactory>();
            captureLogFactory
                .Setup(x => x.Create(
                   Path.Combine(_logs.Object.Directory, _mockSimulator.Object.Name + ".log"),
                   _mockSimulator.Object.SystemLog,
                   false,
                   It.IsAny<string>()))
                .Returns(captureLog.Object);

            // Act
            var appTester = new AppTester(_processManager.Object,
                _hardwareDeviceLoader.Object,
                _simulatorLoader.Object,
                _listenerFactory.Object,
                _snapshotReporterFactory,
                captureLogFactory.Object,
                Mock.Of<IDeviceLogCapturerFactory>(),
                _testReporterFactory,
                new XmlResultParser(),
                _mainLog.Object,
                _logs.Object,
                _helpers.Object,
                Array.Empty<string>());

            var appInformation = new AppBundleInformation(
                appName: AppName,
                bundleIdentifier: AppBundleIdentifier,
                appPath: s_appPath,
                launchAppPath: s_appPath,
                supports32b: false,
                extension: null);

            var (deviceName, result, resultMessage) = await appTester.TestApp(
                appInformation,
                new TestTargetOs(TestTarget.MacCatalyst, null),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30),
                ensureCleanSimulatorState: true);

            // Verify
            Assert.Equal(TestExecutingResult.Succeeded, result);
            Assert.Equal("Tests run: 1194 Passed: 1191 Inconclusive: 0 Failed: 0 Ignored: 0", resultMessage);

            _processManager
                .Verify(
                    x => x.ExecuteCommandAsync(
                       "open",
                       It.Is<IList<string>>(args => args.Contains(s_appPath)),
                       _mainLog.Object,
                       It.IsAny<TimeSpan>(),
                       It.Is<Dictionary<string, string>>(envVars =>
                            envVars["NUNIT_HOSTNAME"] == "127.0.0.1" &&
                            envVars["NUNIT_HOSTPORT"] == Port.ToString() &&
                            envVars["NUNIT_AUTOEXIT"] == "true" &&
                            envVars["NUNIT_XML_VERSION"] == "xUnit" &&
                            envVars["NUNIT_ENABLE_XML_OUTPUT"] == "true"),
                       It.IsAny<CancellationToken>()),
                    Times.Once);

            _listener.Verify(x => x.InitializeAndGetPort(), Times.AtLeastOnce);
            _listener.Verify(x => x.StartAsync(), Times.AtLeastOnce);
            _listener.Verify(x => x.Cancel(), Times.AtLeastOnce);
            _listener.Verify(x => x.Dispose(), Times.AtLeastOnce);
        }

        private static string GetExpectedDeviceMlaunchArgs(string skippedTests = null, bool useTunnel = false, string extraArgs = null) =>
            "-v " +
            "-v " +
            "-setenv=NUNIT_AUTOEXIT=true " +
            $"-setenv=NUNIT_HOSTPORT={Port} " +
            "-setenv=NUNIT_ENABLE_XML_OUTPUT=true " +
            "-setenv=NUNIT_XML_VERSION=xUnit " +
            skippedTests +
            extraArgs +
            "-setenv=NUNIT_HOSTNAME=127.0.0.1,::1 " +
            "--disable-memory-limits " +
            $"--devname \"{DeviceName}\" " +
            (useTunnel ? "-setenv=USE_TCP_TUNNEL=true " : null) +
            $"--launchdev {StringUtils.FormatArguments(s_appPath)} " +
            "--wait-for-exit";

        private string GetExpectedSimulatorMlaunchArgs() =>
            "-v " +
            "-v " +
            "-setenv=NUNIT_AUTOEXIT=true " +
            $"-setenv=NUNIT_HOSTPORT={Port} " +
            "-setenv=NUNIT_ENABLE_XML_OUTPUT=true " +
            "-setenv=NUNIT_XML_VERSION=xUnit " +
            "-setenv=NUNIT_HOSTNAME=127.0.0.1 " +
            $"--device=:v2:udid={_mockSimulator.Object.UDID} " +
            $"--launchsim {StringUtils.FormatArguments(s_appPath)}";
    }
}
