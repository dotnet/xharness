// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.Common.Utilities;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests;

public class CrashReportSnapshotTests : IDisposable
{
    private readonly string _tempXcodeRoot;
    private readonly string _symbolicatePath;
    private readonly Mock<IMlaunchProcessManager> _processManager;
    private readonly Mock<ILog> _log;
    private readonly Mock<ILogs> _logs;

    public CrashReportSnapshotTests()
    {
        _processManager = new Mock<IMlaunchProcessManager>();
        _log = new Mock<ILog>();
        _logs = new Mock<ILogs>();

        _tempXcodeRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _symbolicatePath = Path.Combine(_tempXcodeRoot, "Contents", "SharedFrameworks", "DTDeviceKitBase.framework", "Versions", "A", "Resources");

        _processManager.SetupGet(x => x.XcodeRoot).Returns(_tempXcodeRoot);
        _processManager.SetupGet(x => x.MlaunchPath).Returns("/var/bin/mlaunch");

        // Create fake place for device logs
        Directory.CreateDirectory(_tempXcodeRoot);

        // Create fake symbolicate binary
        Directory.CreateDirectory(_symbolicatePath);
        File.WriteAllText(Path.Combine(_symbolicatePath, "symbolicatecrash"), "");
    }

    public void Dispose()
    {
        Directory.Delete(_tempXcodeRoot, true);
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData("{\"captureTime\":\"2026-08-24 14:00:00.00 +0200\",\"pid\":42,\"procName\":\"Sample-iPhone\",\"bundleInfo\":{\"CFBundleIdentifier\":\"com.example.sample\"}}", 42)]
    [InlineData("{", null)]
    public async Task DeviceCaptureTest(string payload, int? expectedProcessId)
    {
        var tempFilePath = Path.GetTempFileName();

        const string deviceName = "Sample-iPhone";
        string crashLogPath = Path.Combine(_tempXcodeRoot, "crash.log");
        string symbolicateLogPath = Path.Combine(_tempXcodeRoot, "crash.symbolicated.log");

        var crashReport = Mock.Of<IFileBackedLog>(x => x.FullPath == crashLogPath);
        var symbolicateReport = Mock.Of<IFileBackedLog>(x => x.FullPath == symbolicateLogPath);
        var inventoryLog = Mock.Of<IFileBackedLog>();

        _logs.Setup(x => x.Create("crash-reports-before.txt", "Crash reports before launch", false))
            .Returns(inventoryLog);
        _logs.Setup(x => x.Create("crash-reports-after.txt", "Crash reports after launch", false))
            .Returns(inventoryLog);

        // Crash report is added
        _logs.Setup(x => x.Create(deviceName, "Crash report: " + deviceName, It.IsAny<bool>()))
            .Returns(crashReport);

        // Symbolicate report is added
        _logs.Setup(x => x.Create("crash.symbolicated.log", "Symbolicated crash report: crash.log", It.IsAny<bool>()))
            .Returns(symbolicateReport);

        _processManager.SetReturnsDefault(Task.FromResult(new ProcessExecutionResult() { ExitCode = 0 }));

        // Act
        var snapshotReport = new CrashSnapshotReporter(_processManager.Object,
            _log.Object,
            _logs.Object,
            true,
            deviceName,
            new AppBundleInformation(deviceName, "com.example.sample", "/tmp", "/tmp", supports32b: false),
            () => tempFilePath);

        File.WriteAllLines(tempFilePath, new[] { "crash 1", "crash 2" });

        await snapshotReport.StartCaptureAsync();

        File.WriteAllLines(tempFilePath, new[] { "Sample-iPhone" });
        File.WriteAllText(
            crashLogPath,
            "{\"app_name\":\"Sample-iPhone\",\"timestamp\":\"2026-08-24 14:00:00.00 +0200\",\"bundleID\":\"com.example.sample\",\"bug_type\":\"309\"}" +
            Environment.NewLine +
            payload);

        await snapshotReport.EndCaptureAsync(TimeSpan.FromSeconds(10));

        // Verify
        _logs.VerifyAll();
        Assert.Equal(2, snapshotReport.CaptureDiagnostics.ReportsBeforeLaunch);
        Assert.Equal(1, snapshotReport.CaptureDiagnostics.ReportsCreatedDuringRun);
        Assert.Equal("Sample-iPhone", snapshotReport.CaptureDiagnostics.MatchedReport.Name);
        Assert.Equal("com.example.sample", snapshotReport.CaptureDiagnostics.MatchedReport.BundleId);
        Assert.Equal(expectedProcessId, snapshotReport.CaptureDiagnostics.MatchedReport.ProcessId);

        // List of crash reports is retrieved
        _processManager.Verify(
            x => x.ExecuteCommandAsync(
                It.Is<MlaunchArguments>(args => args.AsCommandLine() ==
                   StringUtils.FormatArguments(
                       $"--list-crash-reports={tempFilePath}") + " " +
                       $"--devname {StringUtils.FormatArguments(deviceName)}"),
                _log.Object,
                TimeSpan.FromMinutes(1),
                null,
                It.IsAny<int>(),
                null),
            Times.Exactly(2));

        // Device crash log is downloaded
        _processManager.Verify(
            x => x.ExecuteCommandAsync(
                It.Is<MlaunchArguments>(args => args.AsCommandLine() ==
                    StringUtils.FormatArguments(
                        $"--download-crash-report={deviceName}") + " " +
                        StringUtils.FormatArguments($"--download-crash-report-to={crashLogPath}") + " " +
                        $"--devname {StringUtils.FormatArguments(deviceName)}"),
                _log.Object,
                TimeSpan.FromMinutes(1),
                null,
                It.IsAny<int>(),
                null),
            Times.Once);

        // Symbolicate is ran
        _processManager.Verify(
            x => x.ExecuteCommandAsync(
                Path.Combine(_symbolicatePath, "symbolicatecrash"),
                It.Is<IList<string>>(args => args.First() == crashLogPath),
                symbolicateReport,
                TimeSpan.FromMinutes(1),
                It.IsAny<Dictionary<string, string>>(),
                null),
            Times.Once);
    }
}
