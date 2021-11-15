// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.Apple.Tests.AppOperations;

public class InstallOrchestratorTests : OrchestratorTestBase
{
    private readonly InstallOrchestrator _installOrchestrator;

    public InstallOrchestratorTests()
    {
        _installOrchestrator = new(
            _appInstaller.Object,
            _appUninstaller.Object,
            _appBundleInformationParser.Object,
            _deviceFinder.Object,
            _logger.Object,
            _logs,
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
            .Setup(x => x.InstallApp(_appBundleInformation, It.IsAny<TestTargetOs>(), _simulator.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = 0,
                TimedOut = false,
            });

        _appUninstaller
            .Setup(x => x.UninstallSimulatorApp(_simulator.Object, s_appIdentifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = 1, // This can fail as this is the first purge of the app before we install it
                TimedOut = false,
            });

        var testTarget = new TestTargetOs(TestTarget.Simulator_iOS64, "13.5");

        // Act
        var result = await _installOrchestrator.OrchestrateInstall(
            testTarget,
            null,
            AppPath,
            TimeSpan.FromMinutes(30),
            includeWirelessDevices: false,
            resetSimulator: false,
            enableLldb: false,
            new CancellationToken());

        // Verify
        Assert.Equal(ExitCode.SUCCESS, result);

        _deviceFinder.Verify(
            x => x.FindDevice(testTarget, null, It.IsAny<ILog>(), false),
            Times.Once);

        VerifySimulatorReset(false);
        VerifySimulatorCleanUp(false);
        VerifyDiagnosticData(testTarget);

        _appInstaller.Verify(
            x => x.InstallApp(_appBundleInformation, testTarget, _simulator.Object, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OrchestrateSimulatorInstallationWithResetTest()
    {
        // Setup
        _appInstaller
            .Setup(x => x.InstallApp(_appBundleInformation, It.IsAny<TestTargetOs>(), _simulator.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = 0,
                TimedOut = false,
            });

        _appUninstaller
            .Setup(x => x.UninstallSimulatorApp(_simulator.Object, s_appIdentifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = 1, // This can fail as this is the first purge of the app before we install it
                    TimedOut = false,
            });

        var testTarget = new TestTargetOs(TestTarget.Simulator_tvOS, "13.5");

        // Act
        var result = await _installOrchestrator.OrchestrateInstall(
            testTarget,
            null,
            AppPath,
            TimeSpan.FromMinutes(30),
            includeWirelessDevices: true,
            resetSimulator: true,
            enableLldb: true,
            new CancellationToken());

        // Verify
        Assert.Equal(ExitCode.SUCCESS, result);

        _deviceFinder.Verify(
            x => x.FindDevice(testTarget, null, It.IsAny<ILog>(), true),
            Times.Once);

        VerifySimulatorReset(true);
        VerifySimulatorCleanUp(false); // Install doesn't end with a cleanup so that the app stays behind
        VerifyDiagnosticData(testTarget);

        _appInstaller.Verify(
            x => x.InstallApp(_appBundleInformation, testTarget, _simulator.Object, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OrchestrateDeviceInstallationTest()
    {
        // Setup
        _appInstaller
            .Setup(x => x.InstallApp(_appBundleInformation, It.IsAny<TestTargetOs>(), _device.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = 0,
                TimedOut = false,
            });

        _appUninstaller
            .Setup(x => x.UninstallDeviceApp(_device.Object, s_appIdentifier, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = 1, // This can fail as this is the first purge of the app before we install it
                TimedOut = false,
            });

        var testTarget = new TestTargetOs(TestTarget.Device_iOS, "14.2");

        // Act
        var result = await _installOrchestrator.OrchestrateInstall(
            testTarget,
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
        VerifyDiagnosticData(testTarget);

        _deviceFinder.Verify(
            x => x.FindDevice(testTarget, null, It.IsAny<ILog>(), false),
            Times.Once);

        _appInstaller.Verify(
            x => x.InstallApp(_appBundleInformation, testTarget, _device.Object, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OrchestrateMacCatalystInstallationTest()
    {
        var testTarget = new TestTargetOs(TestTarget.MacCatalyst, null);

        // Act
        var result = await _installOrchestrator.OrchestrateInstall(
            testTarget,
            null,
            AppPath,
            TimeSpan.FromMinutes(30),
            includeWirelessDevices: false,
            resetSimulator: false,
            enableLldb: true,
            new CancellationToken());

        // Verify
        _deviceFinder.Verify(
            x => x.FindDevice(testTarget, It.IsAny<string>(), It.IsAny<ILog>(), It.IsAny<bool>()),
            Times.Never);

        VerifySimulatorReset(false);
        VerifySimulatorCleanUp(false);

        _appInstaller.Verify(
            x => x.InstallApp(It.IsAny<AppBundleInformation>(), It.IsAny<TestTargetOs>(), It.IsAny<IDevice>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _appUninstaller.Verify(
            x => x.UninstallDeviceApp(It.IsAny<IDevice>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _appUninstaller.Verify(
            x => x.UninstallSimulatorApp(It.IsAny<IDevice>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OrchestrateDeviceInstallationWhenNoDeviceTest()
    {
        // Setup
        _deviceFinder.Reset();
        _deviceFinder
            .Setup(x => x.FindDevice(
                It.IsAny<TestTargetOs>(),
                It.IsAny<string?>(),
                It.IsAny<ILog>(),
                It.IsAny<bool>()))
            .ThrowsAsync(new NoDeviceFoundException());

        var testTarget = new TestTargetOs(TestTarget.Device_iOS, "14.2");

        // Act
        var result = await _installOrchestrator.OrchestrateInstall(
            testTarget,
            null,
            AppPath,
            TimeSpan.FromMinutes(30),
            includeWirelessDevices: false,
            resetSimulator: false,
            enableLldb: true,
            new CancellationToken());

        // Verify
        Assert.Equal(ExitCode.DEVICE_NOT_FOUND, result);
    }
}
