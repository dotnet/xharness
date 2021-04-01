// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.Common.Utilities;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.Apple.Tests
{
    public class AppRunnerTests : AppRunTestBase
    {
        [Fact]
        public async Task RunOnSimulatorWithNoAvailableSimulatorTest()
        {
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
            var appRunner = new AppRunner(
                _processManager.Object,
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
                    _mockSimulator,
                    timeout: TimeSpan.FromSeconds(30),
                    extraAppArguments: Array.Empty<string>(),
                    extraEnvVariables: Array.Empty<(string, string)>()));

            // Verify
            _mainLog.Verify(x => x.WriteLine("App run ended with 0"), Times.Never);
        }

        [Fact]
        public async Task RunOnSimulatorSuccessfullyTest()
        {
            var captureLog = new Mock<ICaptureLog>();
            captureLog.SetupGet(x => x.FullPath).Returns(_simulatorLogPath);
            captureLog.SetupGet(x => x.Description).Returns(LogType.SystemLog.ToString());

            var captureLogFactory = new Mock<ICaptureLogFactory>();
            captureLogFactory
                .Setup(x => x.Create(
                   Path.Combine(_logs.Object.Directory, _mockSimulator.Name + ".log"),
                   _mockSimulator.SystemLog,
                   false,
                   LogType.SystemLog))
                .Returns(captureLog.Object);

            SetupLogList(new[] { captureLog.Object });

            var appInformation = GetMockedAppBundleInfo();

            // Act
            var appRunner = new AppRunner(
                _processManager.Object,
                _snapshotReporterFactory,
                captureLogFactory.Object,
                Mock.Of<IDeviceLogCapturerFactory>(),
                _mainLog.Object,
                _logs.Object,
                _helpers.Object);

            var result = await appRunner.RunApp(
                appInformation,
                new TestTargetOs(TestTarget.Simulator_tvOS, null),
                _mockSimulator,
                timeout: TimeSpan.FromSeconds(30),
                extraAppArguments: new[] { "--foo=bar", "--xyz" },
                extraEnvVariables: new[] { ("appArg1", "value1") });

            // Verify
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

            captureLog.Verify(x => x.StartCapture(), Times.AtLeastOnce);
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
            var appRunner = new AppRunner(
                _processManager.Object,
                _snapshotReporterFactory,
                Mock.Of<ICaptureLogFactory>(),
                deviceLogCapturerFactory.Object,
                _mainLog.Object,
                _logs.Object,
                _helpers.Object);

            var result = await appRunner.RunApp(
                appInformation,
                new TestTargetOs(TestTarget.Device_iOS, null),
                s_mockDevice,
                timeout: TimeSpan.FromSeconds(30),
                extraAppArguments: new[] { "--foo=bar", "--xyz" },
                extraEnvVariables: new[] { ("appArg1", "value1") });

            // Verify
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
            $"--devname \"{s_mockDevice.DeviceIdentifier}\" " +
            $"--launchdev {StringUtils.FormatArguments(s_appPath)} " +
            "--wait-for-exit";

        private string GetExpectedSimulatorMlaunchArgs() =>
            "-v " +
            "-v " +
            "-argument=--foo=bar " +
            "-argument=--xyz " +
            "-setenv=appArg1=value1 " +
            $"--device=:v2:udid={_mockSimulator.UDID} " +
            $"--launchsimbundleid {StringUtils.FormatArguments(s_appPath)}";

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
