﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.Apple.Tests.Orchestration;

public class JustRunOrchestratorTests : OrchestratorTestBase
{
    private readonly JustRunOrchestrator _justRunOrchestrator;
    private readonly Mock<IExitCodeDetector> _exitCodeDetector;
    private readonly Mock<IAppRunner> _appRunner;
    private readonly Mock<IAppRunnerFactory> _appRunnerFactory;

    public JustRunOrchestratorTests()
    {
        _exitCodeDetector = new();
        _appRunner = new();

        _appRunnerFactory = new();
        _appRunnerFactory.SetReturnsDefault(_appRunner.Object);

        // These two shouldn't get invoked at all
        _appInstaller.Reset();
        _appUninstaller.Reset();

        _justRunOrchestrator = new(
            _appInstaller.Object,
            _appUninstaller.Object,
            _appRunnerFactory.Object,
            _deviceFinder.Object,
            _exitCodeDetector.Object,
            _exitCodeDetector.Object,
            _logger.Object,
            _logs,
            _mainLog.Object,
            _errorKnowledgeBase.Object,
            _diagnosticsData,
            _helpers.Object);
    }

    [Fact]
    public async Task OrchestrateSimulatorJustRunTest()
    {
        // Setup
        var testTarget = new TestTargetOs(TestTarget.Simulator_iOS64, "13.5");

        var envVars = new[] { ("envVar1", "value1"), ("envVar2", "value2") };

        _exitCodeDetector
            .Setup(x => x.DetectExitCode(_appBundleInformation, It.IsAny<IReadableLog>()))
            .Returns(100)
            .Verifiable();

        _appRunner
            .Setup(x => x.RunApp(
                _appBundleInformation,
                testTarget,
                _simulator.Object,
                null,
                TimeSpan.FromMinutes(30),
                false,
                It.IsAny<IEnumerable<string>>(),
                envVars,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = 0,
                TimedOut = false,
            })
            .Verifiable();

        // Act
        var result = await _justRunOrchestrator.OrchestrateRun(
            _appBundleInformation,
            testTarget,
            null,
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(3),
            expectedExitCode: 100,
            includeWirelessDevices: false,
            resetSimulator: true,
            enableLldb: false,
            signalAppEnd: false,
            envVars,
            Array.Empty<string>(),
            new CancellationToken());

        // Verify
        Assert.Equal(ExitCode.SUCCESS, result);

        _deviceFinder.Verify(
            x => x.FindDevice(testTarget, null, It.IsAny<ILog>(), false),
            Times.Once);

        VerifySimulatorReset(false);
        VerifySimulatorCleanUp(false);
        VerifyDiagnosticData(testTarget);

        _exitCodeDetector.VerifyAll();
        _appRunner.VerifyAll();
        _appInstaller.VerifyNoOtherCalls();
        _appUninstaller.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OrchestrateDeviceJustRunTest()
    {
        // Setup
        var testTarget = new TestTargetOs(TestTarget.Device_iOS, "14.2");

        _exitCodeDetector
            .Setup(x => x.DetectExitCode(_appBundleInformation, It.IsAny<IReadableLog>()))
            .Returns(100)
            .Verifiable();

        var extraArguments = new[] { "--some arg1", "--some arg2" };

        _appRunner
            .Setup(x => x.RunApp(
                _appBundleInformation,
                testTarget,
                _device.Object,
                null,
                TimeSpan.FromMinutes(30),
                false,
                extraArguments,
                It.IsAny<IEnumerable<(string, string)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = 0,
                TimedOut = false,
            })
            .Verifiable();

        // Act
        var result = await _justRunOrchestrator.OrchestrateRun(
            _appBundleInformation,
            testTarget,
            DeviceName,
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(3),
            expectedExitCode: 100,
            includeWirelessDevices: true,
            resetSimulator: false,
            enableLldb: false,
            signalAppEnd: false,
            Array.Empty<(string, string)>(),
            extraArguments,
            new CancellationToken());

        // Verify
        Assert.Equal(ExitCode.SUCCESS, result);

        _deviceFinder.Verify(
            x => x.FindDevice(testTarget, DeviceName, It.IsAny<ILog>(), true),
            Times.Once);

        VerifySimulatorReset(false);
        VerifySimulatorCleanUp(false);
        VerifyDiagnosticData(testTarget);

        _exitCodeDetector.VerifyAll();
        _appRunner.VerifyAll();
        _appInstaller.VerifyNoOtherCalls();
        _appUninstaller.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OrchestrateFailedSimulatorJustRunTest()
    {
        // Setup
        var testTarget = new TestTargetOs(TestTarget.Simulator_iOS64, "13.5");

        _appRunner
            .Setup(x => x.RunApp(
                _appBundleInformation,
                testTarget,
                _simulator.Object,
                null,
                TimeSpan.FromMinutes(30),
                false,
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<(string, string)>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception())
            .Verifiable();

        var failure = new KnownIssue("Some failure", suggestedExitCode: (int)ExitCode.SIMULATOR_FAILURE);
        _errorKnowledgeBase
            .Setup(x => x.IsKnownTestIssue(It.IsAny<IFileBackedLog>(), out failure))
            .Returns(true)
            .Verifiable();

        // Act
        var result = await _justRunOrchestrator.OrchestrateRun(
            _appBundleInformation,
            testTarget,
            null,
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(3),
            expectedExitCode: 100,
            includeWirelessDevices: false,
            resetSimulator: false,
            enableLldb: true,
            signalAppEnd: false,
            Array.Empty<(string, string)>(),
            Array.Empty<string>(),
            new CancellationToken());

        // Verify
        Assert.Equal(ExitCode.SIMULATOR_FAILURE, result);

        _deviceFinder.Verify(
            x => x.FindDevice(testTarget, null, It.IsAny<ILog>(), false),
            Times.Once);

        VerifySimulatorReset(false);
        VerifySimulatorCleanUp(false);
        VerifyDiagnosticData(testTarget);

        _errorKnowledgeBase.VerifyAll();
        _appRunner.VerifyAll();
        _appInstaller.VerifyNoOtherCalls();
        _appUninstaller.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OrchestrateFailedDeviceJustRunTest()
    {
        // Setup
        var testTarget = new TestTargetOs(TestTarget.Device_iOS, "14.2");

        _exitCodeDetector
            .Setup(x => x.DetectExitCode(_appBundleInformation, It.IsAny<IReadableLog>()))
            .Returns(200)
            .Verifiable();

        var extraArguments = new[] { "--some arg1", "--some arg2" };

        _appRunner
            .Setup(x => x.RunApp(
                _appBundleInformation,
                testTarget,
                _device.Object,
                null,
                TimeSpan.FromMinutes(30),
                true,
                extraArguments,
                It.IsAny<IEnumerable<(string, string)>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = 0,
                TimedOut = false,
            })
            .Verifiable();

        var failure = new KnownIssue("Some failure", suggestedExitCode: (int)ExitCode.DEVICE_FAILURE);
        _errorKnowledgeBase
            .Setup(x => x.IsKnownTestIssue(It.IsAny<IFileBackedLog>(), out failure))
            .Returns(true)
            .Verifiable();

        // Act
        var result = await _justRunOrchestrator.OrchestrateRun(
            _appBundleInformation,
            testTarget,
            DeviceName,
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(3),
            expectedExitCode: 100,
            includeWirelessDevices: true,
            resetSimulator: false,
            enableLldb: false,
            signalAppEnd: true,
            Array.Empty<(string, string)>(),
            extraArguments,
            new CancellationToken());

        // Verify
        Assert.Equal(ExitCode.DEVICE_FAILURE, result);

        _deviceFinder.Verify(
            x => x.FindDevice(testTarget, DeviceName, It.IsAny<ILog>(), true),
            Times.Once);

        VerifySimulatorReset(false);
        VerifySimulatorCleanUp(false);
        VerifyDiagnosticData(testTarget);

        _exitCodeDetector.VerifyAll();
        _appRunner.VerifyAll();
        _appInstaller.VerifyNoOtherCalls();
        _appUninstaller.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OrchestrateMacCatalystJustRunTest()
    {
        // Setup
        _appInstaller.Reset();
        _appUninstaller.Reset();
        _deviceFinder.Reset();

        var testTarget = new TestTargetOs(TestTarget.MacCatalyst, null);

        var envVars = new[] { ("envVar1", "value1"), ("envVar2", "value2") };

        _exitCodeDetector
            .Setup(x => x.DetectExitCode(_appBundleInformation, It.IsAny<IReadableLog>()))
            .Returns(100)
            .Verifiable();

        _appRunner
            .Setup(x => x.RunMacCatalystApp(
                _appBundleInformation,
                TimeSpan.FromMinutes(30),
                true,
                It.IsAny<IEnumerable<string>>(),
                envVars,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = 0,
                TimedOut = false,
            })
            .Verifiable();

        // Act
        var result = await _justRunOrchestrator.OrchestrateRun(
            _appBundleInformation,
            testTarget,
            null,
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(3),
            expectedExitCode: 100,
            includeWirelessDevices: false,
            resetSimulator: true,
            enableLldb: false,
            signalAppEnd: true,
            envVars,
            Array.Empty<string>(),
            new CancellationToken());

        // Verify
        Assert.Equal(ExitCode.SUCCESS, result);

        VerifySimulatorReset(false);
        VerifySimulatorCleanUp(false);

        _exitCodeDetector.VerifyAll();
        _appRunner.VerifyAll();
        _deviceFinder.VerifyNoOtherCalls();
        _appInstaller.VerifyNoOtherCalls();
        _appUninstaller.VerifyNoOtherCalls();
    }
}