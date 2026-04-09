// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.Apple.Tests.Orchestration;

public class CopyLogsToMainLogTests : OrchestratorTestBase
{
    private readonly TestOrchestrator _testOrchestrator;
    private readonly Mock<IAppTester> _appTester;
    private readonly Mock<IAppTesterFactory> _appTesterFactory;
    private readonly List<string> _mainLogLines;

    private const string SuccessResultLine = "Tests run: 10 Passed: 10 Inconclusive: 0 Failed: 0 Ignored: 0";

    public CopyLogsToMainLogTests()
    {
        _appTester = new();
        _appTesterFactory = new();
        _appTesterFactory.SetReturnsDefault(_appTester.Object);

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

        _mainLogLines = new List<string>();
        _mainLog
            .Setup(x => x.WriteLine(It.IsAny<string>()))
            .Callback<string>(line => _mainLogLines.Add(line));

        _testOrchestrator = new(
            _appBundleInformationParser.Object,
            _appInstaller.Object,
            _appUninstaller.Object,
            _appTesterFactory.Object,
            _deviceFinder.Object,
            _logger.Object,
            _logs,
            _mainLog.Object,
            _errorKnowledgeBase.Object,
            _diagnosticsData,
            _helpers.Object);
    }

    [Fact]
    public async Task SimulatorTest_CopiesApplicationLogToMainLog()
    {
        // Setup: add an ApplicationLog with test output
        var appLogContent = "[PASS] MyTest.TestMethod1\n[PASS] MyTest.TestMethod2\n[FAIL] MyTest.TestMethod3\n";
        AddLogWithContent(LogType.ApplicationLog, "net.dot.Tests.log", appLogContent);

        // Also add a SystemLog (should NOT be copied for simulators)
        AddLogWithContent(LogType.SystemLog, "simulator.system.log", "System noise that should not appear");

        var testTarget = new TestTargetOs(TestTarget.Simulator_iOS64, "13.5");
        _appTester
            .Setup(x => x.TestApp(
                It.IsAny<AppBundleInformation>(), It.IsAny<TestTargetOs>(), It.IsAny<IDevice>(), It.IsAny<IDevice>(),
                It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<(string, string?)>>(), It.IsAny<XmlResultJargon>(),
                It.IsAny<string[]?>(), It.IsAny<string[]?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestExecutingResult.Succeeded, SuccessResultLine));

        // Act
        var result = await _testOrchestrator.OrchestrateTest(
            AppPath, testTarget, null,
            TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(3),
            CommunicationChannel.UsbTunnel, XmlResultJargon.xUnit,
            Array.Empty<string>(), Array.Empty<string>(),
            includeWirelessDevices: false, resetSimulator: true, enableLldb: false,
            signalAppEnd: false,
            Array.Empty<(string, string?)>(), Array.Empty<string>(),
            new CancellationToken());

        // Verify
        Assert.Equal(ExitCode.SUCCESS, result);
        var mainLogText = string.Join("\n", _mainLogLines);

        // ApplicationLog content should be present
        Assert.Contains("[PASS] MyTest.TestMethod1", mainLogText);
        Assert.Contains("[FAIL] MyTest.TestMethod3", mainLogText);
        Assert.Contains("==================== ApplicationLog ====================", mainLogText);
        Assert.Contains("==================== End of ApplicationLog ====================", mainLogText);

        // SystemLog content should NOT be present
        Assert.DoesNotContain("System noise that should not appear", mainLogText);
    }

