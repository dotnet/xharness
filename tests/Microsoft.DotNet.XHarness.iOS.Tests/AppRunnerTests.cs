using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.iOS;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution.Mlaunch;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Listeners;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Tests
{
    public class AppRunnerTests : IDisposable
    {
        const string AppName = "com.xamarin.bcltests.SystemXunit";
        const string AppBundleIdentifier = AppName + ".ID";

        private static readonly string s_outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        private static readonly string s_appPath = Path.Combine(s_outputPath, AppName);

        private static readonly IHardwareDevice s_mockDevice = new Device(
            buildVersion: "17A577",
            deviceClass: DeviceClass.iPhone,
            deviceIdentifier: "8A450AA31EA94191AD6B02455F377CC1",
            interfaceType: "Usb",
            isUsableForDebugging: true,
            name: "Test iPhone",
            productType: "iPhone12,1",
            productVersion: "13.0");

        private readonly Mock<IProcessManager> _processManager;
        private readonly Mock<ILogs> _logs;
        private readonly Mock<ILog> _mainLog;
        private readonly Mock<ISimulatorLoader> _simulatorLoader;
        private readonly Mock<ISimpleListener> _listener;
        private readonly Mock<ICrashSnapshotReporter> _snapshotReporter;
        private readonly Mock<ITestReporter> _testReporter;
        private readonly Mock<IHelpers> _helpers;
        private readonly Mock<ITunnelBore> _tunnelBore;

        private readonly ISimpleListenerFactory _listenerFactory;
        private readonly ICrashSnapshotReporterFactory _snapshotReporterFactory;
        private readonly ITestReporterFactory _testReporterFactory;

        private Mock<IHardwareDeviceLoader> _hardwareDeviceLoader;

        public AppRunnerTests()
        {
            _mainLog = new Mock<ILog>();

            _processManager = new Mock<IProcessManager>();
            _processManager.SetReturnsDefault(Task.FromResult(new ProcessExecutionResult() { ExitCode = 0 }));

            _hardwareDeviceLoader = new Mock<IHardwareDeviceLoader>();
            _hardwareDeviceLoader
                .Setup(x => x.FindDevice(RunMode.iOS, _mainLog.Object, false, false))
                .ReturnsAsync(s_mockDevice);

            _simulatorLoader = new Mock<ISimulatorLoader>();
            _simulatorLoader
                .Setup(x => x.LoadDevices(It.IsAny<ILog>(), false, false, false))
                .Returns(Task.CompletedTask);

            _listener = new Mock<ISimpleListener>();
            _listener
                .SetupGet(x => x.ConnectedTask)
                .Returns(Task.CompletedTask);

            _snapshotReporter = new Mock<ICrashSnapshotReporter>();

            _testReporter = new Mock<ITestReporter>();
            _testReporter
                .Setup(r => r.Success)
                .Returns(true);
            _testReporter
                .Setup(r => r.ParseResult())
                .ReturnsAsync((TestExecutingResult.Succeeded, null));
            _testReporter
                .Setup(x => x.CollectSimulatorResult(It.IsAny<Task<ProcessExecutionResult>>()))
                .Returns(Task.CompletedTask);

            _logs = new Mock<ILogs>();
            _logs.SetupGet(x => x.Directory).Returns(Path.Combine(s_outputPath, "logs"));

            _tunnelBore = new Mock<ITunnelBore>();
            _tunnelBore.Setup(t => t.Close(It.IsAny<string>()));

            var factory = new Mock<ISimpleListenerFactory>();
            factory.SetReturnsDefault((ListenerTransport.Tcp, _listener.Object, "listener-temp-file"));
            factory.Setup(f => f.TunnelBore).Returns(_tunnelBore.Object);
            _listenerFactory = factory.Object;
            _listener.SetupGet(x => x.Port).Returns(1020);

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

            Directory.CreateDirectory(s_outputPath);
        }

        public void Dispose()
        {
            Directory.Delete(s_outputPath, true);
        }

        [Theory]
        [InlineData(false, false)] // no tunnel, no xml
        [InlineData(false, true)] // no tunnel, xml
        [InlineData(true, false)] // tunnel, no xml
        [InlineData(true, true)] // tunnel and xml
        public async Task RunOnSimulatorWithNoAvailableSimulatorTest(bool useTcpTunnel, bool useXmlOutput)
        {
            // Mock finding simulators
            string simulatorLogPath = Path.Combine(Path.GetTempPath(), "simulator-logs");

            _simulatorLoader
                .Setup(x => x.FindSimulators(TestTarget.Simulator_tvOS, _mainLog.Object, true, false))
                .ReturnsAsync(new ISimulatorDevice[0]);

            var listenerLogFile = new Mock<ILog>();

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
                   "/path/to/simulator.log",
                   true,
                   It.IsAny<string>()))
                .Returns(captureLog.Object);

            // Act
            var appRunner = new AppRunner(_processManager.Object,
                _hardwareDeviceLoader.Object,
                _simulatorLoader.Object,
                _listenerFactory,
                _snapshotReporterFactory,
                captureLogFactory.Object,
                Mock.Of<IDeviceLogCapturerFactory>(),
                _testReporterFactory,
                _mainLog.Object,
                _logs.Object,
                _helpers.Object,
                useXmlOutput,
                useTcpTunnel);

            var appInformation = new AppBundleInformation(AppName, AppBundleIdentifier, s_appPath, s_appPath, null);

            await Assert.ThrowsAsync<NoDeviceFoundException>(
                async () => await appRunner.RunApp(
                    appInformation,
                    TestTarget.Simulator_tvOS,
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(30)));

            // Verify

            _mainLog.Verify(x => x.WriteLine("Test run completed"), Times.Never);

            _simulatorLoader.VerifyAll();

            _listener.Verify(x => x.Initialize(), Times.AtLeastOnce);
            _listener.Verify(x => x.StartAsync(), Times.AtLeastOnce);

            _tunnelBore.Verify(t => t.Create(It.IsAny<string>(), It.IsAny<ILog>()), Times.Never); // never create tunnels on simulators
        }

        [Theory]
        [InlineData(false, false)] // no tunnel, no xml
        [InlineData(false, true)] // no tunnel, xml
        [InlineData(true, false)] // tunnel, no xml
        [InlineData(true, true)] // tunnel and xml
        public async Task RunOnSimulatorSuccessfullyTest(bool useTunnel, bool useXml)
        {
            string simulatorLogPath = Path.Combine(Path.GetTempPath(), "simulator-logs");

            var simulator = new Mock<ISimulatorDevice>();
            simulator.SetupGet(x => x.Name).Returns("Test iPhone simulator");
            simulator.SetupGet(x => x.UDID).Returns("58F21118E4D34FD69EAB7860BB9B38A0");
            simulator.SetupGet(x => x.LogPath).Returns(simulatorLogPath);
            simulator.SetupGet(x => x.SystemLog).Returns(Path.Combine(simulatorLogPath, "system.log"));

            _simulatorLoader
                .Setup(x => x.FindSimulators(TestTarget.Simulator_tvOS, _mainLog.Object, true, false))
                .ReturnsAsync(new ISimulatorDevice[] { simulator.Object });

            var testResultFilePath = Path.GetTempFileName();
            var listenerLogFile = Mock.Of<ILog>(x => x.FullPath == testResultFilePath);
            File.WriteAllLines(testResultFilePath, new[] { "Some result here", "Tests run: 124", "Some result there" });

            _logs
                .Setup(x => x.Create("test-Simulator_tvOS-mocked_timestamp.log", "TestLog", It.IsAny<bool?>()))
                .Returns(listenerLogFile);

            var captureLog = new Mock<ICaptureLog>();
            captureLog.SetupGet(x => x.FullPath).Returns(simulatorLogPath);

            var captureLogFactory = new Mock<ICaptureLogFactory>();
            captureLogFactory
                .Setup(x => x.Create(
                   Path.Combine(_logs.Object.Directory, simulator.Object.Name + ".log"),
                   simulator.Object.SystemLog,
                   true,
                   It.IsAny<string>()))
                .Returns(captureLog.Object);

            // Act
            var appRunner = new AppRunner(_processManager.Object,
                _hardwareDeviceLoader.Object,
                _simulatorLoader.Object,
                _listenerFactory,
                _snapshotReporterFactory,
                captureLogFactory.Object,
                Mock.Of<IDeviceLogCapturerFactory>(),
                _testReporterFactory,
                _mainLog.Object,
                _logs.Object,
                _helpers.Object,
                useXml,
                useTunnel);

            var appInformation = new AppBundleInformation(AppName, AppBundleIdentifier, s_appPath, s_appPath, null);

            var (deviceName, success) = await appRunner.RunApp(
                appInformation,
                TestTarget.Simulator_tvOS,
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30),
                ensureCleanSimulatorState: true);

            // Verify
            Assert.Equal("Test iPhone simulator", deviceName);
            Assert.True(success);

            var xmlParam = useXml ? "-setenv=NUNIT_ENABLE_XML_OUTPUT=true -setenv=NUNIT_ENABLE_XML_MODE=wrapped -setenv=NUNIT_XML_VERSION=xUnit " : "";

            var expectedArgs = $"-argument=-connection-mode -argument=none -argument=-app-arg:-autostart " +
                $"-setenv=NUNIT_AUTOSTART=true -argument=-app-arg:-autoexit -setenv=NUNIT_AUTOEXIT=true " +
                $"-argument=-app-arg:-enablenetwork -setenv=NUNIT_ENABLE_NETWORK=true -setenv=DISABLE_SYSTEM_PERMISSION_TESTS=1 -v -v " +
                $"-argument=-app-arg:-hostname:127.0.0.1 -setenv=NUNIT_HOSTNAME=127.0.0.1 -argument=-app-arg:-transport:Tcp " +
                $"-setenv=NUNIT_TRANSPORT=TCP -argument=-app-arg:-hostport:{_listener.Object.Port} " +
                $"-setenv=NUNIT_HOSTPORT={_listener.Object.Port} {xmlParam}--launchsim {StringUtils.FormatArguments(s_appPath)} " +
                $"--stdout=tty1 --stderr=tty1 --device=:v2:udid={simulator.Object.UDID}";

            _processManager
                .Verify(
                    x => x.ExecuteCommandAsync(
                       It.Is<MlaunchArguments>(args => args.AsCommandLine() == expectedArgs),
                       _mainLog.Object,
                       It.IsAny<TimeSpan>(),
                       null,
                       It.IsAny<CancellationToken>()),
                    Times.Once);

            _listener.Verify(x => x.Initialize(), Times.AtLeastOnce);
            _listener.Verify(x => x.StartAsync(), Times.AtLeastOnce);
            _listener.Verify(x => x.Cancel(), Times.AtLeastOnce);
            _listener.Verify(x => x.Dispose(), Times.AtLeastOnce);

            _simulatorLoader.VerifyAll();

            captureLog.Verify(x => x.StartCapture(), Times.AtLeastOnce);
            captureLog.Verify(x => x.StopCapture(), Times.AtLeastOnce);

            // When ensureCleanSimulatorState == true
            simulator.Verify(x => x.PrepareSimulator(_mainLog.Object, AppBundleIdentifier));
            simulator.Verify(x => x.KillEverything(_mainLog.Object));
        }

        [Fact]
        public async Task RunOnDeviceWithNoAvailableSimulatorTest()
        {
            _hardwareDeviceLoader = new Mock<IHardwareDeviceLoader>();
            _hardwareDeviceLoader
                .Setup(x => x.FindDevice(RunMode.iOS, _mainLog.Object, false, false))
                .ThrowsAsync(new NoDeviceFoundException());

            // Act
            var appRunner = new AppRunner(_processManager.Object,
                _hardwareDeviceLoader.Object,
                _simulatorLoader.Object,
                _listenerFactory,
                _snapshotReporterFactory,
                Mock.Of<ICaptureLogFactory>(),
                Mock.Of<IDeviceLogCapturerFactory>(),
                _testReporterFactory,
                _mainLog.Object,
                _logs.Object,
                _helpers.Object,
                true,
                false);

            var appInformation = new AppBundleInformation(AppName, AppBundleIdentifier, s_appPath, s_appPath, null);

            await Assert.ThrowsAsync<NoDeviceFoundException>(
                async () => await appRunner.RunApp(
                    appInformation,
                    TestTarget.Device_iOS,
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(30),
                    ensureCleanSimulatorState: true));
        }

        [Theory]
        [InlineData(false, false)] // no tunnel, no xml
        [InlineData(false, true)] // no tunnel, xml
        [InlineData(true, false)] // tunnel, no xml
        [InlineData(true, true)] // tunnel and xml
        public async Task RunOnDeviceSuccessfullyTest(bool useTunnel, bool useXml)
        {
            var deviceSystemLog = new Mock<ILog>();
            deviceSystemLog.SetupGet(x => x.FullPath).Returns(Path.GetTempFileName());

            var deviceLogCapturer = new Mock<IDeviceLogCapturer>();

            var deviceLogCapturerFactory = new Mock<IDeviceLogCapturerFactory>();
            deviceLogCapturerFactory
                .Setup(x => x.Create(_mainLog.Object, deviceSystemLog.Object, "Test iPhone"))
                .Returns(deviceLogCapturer.Object);

            var testResultFilePath = Path.GetTempFileName();
            var listenerLogFile = Mock.Of<ILog>(x => x.FullPath == testResultFilePath);
            File.WriteAllLines(testResultFilePath, new[] { "Some result here", "Tests run: 124", "Some result there" });

            _logs
                .Setup(x => x.Create("test-Device_iOS-mocked_timestamp.log", "TestLog", It.IsAny<bool?>()))
                .Returns(listenerLogFile);

            _logs
                .Setup(x => x.Create("device-Test iPhone-mocked_timestamp.log", "Device log", It.IsAny<bool?>()))
                .Returns(deviceSystemLog.Object);

            // set tunnel bore expectation
            if (useTunnel) {
                _tunnelBore.Setup(t => t.Create("Test iPhone", It.IsAny<ILog>()));
            }

            // Act
            var appRunner = new AppRunner(_processManager.Object,
                _hardwareDeviceLoader.Object,
                _simulatorLoader.Object,
                _listenerFactory,
                _snapshotReporterFactory,
                Mock.Of<ICaptureLogFactory>(),
                deviceLogCapturerFactory.Object,
                _testReporterFactory,
                _mainLog.Object,
                _logs.Object,
                _helpers.Object,
                useXml,
                useTunnel);

            var appInformation = new AppBundleInformation(AppName, AppBundleIdentifier, s_appPath, s_appPath, null);

            var (deviceName, success) = await appRunner.RunApp(
                appInformation,
                TestTarget.Device_iOS,
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30));

            // Verify
            Assert.Equal("Test iPhone", deviceName);
            Assert.True(success);

            var ipAddresses = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList.Select(ip => ip.ToString());
            var ips = string.Join(",", ipAddresses);

            var tunnelParam = useTunnel ? "-setenv=USE_TCP_TUNNEL=true " : "";
            var xmlParam = useXml ? "-setenv=NUNIT_ENABLE_XML_OUTPUT=true -setenv=NUNIT_ENABLE_XML_MODE=wrapped -setenv=NUNIT_XML_VERSION=xUnit " : "";

            var expectedArgs = $"-argument=-connection-mode -argument=none -argument=-app-arg:-autostart " +
                $"-setenv=NUNIT_AUTOSTART=true -argument=-app-arg:-autoexit -setenv=NUNIT_AUTOEXIT=true " +
                $"-argument=-app-arg:-enablenetwork -setenv=NUNIT_ENABLE_NETWORK=true -setenv=DISABLE_SYSTEM_PERMISSION_TESTS=1 -v -v " +
                $"-argument=-app-arg:-hostname:{ips} -setenv=NUNIT_HOSTNAME={ips} -argument=-app-arg:-transport:Tcp " +
                $"-setenv=NUNIT_TRANSPORT=TCP -argument=-app-arg:-hostport:{_listener.Object.Port} " +
                $"-setenv=NUNIT_HOSTPORT={_listener.Object.Port} {tunnelParam}{xmlParam}--launchdev {StringUtils.FormatArguments(s_appPath)} " +
                $"--disable-memory-limits --wait-for-exit --devname \"Test iPhone\"";

            _processManager
                .Verify(
                    x => x.ExecuteCommandAsync(
                       It.Is<MlaunchArguments>(args => args.AsCommandLine() == expectedArgs),
                       It.IsAny<ILog>(),
                       It.IsAny<TimeSpan>(),
                       null,
                       It.IsAny<CancellationToken>()),
                    Times.Once);

            _listener.Verify(x => x.Initialize(), Times.AtLeastOnce);
            _listener.Verify(x => x.StartAsync(), Times.AtLeastOnce);
            _listener.Verify(x => x.Cancel(), Times.AtLeastOnce);
            _listener.Verify(x => x.Dispose(), Times.AtLeastOnce);

            // verify that we do close the tunnel when it was used
            // we dont want to leak a process
            if (useTunnel) {
                _tunnelBore.Verify(t => t.Close("Test iPhone"));
            }

            _hardwareDeviceLoader.VerifyAll();

            _snapshotReporter.Verify(x => x.StartCaptureAsync(), Times.AtLeastOnce);
            _snapshotReporter.Verify(x => x.StartCaptureAsync(), Times.AtLeastOnce);

            deviceSystemLog.Verify(x => x.Dispose(), Times.AtLeastOnce);
        }
    }
}
