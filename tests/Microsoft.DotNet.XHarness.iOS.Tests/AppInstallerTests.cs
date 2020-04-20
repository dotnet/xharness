using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.iOS;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution.Mlaunch;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Moq;
using NUnit.Framework;

namespace Xharness.Tests
{
    [TestFixture]
    public class AppInstallerTests
    {
        private static readonly string s_appPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        private static readonly IHardwareDevice s_mockDevice = new Device(
            buildVersion: "17A577",
            deviceClass: DeviceClass.iPhone,
            deviceIdentifier: "8A450AA31EA94191AD6B02455F377CC1",
            interfaceType: "Usb",
            isUsableForDebugging: true,
            name: "Test iPhone",
            productType: "iPhone12,1",
            productVersion: "13.0");

        Mock<IProcessManager> _processManager;
        Mock<ILog> _mainLog;
        Mock<IHardwareDeviceLoader> _hardwareDeviceLoader;

        [SetUp]
        public void SetUp()
        {
            _mainLog = new Mock<ILog>();

            _processManager = new Mock<IProcessManager>();
            _processManager.SetReturnsDefault(Task.FromResult(new ProcessExecutionResult() { ExitCode = 0 }));

            _hardwareDeviceLoader = new Mock<IHardwareDeviceLoader>();
            _hardwareDeviceLoader
                .Setup(x => x.FindDevice(RunMode.iOS, _mainLog.Object, false, false))
                .ReturnsAsync(s_mockDevice);

            Directory.CreateDirectory(s_appPath);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(s_appPath, true);
        }

        [Test]
        public void InstallToSimulatorTest()
        {
            var appInstaller = new AppInstaller(_processManager.Object, _hardwareDeviceLoader.Object, _mainLog.Object, 1);

            var exception = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await appInstaller.InstallApp(s_appPath, TestTarget.Simulator_iOS64),
                "Install should not be allowed on a simulator");
        }

        [Test]
        public void InstallWhenNoDevicesTest()
        {
            _hardwareDeviceLoader = new Mock<IHardwareDeviceLoader>();
            _hardwareDeviceLoader
                .Setup(x => x.FindDevice(RunMode.iOS, _mainLog.Object, false, false))
                .ThrowsAsync(new NoDeviceFoundException());

            var appInstaller = new AppInstaller(_processManager.Object, _hardwareDeviceLoader.Object, _mainLog.Object, 1);

            Assert.ThrowsAsync<NoDeviceFoundException>(
                async () => await appInstaller.InstallApp(s_appPath, TestTarget.Device_iOS),
                "Install requires connected devices");
        }

        [Test]
        public async Task InstallOnDeviceTest()
        {
            // Act
            var appInstaller = new AppInstaller(_processManager.Object, _hardwareDeviceLoader.Object, _mainLog.Object, 2);

            var (deviceName, result) = await appInstaller.InstallApp(s_appPath, TestTarget.Device_iOS);

            // Verify
            Assert.AreEqual(0, result.ExitCode);
            Assert.AreEqual(s_mockDevice.Name, deviceName);

            var expectedArgs = $"-v -v -v --installdev {StringUtils.FormatArguments(s_appPath)} --devname \"{s_mockDevice.Name}\"";

            _processManager.Verify(x => x.ExecuteCommandAsync(
               It.Is<MlaunchArguments>(args => args.AsCommandLine() == expectedArgs),
               _mainLog.Object,
               It.IsAny<TimeSpan>(),
               null,
               It.IsAny<CancellationToken>()));
        }

        [Test]
        public async Task InstallOnPredefinedDeviceTest()
        {
            // Act
            var appInstaller = new AppInstaller(_processManager.Object, _hardwareDeviceLoader.Object, _mainLog.Object, 2);

            var (deviceName, result) = await appInstaller.InstallApp(s_appPath, TestTarget.Device_iOS, deviceName: "OtherDevice");

            // Verify
            Assert.AreEqual(0, result.ExitCode);
            Assert.AreEqual("OtherDevice", deviceName);

            var expectedArgs = $"-v -v -v --installdev {StringUtils.FormatArguments(s_appPath)} --devname OtherDevice";

            _processManager.Verify(x => x.ExecuteCommandAsync(
               It.Is<MlaunchArguments>(args => args.AsCommandLine() == expectedArgs),
               _mainLog.Object,
               It.IsAny<TimeSpan>(),
               null,
               It.IsAny<CancellationToken>()));
        }
    }
}
