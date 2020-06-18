// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.Common.Utilities;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution.Mlaunch;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Tests
{
    public class AppInstallerTests : IDisposable
    {
        private static readonly string s_appPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        private static readonly string s_appIdentifier = Guid.NewGuid().ToString();
        private static readonly IHardwareDevice s_mockDevice = new Device(
            buildVersion: "17A577",
            deviceClass: DeviceClass.iPhone,
            deviceIdentifier: "8A450AA31EA94191AD6B02455F377CC1",
            interfaceType: "Usb",
            isUsableForDebugging: true,
            name: "Test iPhone",
            productType: "iPhone12,1",
            productVersion: "13.0");
        private readonly Mock<IMLaunchProcessManager> _processManager;
        private readonly Mock<ILog> _mainLog;
        private readonly AppBundleInformation _appBundleInformation;
        private Mock<IHardwareDeviceLoader> _hardwareDeviceLoader;

        public AppInstallerTests()
        {
            _mainLog = new Mock<ILog>();

            _processManager = new Mock<IMLaunchProcessManager>();
            _processManager.SetReturnsDefault(Task.FromResult(new ProcessExecutionResult() { ExitCode = 0 }));

            _hardwareDeviceLoader = new Mock<IHardwareDeviceLoader>();
            _hardwareDeviceLoader
                .Setup(x => x.Connected64BitIOS).Returns(new List<IHardwareDevice> { s_mockDevice });

            Directory.CreateDirectory(s_appPath);
            _appBundleInformation = new AppBundleInformation(
                appName: "AppName",
                bundleIdentifier: s_appIdentifier,
                appPath: s_appPath,
                launchAppPath: s_appPath,
                supports32b: false,
                extension: null);
        }

        public void Dispose() => Directory.Delete(s_appPath, true);

        [Fact]
        public async Task InstallToSimulatorTest()
        {
            var appInstaller = new AppInstaller(_processManager.Object, _hardwareDeviceLoader.Object, _mainLog.Object, 1);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await appInstaller.InstallApp(_appBundleInformation, new TestTargetOs(TestTarget.Simulator_iOS64, null)));
        }

        [Fact]
        public async Task InstallWhenNoDevicesTest()
        {
            _hardwareDeviceLoader = new Mock<IHardwareDeviceLoader>();
            _hardwareDeviceLoader
                .Setup(x => x.FindDevice(RunMode.iOS, _mainLog.Object, false, false))
                .ThrowsAsync(new NoDeviceFoundException());

            var appInstaller = new AppInstaller(_processManager.Object, _hardwareDeviceLoader.Object, _mainLog.Object, 1);

            await Assert.ThrowsAsync<NoDeviceFoundException>(
                async () => await appInstaller.InstallApp(_appBundleInformation, new TestTargetOs(TestTarget.Device_iOS, null)));
        }

        [Fact]
        public async Task InstallOnDeviceTest()
        {
            // Act
            var appInstaller = new AppInstaller(_processManager.Object, _hardwareDeviceLoader.Object, _mainLog.Object, 2);

            var (deviceName, result) = await appInstaller.InstallApp(_appBundleInformation, new TestTargetOs(TestTarget.Device_iOS, null));

            // Verify
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(s_mockDevice.Name, deviceName);

            var expectedArgs = $"-v -v -v --installdev {StringUtils.FormatArguments(s_appPath)} --devname \"{s_mockDevice.Name}\"";

            _processManager.Verify(x => x.ExecuteCommandAsync(
               It.Is<MlaunchArguments>(args => args.AsCommandLine() == expectedArgs),
               _mainLog.Object,
               It.IsAny<TimeSpan>(),
               null,
               It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task InstallOnPredefinedDeviceTest()
        {
            // Act
            var appInstaller = new AppInstaller(_processManager.Object, _hardwareDeviceLoader.Object, _mainLog.Object, 2);

            var (deviceName, result) = await appInstaller.InstallApp(_appBundleInformation, new TestTargetOs(TestTarget.Device_iOS, null), deviceName: "OtherDevice");

            // Verify
            Assert.Equal(0, result.ExitCode);
            Assert.Equal("OtherDevice", deviceName);

            var expectedArgs = $"-v -v -v --installdev {StringUtils.FormatArguments(s_appPath)} --devname OtherDevice";

            _processManager.Verify(x => x.ExecuteCommandAsync(
               It.Is<MlaunchArguments>(args => args.AsCommandLine() == expectedArgs),
               _mainLog.Object,
               It.IsAny<TimeSpan>(),
               null,
               It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task InstallOn32bMissingDevice()
        {
            var appBundle32b = new AppBundleInformation(
                appName: "AppName",
                bundleIdentifier: s_appIdentifier,
                appPath: s_appPath,
                launchAppPath: s_appPath,
                supports32b: true,
                extension: null);

            var appInstaller = new AppInstaller(_processManager.Object, _hardwareDeviceLoader.Object, _mainLog.Object, 1);

            await Assert.ThrowsAsync<NoDeviceFoundException>(
                async () => await appInstaller.InstallApp(appBundle32b, new TestTargetOs(TestTarget.Device_iOS, null)));
        }
    }
}
