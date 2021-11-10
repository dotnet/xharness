// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.Apple.Tests.AppOperations;

public class InstallOrchestratorTests
{
    private static readonly string s_appIdentifier = Guid.NewGuid().ToString();
    private const string UDID = "8A450AA31EA94191AD6B02455F377CC1";
    private const string DeviceName = "Some iPhone";
    private const string AppName = "System.Buffers.Tests";
    private const string AppPath = "/tmp/apps/System.Buffers.Tests.app";

    private readonly Mock<IDeviceFinder> _deviceFinder;
    private readonly Mock<IAppBundleInformationParser> _appBundleInformationParser;
    private readonly Mock<IErrorKnowledgeBase> _errorKnowledgeBase;
    private readonly Mock<IFileBackedLog> _mainLog;
    private readonly Mock<ILogger> _logger;
    private readonly Mock<ILogs> _logs;
    private readonly Mock<IHelpers> _helpers;
    private readonly Mock<IAppInstaller> _appInstaller;
    private readonly Mock<IAppUninstaller> _appUninstaller;

    private readonly AppBundleInformation _appBundleInformation;
    private readonly IDiagnosticsData _diagnosticsData;
    private readonly InstallOrchestrator _installOrchestrator;

    private readonly Mock<ISimulatorDevice>? _simulator;
    private readonly Mock<ISimulatorDevice>? _device;