    [Fact]
    public async Task MacCatalystTest_CopiesSystemLogToMainLog()
    {
        // Setup: reset mocks since MacCatalyst skips device finding, install, uninstall
        _appInstaller.Reset();
        _appUninstaller.Reset();
        _deviceFinder.Reset();

        // Add a SystemLog with test output (this is where MacCatalyst output goes)
        var sysLogContent = "[PASS] MyMacTest.TestA\n[PASS] MyMacTest.TestB\n=== TEST EXECUTION SUMMARY ===\n";
        AddLogWithContent(LogType.SystemLog, "MacCatalyst.system.log", sysLogContent);

        // Also add an ApplicationLog (should NOT be copied for MacCatalyst — it won't exist in practice)
        AddLogWithContent(LogType.ApplicationLog, "app.log", "App log content that should not appear");

        var testTarget = new TestTargetOs(TestTarget.MacCatalyst, null);
        _appTester
            .Setup(x => x.TestMacCatalystApp(
                It.IsAny<AppBundleInformation>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<(string, string?)>>(),
                It.IsAny<XmlResultJargon>(), It.IsAny<string[]?>(), It.IsAny<string[]?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestExecutingResult.Succeeded, SuccessResultLine));

        // Act
        var result = await _testOrchestrator.OrchestrateTest(
            AppPath, testTarget, null,
            TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(3),
            CommunicationChannel.UsbTunnel, XmlResultJargon.xUnit,
            Array.Empty<string>(), Array.Empty<string>(),
            includeWirelessDevices: false, resetSimulator: true, enableLldb: false,
            signalAppEnd: true,
            Array.Empty<(string, string?)>(), Array.Empty<string>(),
            new CancellationToken());

        // Verify
        Assert.Equal(ExitCode.SUCCESS, result);
        var mainLogText = string.Join("\n", _mainLogLines);

        // SystemLog content should be present (MacCatalyst uses SystemLog)
        Assert.Contains("[PASS] MyMacTest.TestA", mainLogText);
        Assert.Contains("TEST EXECUTION SUMMARY", mainLogText);
        Assert.Contains("==================== SystemLog ====================", mainLogText);

        // ApplicationLog content should NOT be present
        Assert.DoesNotContain("App log content that should not appear", mainLogText);
    }

    [Fact]
    public async Task DeviceTest_CopiesApplicationLogToMainLog()
    {
        // Setup: add an ApplicationLog with test output
        var appLogContent = "[PASS] DeviceTest.Test1\n[FAIL] DeviceTest.Test2\n";
        AddLogWithContent(LogType.ApplicationLog, "net.dot.Tests.log", appLogContent);

        var testTarget = new TestTargetOs(TestTarget.Device_iOS, "14.2");
        _appTester
            .Setup(x => x.TestApp(
                It.IsAny<AppBundleInformation>(), It.IsAny<TestTargetOs>(), It.IsAny<IDevice>(), It.IsAny<IDevice>(),
                It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<(string, string?)>>(), It.IsAny<XmlResultJargon>(),
                It.IsAny<string[]?>(), It.IsAny<string[]?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestExecutingResult.Succeeded, SuccessResultLine));

        // Act
        var result = await _testOrchestrator.OrchestrateTest(
            AppPath, testTarget, null,
            TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(3),
            CommunicationChannel.UsbTunnel, XmlResultJargon.xUnit,
            Array.Empty<string>(), Array.Empty<string>(),
            includeWirelessDevices: false, resetSimulator: false, enableLldb: false,
            signalAppEnd: false,
            Array.Empty<(string, string?)>(), Array.Empty<string>(),
            new CancellationToken());

        // Verify
        Assert.Equal(ExitCode.SUCCESS, result);
        var mainLogText = string.Join("\n", _mainLogLines);

        Assert.Contains("[PASS] DeviceTest.Test1", mainLogText);
        Assert.Contains("[FAIL] DeviceTest.Test2", mainLogText);
        Assert.Contains("==================== ApplicationLog ====================", mainLogText);
    }

    private void AddLogWithContent(LogType logType, string fileName, string content)
    {
        // Write content to a temp file so GetReader() works
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "_" + fileName);
        File.WriteAllText(tempPath, content);

        var mockLog = new Mock<IFileBackedLog>();
        mockLog.Setup(x => x.Description).Returns(logType.ToString());
        mockLog.Setup(x => x.FullPath).Returns(tempPath);
        mockLog.Setup(x => x.GetReader()).Returns(() => new StreamReader(tempPath));

        _logs.Add(mockLog.Object);
    }
}
