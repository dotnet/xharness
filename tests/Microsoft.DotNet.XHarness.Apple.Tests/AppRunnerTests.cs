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
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.Apple.Tests
{
    public class AppRunnerTests : IDisposable
    {
        private const string AppName = "com.xamarin.bcltests.SystemXunit";
        private const string AppBundleIdentifier = AppName + ".ID";
        private const string SimulatorDeviceName = "Test iPhone simulator";
        private const string DeviceName = "Test iPhone";

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
        private readonly Mock<ICrashSnapshotReporter> _snapshotReporter;
        private readonly Mock<IHelpers> _helpers;

        private readonly ICrashSnapshotReporterFactory _snapshotReporterFactory;

        private Mock<IHardwareDeviceLoader> _hardwareDeviceLoader;

        public AppRunnerTests()
        {
            _mainLog = new Mock<IFileBackedLog>();

            _processManager = new Mock<IMlaunchProcessManager>();
            _processManager.SetReturnsDefault(Task.FromResult(new ProcessExecutionResult() { ExitCode = 0 }));

            _hardwareDeviceLoader = new Mock<IHardwareDeviceLoader>();
            _hardwareDeviceLoader
                .Setup(x => x.FindDevice(RunMode.iOS, _mainLog.Object, false))
                .ReturnsAsync(s_mockDevice);

            _simulatorLoader = new Mock<ISimulatorLoader>();
            _snapshotReporter = new Mock<ICrashSnapshotReporter>();

            _logs = new Mock<ILogs>();
            _logs.SetupGet(x => x.Directory).Returns(Path.Combine(s_outputPath, "logs"));

            var factory2 = new Mock<ICrashSnapshotReporterFactory>();
            factory2.SetReturnsDefault(_snapshotReporter.Object);
            _snapshotReporterFactory = factory2.Object;

            _mockSimulator = new Mock<ISimulatorDevice>();
            _mockSimulator.SetupGet(x => x.Name).Returns(SimulatorDeviceName);
            _mockSimulator.SetupGet(x => x.UDID).Returns("58F21118E4D34FD69EAB7860BB9B38A0");
            _mockSimulator.SetupGet(x => x.LogPath).Returns(_simulatorLogPath);
            _mockSimulator.SetupGet(x => x.SystemLog).Returns(Path.Combine(_simulatorLogPath, "system.log"));

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

        public void Dispose()
        {
            Directory.Delete(s_outputPath, true);
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task RunOnSimulatorWithNoAvailableSimulatorTest()
        {
            _simulatorLoader
                .Setup(x => x.FindSimulators(It.Is<TestTargetOs>(t => t.Platform == TestTarget.Simulator_tvOS), _mainLog.Object, It.IsAny<int>(), true, false))
                .ThrowsAsync(new NoDeviceFoundException("Failed to find simulator"));

            var captureLog = new Mock<ICaptureLog>();
            captureLog
                .SetupGet(x => x.FullPath)
                .Returns(_simulatorLogPath);

            var captureLogFactory = new Mock<ICaptureLogFactory>();
            captureLogFactory
                .Setup(x => x.Create(
                   Path.Combine(_logs.Object.Directory, "tvos.log"),
                   "/path/to/simulator.log",
                   false,
                   It.IsAny<LogType>()))
                .Returns(captureLog.Object);

            // Act
            var appRunner = new AppRunner(_processManager.Object,
                _hardwareDeviceLoader.Object,
                _simulatorLoader.Object,
                _snapshotReporterFactory,
                captureLogFactory.Object,
                Mock.Of<IDeviceLogCapturerFactory>(),
                _mainLog.Object,
                _logs.Object,
                _helpers.Object);

            var appInformation = GetMockedAppBundleInfo();

            await Assert.ThrowsAsync<NoDeviceFoundException>(
                async () => await appRunner.RunApp(
                    appInformation,
                    new TestTargetOs(TestTarget.Simulator_tvOS, null),
                    TimeSpan.FromSeconds(30),
                    Array.Empty<string>(),
                    Array.Empty<(string, string)>()));

            // Verify
            _mainLog.Verify(x => x.WriteLine("App run ended with 0"), Times.Never);
            _simulatorLoader.VerifyAll();
        }

        [Fact]
        public async Task RunOnSimulatorSuccessfullyTest()
        {
            _simulatorLoader
                .Setup(x => x.FindSimulators(It.Is<TestTargetOs>(t => t.Platform == TestTarget.Simulator_tvOS), _mainLog.Object, It.IsAny<int>(), true, false))
                .ReturnsAsync((_mockSimulator.Object, null));

            var captureLog = new Mock<ICaptureLog>();
            captureLog.SetupGet(x => x.FullPath).Returns(_simulatorLogPath);
            captureLog.SetupGet(x => x.Description).Returns(LogType.SystemLog.ToString());

            var captureLogFactory = new Mock<ICaptureLogFactory>();
            captureLogFactory
                .Setup(x => x.Create(
                   Path.Combine(_logs.Object.Directory, _mockSimulator.Object.Name + ".log"),
                   _mockSimulator.Object.SystemLog,
                   false,
                   LogType.SystemLog))
                .Returns(captureLog.Object);

            SetupLogList(new[] { captureLog.Object });

            var appInformation = GetMockedAppBundleInfo();

            // Act
            var appRunner = new AppRunner(_processManager.Object,
                _hardwareDeviceLoader.Object,
                _simulatorLoader.Object,
                _snapshotReporterFactory,
                captureLogFactory.Object,
                Mock.Of<IDeviceLogCapturerFactory>(),
                _mainLog.Object,
                _logs.Object,
                _helpers.Object);

            var (deviceName, result) = await appRunner.RunApp(
                appInformation,
                new TestTargetOs(TestTarget.Simulator_tvOS, null),
                TimeSpan.FromSeconds(30),
                new[] { "--foo=bar", "--xyz" },
                new[] { ("appArg1", "value1") },
                resetSimulator: true);

            // Verify
            Assert.Equal(SimulatorDeviceName, deviceName);
            Assert.True(result.Succeeded);

            var expectedArgs = GetExpectedSimulatorMlaunchArgs();

            _processManager
                .Verify(
                    x => x.ExecuteCommandAsync(
                       It.Is<MlaunchArguments>(args => args.AsCommandLine() == expectedArgs),
                       _mainLog.Object,
                       It.IsAny<TimeSpan>(),
                       It.IsAny<Dictionary<string, string>>(),
                       It.IsAny<CancellationToken>()),
                    Times.Once);

            _simulatorLoader.VerifyAll();

            captureLog.Verify(x => x.StartCapture(), Times.AtLeastOnce);

            // When resetSimulator == true
            _mockSimulator.Verify(x => x.PrepareSimulator(_mainLog.Object, AppBundleIdentifier));
            _mockSimulator.Verify(x => x.KillEverything(_mainLog.Object));
        }

        [Fact]
        public async Task RunOnDeviceWithNoAvailableSimulatorTest()
        {
            _hardwareDeviceLoader = new Mock<IHardwareDeviceLoader>();
            _hardwareDeviceLoader
                .Setup(x => x.FindDevice(RunMode.iOS, _mainLog.Object, false))
                .ThrowsAsync(new NoDeviceFoundException());

            // Act
            var appRunner = new AppRunner(_processManager.Object,
                _hardwareDeviceLoader.Object,
                _simulatorLoader.Object,
                _snapshotReporterFactory,
                Mock.Of<ICaptureLogFactory>(),
                Mock.Of<IDeviceLogCapturerFactory>(),
                _mainLog.Object,
                _logs.Object,
                _helpers.Object);

            var appInformation = GetMockedAppBundleInfo();

            await Assert.ThrowsAsync<NoDeviceFoundException>(
                async () => await appRunner.RunApp(
                    appInformation,
                    new TestTargetOs(TestTarget.Device_iOS, null),
                    TimeSpan.FromSeconds(30),
                    Enumerable.Empty<string>(),
                    Enumerable.Empty<(string, string)>(),
                    resetSimulator: true));
        }

        [Fact]
        public async Task RunOnDeviceSuccessfullyTest()
        {
            var deviceSystemLog = new Mock<IFileBackedLog>();
            deviceSystemLog.SetupGet(x => x.FullPath).Returns(Path.GetTempFileName());
            deviceSystemLog.SetupGet(x => x.Description).Returns(LogType.SystemLog.ToString());

            SetupLogList(new[] { deviceSystemLog.Object });
            _logs
                .Setup(x => x.Create("device-" + DeviceName + "-mocked_timestamp.log", LogType.SystemLog.ToString(), It.IsAny<bool?>()))
                .Returns(deviceSystemLog.Object);

            var deviceLogCapturer = new Mock<IDeviceLogCapturer>();

            var deviceLogCapturerFactory = new Mock<IDeviceLogCapturerFactory>();
            deviceLogCapturerFactory
                .Setup(x => x.Create(_mainLog.Object, deviceSystemLog.Object, DeviceName))
                .Returns(deviceLogCapturer.Object);

            var x = _logs.Object.First();

            var appInformation = GetMockedAppBundleInfo();

            // Act
            var appRunner = new AppRunner(_processManager.Object,
                _hardwareDeviceLoader.Object,
                _simulatorLoader.Object,
                _snapshotReporterFactory,
                Mock.Of<ICaptureLogFactory>(),
                deviceLogCapturerFactory.Object,
                _mainLog.Object,
                _logs.Object,
                _helpers.Object);

            var (deviceName, result) = await appRunner.RunApp(
                appInformation,
                new TestTargetOs(TestTarget.Device_iOS, null),
                TimeSpan.FromSeconds(30),
                new[] { "--foo=bar", "--xyz" },
                new[] { ("appArg1", "value1") });

            // Verify
            Assert.Equal(DeviceName, deviceName);
            Assert.True(result.Succeeded);

            var expectedArgs = GetExpectedDeviceMlaunchArgs();

            _processManager
                .Verify(
                    x => x.ExecuteCommandAsync(
                       It.Is<MlaunchArguments>(args => args.AsCommandLine() == expectedArgs),
                       It.IsAny<ILog>(),
                       It.IsAny<TimeSpan>(),
                       It.IsAny<Dictionary<string, string>>(),
                       It.IsAny<CancellationToken>()),
                    Times.Once);

            _hardwareDeviceLoader.VerifyAll();

            _snapshotReporter.Verify(x => x.StartCaptureAsync(), Times.AtLeastOnce);
            _snapshotReporter.Verify(x => x.StartCaptureAsync(), Times.AtLeastOnce);

            deviceSystemLog.Verify(x => x.Dispose(), Times.AtLeastOnce);
        }

        private static AppBundleInformation GetMockedAppBundleInfo() =>
            new AppBundleInformation(
                appName: AppName,
                bundleIdentifier: AppBundleIdentifier,
                appPath: s_appPath,
                launchAppPath: s_appPath,
                supports32b: false,
                extension: null);

        private static string GetExpectedDeviceMlaunchArgs() =>
            "-v " +
            "-v " +
            "-argument=--foo=bar " +
            "-argument=--xyz " +
            "-setenv=appArg1=value1 " +
            "--disable-memory-limits " +
            $"--devname \"{DeviceName}\" " +
            $"--launchdev {StringUtils.FormatArguments(s_appPath)} " +
            "--wait-for-exit";

        private string GetExpectedSimulatorMlaunchArgs() =>
            "-v " +
            "-v " +
            "-argument=--foo=bar " +
            "-argument=--xyz " +
            "-setenv=appArg1=value1 " +
            $"--device=:v2:udid={_mockSimulator.Object.UDID} " +
            $"--launchsim {StringUtils.FormatArguments(s_appPath)}";

        private void SetupLogList(IEnumerable<IFileBackedLog> logs)
        {
            _logs
                .Setup(x => x.GetEnumerator())
                .Returns(() => logs.GetEnumerator());

            _logs
                .Setup(m => m.Count)
                .Returns(() => logs.Count());

            _logs
                .Setup(m => m[It.IsAny<int>()])
                .Returns<int>(i => logs.ElementAt(i));
        }
    }
}
