// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution.Mlaunch;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests
{
    public class CrashReportSnapshotTests : IDisposable
    {
        private readonly string tempXcodeRoot;
        private readonly string symbolicatePath;
        private readonly Mock<IProcessManager> processManager;
        private readonly Mock<ILog> _log;
        private readonly Mock<ILogs> _logs;

        public CrashReportSnapshotTests()
        {
            processManager = new Mock<IProcessManager>();
            _log = new Mock<ILog>();
            _logs = new Mock<ILogs>();

            tempXcodeRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            symbolicatePath = Path.Combine(tempXcodeRoot, "Contents", "SharedFrameworks", "DTDeviceKitBase.framework", "Versions", "A", "Resources");

            processManager.SetupGet(x => x.XcodeRoot).Returns(tempXcodeRoot);
            processManager.SetupGet(x => x.MlaunchPath).Returns("/var/bin/mlaunch");

            // Create fake place for device logs
            Directory.CreateDirectory(tempXcodeRoot);

            // Create fake symbolicate binary
            Directory.CreateDirectory(symbolicatePath);
            File.WriteAllText(Path.Combine(symbolicatePath, "symbolicatecrash"), "");
        }

        public void Dispose()
        {
            Directory.Delete(tempXcodeRoot, true);
        }

        [Fact]
        public async Task DeviceCaptureTest()
        {
            var tempFilePath = Path.GetTempFileName();

            const string deviceName = "Sample-iPhone";
            const string crashLogPath = "/path/to/crash.log";
            const string symbolicateLogPath = "/path/to/" + deviceName + ".symbolicated.log";

            var crashReport = Mock.Of<ILog>(x => x.FullPath == crashLogPath);
            var symbolicateReport = Mock.Of<ILog>(x => x.FullPath == symbolicateLogPath);

            // Crash report is added
            _logs.Setup(x => x.Create(deviceName, "Crash report: " + deviceName, It.IsAny<bool>()))
                .Returns(crashReport);

            // Symbolicate report is added
            _logs.Setup(x => x.Create("crash.symbolicated.log", "Symbolicated crash report: crash.log", It.IsAny<bool>()))
                .Returns(symbolicateReport);

            processManager.SetReturnsDefault(Task.FromResult(new ProcessExecutionResult() { ExitCode = 0 }));

            // Act
            var snapshotReport = new CrashSnapshotReporter(processManager.Object,
                _log.Object,
                _logs.Object,
                true,
                deviceName,
                () => tempFilePath);

            File.WriteAllLines(tempFilePath, new[] { "crash 1", "crash 2" });

            await snapshotReport.StartCaptureAsync();

            File.WriteAllLines(tempFilePath, new[] { "Sample-iPhone" });

            await snapshotReport.EndCaptureAsync(TimeSpan.FromSeconds(10));

            // Verify
            _logs.VerifyAll();

            // List of crash reports is retrieved
            processManager.Verify(
                x => x.ExecuteCommandAsync(
                    It.Is<MlaunchArguments>(args => args.AsCommandLine() ==
                       StringUtils.FormatArguments(
                           $"--list-crash-reports={tempFilePath}") + " " +
                           $"--devname {StringUtils.FormatArguments(deviceName)}"),
                    _log.Object,
                    TimeSpan.FromMinutes(1),
                    null,
                    null),
                Times.Exactly(2));

            // Device crash log is downloaded
            processManager.Verify(
                x => x.ExecuteCommandAsync(
                    It.Is<MlaunchArguments>(args => args.AsCommandLine() ==
                        StringUtils.FormatArguments(
                            $"--download-crash-report={deviceName}") + " " +
                            StringUtils.FormatArguments($"--download-crash-report-to={crashLogPath}") + " " +
                            $"--devname {StringUtils.FormatArguments(deviceName)}"),
                    _log.Object,
                    TimeSpan.FromMinutes(1),
                    null,
                    null),
                Times.Once);

            // Symbolicate is ran
            processManager.Verify(
                x => x.ExecuteCommandAsync(
                    Path.Combine(symbolicatePath, "symbolicatecrash"),
                    It.Is<IList<string>>(args => args.First() == crashLogPath),
                    symbolicateReport,
                    TimeSpan.FromMinutes(1),
                    It.IsAny<Dictionary<string, string>>(),
                    null),
                Times.Once);
        }
    }
}
