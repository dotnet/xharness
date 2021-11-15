// Licensed to the .NET Foundation under one or more agreements.
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
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.Apple.Tests.AppOperations;

public abstract class AppOrchestratorTestBase : OrchestratorTestBase
{
    protected readonly Mock<IMlaunchProcessManager> _processManager;
    protected readonly Mock<IAppRunnerFactory> _appRunnerFactory;

    public AppOrchestratorTestBase()
    {
        _processManager = new();
        _appRunnerFactory = new();

        // Prepare succeeding install/uninstall as we don't care about those in the test/run tests
        _appInstaller.SetReturnsDefault(Task.FromResult(new ProcessExecutionResult
        {
            ExitCode = 0,
            TimedOut = false,
        }));

        _appUninstaller.SetReturnsDefault(Task.FromResult(new ProcessExecutionResult
        {
            ExitCode = 0,
            TimedOut = false,
        }));
    }
}

public class RunOrchestratorTests : AppOrchestratorTestBase
{
    private readonly RunOrchestrator _runOrchestrator;
    private readonly Mock<IExitCodeDetector> _exitCodeDetector;
    private readonly Mock<IAppRunner> _appRunner;

    public RunOrchestratorTests()
    {
        _exitCodeDetector = new();
        _appRunner = new();
        _appRunnerFactory.SetReturnsDefault(_appRunner.Object);

        _runOrchestrator = new(
            _appInstaller.Object,
            _appUninstaller.Object,
            _appRunnerFactory.Object,
            _processManager.Object,
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
    public async Task OrchestrateSimulatorInstallationTest()
    {
        // Setup
        var testTarget = new TestTargetOs(TestTarget.Simulator_iOS64, "13.5");

        var envVars = new[] { ("envVar1", "value1"), ("envVar2", "value2") };

        _exitCodeDetector
            .Setup(x => x.DetectExitCode(_appBundleInformation, It.IsAny<IReadableLog>()))
            .Returns(100)
            .Verifiable();

        _appRunner
            .Setup(x => x.RunApp(_appBundleInformation, testTarget, _simulator.Object, null, TimeSpan.FromMinutes(30), false, It.IsAny<IEnumerable<string>>(), envVars, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = 0,
                TimedOut = false,
            })
            .Verifiable();

        // Act
        var result = await _runOrchestrator.OrchestrateRun(
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

        VerifySimulatorReset(true);
        VerifySimulatorCleanUp(true);
        VerifyDiagnosticData(testTarget);

        _appInstaller.Verify(
            x => x.InstallApp(_appBundleInformation, testTarget, _simulator.Object, It.IsAny<CancellationToken>()),
            Times.Once);

        _exitCodeDetector.VerifyAll();
        _appRunner.VerifyAll();
    }

    [Fact]
    public async Task OrchestrateDeviceInstallationTest()
    {
        // Setup
        var testTarget = new TestTargetOs(TestTarget.Device_iOS, "14.2");

        _exitCodeDetector
            .Setup(x => x.DetectExitCode(_appBundleInformation, It.IsAny<IReadableLog>()))
            .Returns(100)
            .Verifiable();

        var extraArguments = new[] { "--some arg1", "--some arg2" };

        _appRunner
            .Setup(x => x.RunApp(_appBundleInformation, testTarget, _device.Object, null, TimeSpan.FromMinutes(30), false, extraArguments, It.IsAny<IEnumerable<(string, string)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = 0,
                TimedOut = false,
            })
            .Verifiable();

        // Act
        var result = await _runOrchestrator.OrchestrateRun(
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

        _appInstaller.Verify(
            x => x.InstallApp(_appBundleInformation, testTarget, _device.Object, It.IsAny<CancellationToken>()),
            Times.Once);

        _exitCodeDetector.VerifyAll();
        _appRunner.VerifyAll();
    }
}