    public InstallOrchestratorTests()
    {
        _logger = new();
        _mainLog = new();
        _logs = new();
        _helpers = new();
        _errorKnowledgeBase = new();
        _appInstaller = new();
        _appUninstaller = new();

        _simulator = new();
        _simulator.Setup(x => x.UDID).Returns(UDID);
        _simulator.Setup(x => x.Name).Returns(DeviceName);
        _simulator.Setup(x => x.OSVersion).Returns("13.5");

        _device = new();
        _device.Setup(x => x.UDID).Returns(UDID);
        _device.Setup(x => x.Name).Returns(DeviceName);
        _device.Setup(x => x.OSVersion).Returns("14.2");

        _appBundleInformation = new AppBundleInformation(
            appName: AppName,
            bundleIdentifier: s_appIdentifier,
            appPath: AppPath,
            launchAppPath: AppPath,
            supports32b: false,
            extension: null);

        _appBundleInformationParser = new Mock<IAppBundleInformationParser>();
        _appBundleInformationParser
            .Setup(x => x.ParseFromAppBundle(
                AppPath,
                It.IsAny<TestTarget>(),
                _mainLog.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_appBundleInformation);

        _diagnosticsData = new CommandDiagnostics(Mock.Of<Extensions.Logging.ILogger>(), TargetPlatform.Apple, "install");

        _deviceFinder = new Mock<IDeviceFinder>();

        _deviceFinder
            .Setup(x => x.FindDevice(
                It.Is<TestTargetOs>(t => t.Platform.IsSimulator()),
                It.IsAny<string?>(),
                It.IsAny<ILog>(),
                It.IsAny<bool>()))
            .ReturnsAsync((_simulator.Object, null));

        _deviceFinder
            .Setup(x => x.FindDevice(
                It.Is<TestTargetOs>(t => !t.Platform.IsSimulator()),
                It.IsAny<string?>(),
                It.IsAny<ILog>(),
                It.IsAny<bool>()))
            .ReturnsAsync((_device.Object, null));

        _installOrchestrator = new(
            _appInstaller.Object,
            _appUninstaller.Object,
            _appBundleInformationParser.Object,
            _deviceFinder.Object,
            _logger.Object,
            _logs.Object,
            _mainLog.Object,
            _errorKnowledgeBase.Object,
            _diagnosticsData,
            _helpers.Object);
    }

    [Fact]
    public async Task OrchestrateSimulatorInstallationTest()
    {
        // Setup
        _appInstaller
            .Setup(x => x.InstallApp(_appBundleInformation, It.IsAny<TestTargetOs>(), _simulator!.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = 0,
                TimedOut = false,
            });

        _appUninstaller
            .Setup(x => x.UninstallSimulatorApp(_simulator!.Object, s_appIdentifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = 1, // This can fail as this is the first purge of the app before we install it
                    TimedOut = false,
            });

        // Act
        var result = await _installOrchestrator.OrchestrateInstall(
            new(TestTarget.Simulator_iOS64, "13.5"),
            null,
            AppPath,
            TimeSpan.FromMinutes(30),
            includeWirelessDevices: false,
            resetSimulator: false,
            enableLldb: false,
            new CancellationToken());

        // Verify
        Assert.Equal(ExitCode.SUCCESS, result);

        _deviceFinder
            .Verify(x => x.FindDevice(It.Is<TestTargetOs>(
                t => t.Platform == TestTarget.Simulator_iOS64 && t.OSVersion == "13.5"), null, It.IsAny<ILog>(), false),
                Times.Once);

        VerifySimulatorReset(false);
        VerifySimulatorCleanUp(false);

        _appInstaller.Verify(
            x => x.InstallApp(
                _appBundleInformation,
                It.Is<TestTargetOs>(t => t.Platform == TestTarget.Simulator_iOS64 && t.OSVersion == "13.5"),
                _simulator!.Object,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OrchestrateSimulatorInstallationWithResetTest()
    {
        // Setup
        _appInstaller
            .Setup(x => x.InstallApp(_appBundleInformation, It.IsAny<TestTargetOs>(), _simulator!.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = 0,
                TimedOut = false,
            });

        _appUninstaller
            .Setup(x => x.UninstallSimulatorApp(_simulator!.Object, s_appIdentifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = 1, // This can fail as this is the first purge of the app before we install it
                    TimedOut = false,
            });

        // Act
        var result = await _installOrchestrator.OrchestrateInstall(
            new(TestTarget.Simulator_tvOS, "13.5"),
            null,
            AppPath,
            TimeSpan.FromMinutes(30),
            includeWirelessDevices: true,
            resetSimulator: true,
            enableLldb: true,
            new CancellationToken());

        // Verify
        Assert.Equal(ExitCode.SUCCESS, result);

        _deviceFinder
            .Verify(x => x.FindDevice(It.Is<TestTargetOs>(
                t => t.Platform == TestTarget.Simulator_tvOS && t.OSVersion == "13.5"), null, It.IsAny<ILog>(), true),
                Times.Once);

        VerifySimulatorReset(true);
        VerifySimulatorCleanUp(false); // Install doesn't end with a cleanup so that the app stays behind

        _appInstaller.Verify(
            x => x.InstallApp(
                _appBundleInformation,
                It.Is<TestTargetOs>(t => t.Platform == TestTarget.Simulator_tvOS && t.OSVersion == "13.5"),
                _simulator!.Object,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OrchestrateDeviceInstallationTest()
    {
        // Setup
        _appInstaller
            .Setup(x => x.InstallApp(_appBundleInformation, It.IsAny<TestTargetOs>(), _device!.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = 0,
                TimedOut = false,
            });

        _appUninstaller
            .Setup(x => x.UninstallDeviceApp(_device!.Object, s_appIdentifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = 1, // This can fail as this is the first purge of the app before we install it
                TimedOut = false,
            });

        // Act
        var result = await _installOrchestrator.OrchestrateInstall(
            new(TestTarget.Device_iOS, "14.2"),
            null,
            AppPath,
            TimeSpan.FromMinutes(30),
            includeWirelessDevices: false,
            resetSimulator: false,
            enableLldb: true,
            new CancellationToken());

        // Verify
        Assert.Equal(ExitCode.SUCCESS, result);

        VerifySimulatorReset(false);
        VerifySimulatorCleanUp(false);

        _deviceFinder
            .Verify(x => x.FindDevice(It.Is<TestTargetOs>(
                t => t.Platform == TestTarget.Device_iOS && t.OSVersion == "13.5"), null, It.IsAny<ILog>(), false),
                Times.Once);

        _appInstaller.Verify(
            x => x.InstallApp(
                _appBundleInformation,
                It.Is<TestTargetOs>(t => t.Platform == TestTarget.Device_iOS && t.OSVersion == "14.2"),
                _device!.Object,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private void VerifySimulatorReset(bool shouldBeReset)
    {
        _simulator!.Verify(
            x => x.PrepareSimulator(It.IsAny<ILog>(), It.IsAny<string[]>()),
            shouldBeReset ? Times.Once : Times.Never);
    }

    private void VerifySimulatorCleanUp(bool shouldBeCleanedUp)
    {
        _simulator!.Verify(
            x => x.KillEverything(It.IsAny<ILog>()),
            shouldBeCleanedUp ? Times.Once : Times.Never);
    }
}
