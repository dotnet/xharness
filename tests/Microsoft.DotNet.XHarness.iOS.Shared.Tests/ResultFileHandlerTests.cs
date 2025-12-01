// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests;

public class ResultFileHandlerTests : IDisposable
{
    private readonly string _tempFile;

    public ResultFileHandlerTests()
    {
        _tempFile = Path.GetTempFileName();
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
    }

    private static ResultFileHandler CreateHandler(
        Mock<IMlaunchProcessManager> processManagerMock,
        Mock<IFileBackedLog> logMock)
    {
        return new ResultFileHandler(processManagerMock.Object, logMock.Object);
    }

    [Fact]
    public async Task SimulatorBadOsVersionFormatThrowsException()
    {
        Mock<IMlaunchProcessManager> pm = new Mock<IMlaunchProcessManager>();
        Mock<IFileBackedLog> log = new Mock<IFileBackedLog>();
        ResultFileHandler handler = CreateHandler(pm, log);

        var exception = await Assert.ThrowsAsync<FormatException>(async () =>
            await handler.CopyResultsAsync(
                RunMode.iOS, true, "Simulator", "udid", "bundle", _tempFile));

        Assert.Equal("Simulator OS version is not in the expected format.", exception.Message);
    }

    [Fact]
    public async Task SimulatorBadOsVersionNumberThrowsException()
    {
        Mock<IMlaunchProcessManager> pm = new Mock<IMlaunchProcessManager>();
        Mock<IFileBackedLog> log = new Mock<IFileBackedLog>();
        ResultFileHandler handler = CreateHandler(pm, log);

        var exception = await Assert.ThrowsAsync<FormatException>(async () =>
            await handler.CopyResultsAsync(
                RunMode.iOS, true, "Simulator notanumber", "udid", "bundle", _tempFile));

        Assert.Equal("Simulator OS version is not in the expected format.", exception.Message);
    }

    [Fact]
    public async Task SimulatorOsVersionLessThan18ReturnsFalse()
    {
        Mock<IMlaunchProcessManager> pm = new Mock<IMlaunchProcessManager>();
        Mock<IFileBackedLog> log = new Mock<IFileBackedLog>();
        ResultFileHandler handler = CreateHandler(pm, log);

        bool result = await handler.CopyResultsAsync(
            RunMode.iOS, true, "Simulator 17.4", "udid", "bundle", _tempFile);

        Assert.True(result);
    }

    [Fact]
    public async Task SimulatorOsVersion18FileExistsReturnsTrue()
    {
        Mock<IMlaunchProcessManager> pm = new Mock<IMlaunchProcessManager>();
        Mock<IFileBackedLog> log = new Mock<IFileBackedLog>();
        ResultFileHandler handler = CreateHandler(pm, log);

        File.WriteAllText(_tempFile, "dummy");

        bool result = await handler.CopyResultsAsync(
            RunMode.iOS, true, "Simulator 18.0", "udid", "bundle", _tempFile);

        Assert.True(result);
    }

    [Fact]
    public async Task SimulatorOsVersion18FileMissingReturnsFalse()
    {
        Mock<IMlaunchProcessManager> pm = new Mock<IMlaunchProcessManager>();
        Mock<IFileBackedLog> log = new Mock<IFileBackedLog>();
        ResultFileHandler handler = CreateHandler(pm, log);

        if (File.Exists(_tempFile))
            File.Delete(_tempFile);

        bool result = await handler.CopyResultsAsync(
            RunMode.iOS, true, "Simulator 18.0", "udid", "bundle", _tempFile);

        Assert.False(result);
        log.Verify(l => l.WriteLine($"Failed to copy results file from simulator. Expected at: {_tempFile}"), Times.Once);
    }

    [Fact]
    public async Task DeviceBadOsVersionFormatThrowsException()
    {
        Mock<IMlaunchProcessManager> pm = new Mock<IMlaunchProcessManager>();
        Mock<IFileBackedLog> log = new Mock<IFileBackedLog>();
        ResultFileHandler handler = CreateHandler(pm, log);

        var exception = await Assert.ThrowsAsync<FormatException>(async () =>
            await handler.CopyResultsAsync(
                RunMode.iOS, false, "notanumber", "udid", "bundle", _tempFile));

        Assert.Equal("Device OS version is not in the expected format.", exception.Message);
    }

    [Fact]
    public async Task DeviceOsVersionLessThan18ReturnsTrue()
    {
        Mock<IMlaunchProcessManager> pm = new Mock<IMlaunchProcessManager>();
        Mock<IFileBackedLog> log = new Mock<IFileBackedLog>();
        ResultFileHandler handler = CreateHandler(pm, log);

        bool result = await handler.CopyResultsAsync(
            RunMode.iOS, false, "17.4", "udid", "bundle", _tempFile);

        Assert.True(result);
    }

    [Fact]
    public async Task DeviceOsVersion18FileExistsReturnsTrue()
    {
        Mock<IMlaunchProcessManager> pm = new Mock<IMlaunchProcessManager>();
        Mock<IFileBackedLog> log = new Mock<IFileBackedLog>();
        ResultFileHandler handler = CreateHandler(pm, log);

        File.WriteAllText(_tempFile, "dummy");

        bool result = await handler.CopyResultsAsync(
            RunMode.iOS, false, "18.0", "udid", "bundle", _tempFile);

        Assert.True(result);
    }

    [Fact]
    public async Task DeviceOsVersion18FileMissingReturnsFalse()
    {
        Mock<IMlaunchProcessManager> pm = new Mock<IMlaunchProcessManager>();
        Mock<IFileBackedLog> log = new Mock<IFileBackedLog>();
        ResultFileHandler handler = CreateHandler(pm, log);

        if (File.Exists(_tempFile))
            File.Delete(_tempFile);

        bool result = await handler.CopyResultsAsync(
            RunMode.iOS, false, "18.0", "udid", "bundle", _tempFile);

        Assert.False(result);
        log.Verify(l => l.WriteLine($"Failed to copy results file from device. Expected at: {_tempFile}"), Times.Once);
    }

    [Fact]
    public async Task CopyCrashReportUsesHelixUploadRootWhenAvailable()
    {
        string originalUploadRoot = Environment.GetEnvironmentVariable("HELIX_WORKITEM_UPLOAD_ROOT");
        string uploadRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(uploadRoot);

        try
        {
            Environment.SetEnvironmentVariable("HELIX_WORKITEM_UPLOAD_ROOT", uploadRoot);

            Mock<IMlaunchProcessManager> pm = new Mock<IMlaunchProcessManager>();
            Mock<IFileBackedLog> log = new Mock<IFileBackedLog>();
            ResultFileHandler handler = CreateHandler(pm, log);

            string crashReportName = "MyApp-2025-11-25-223847.ips";
            string expectedDownloadPath = Path.Combine(uploadRoot, crashReportName);
            string crashContent = "Dummy crash content";
            string actualDownloadPath = null;

            int callCount = 0;

            pm.Setup(m => m.ExecuteCommandAsync(
                    It.IsAny<MlaunchArguments>(),
                    It.IsAny<ILog>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken?>()))
                .Returns((MlaunchArguments args, ILog _, TimeSpan _, Dictionary<string, string> _, int _, CancellationToken? _) =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        string listFilePath = GetArgumentValue(args, "list-crash-reports");
                        File.WriteAllLines(listFilePath, new[] { crashReportName });
                    }
                    else if (callCount == 2)
                    {
                        actualDownloadPath = GetArgumentValue(args, "download-crash-report-to");
                        File.WriteAllText(actualDownloadPath, crashContent);
                    }

                    return Task.FromResult(new ProcessExecutionResult { ExitCode = 0 });
                });

            var appInfo = new AppBundleInformation("MyApp", "com.example.myapp", "/tmp", "/tmp", supports32b: false);

            await handler.CopyCrashReportAsync("device-udid", null, appInfo, log.Object, isSimulator: false);

            Assert.Equal(expectedDownloadPath, actualDownloadPath);
            Assert.True(File.Exists(expectedDownloadPath));

            log.Verify(l => l.WriteLine("Attempting to retrieve crash report from device..."), Times.Once);
            log.Verify(l => l.WriteLine($"Found crash report: {crashReportName}"), Times.Once);
            log.Verify(l => l.WriteLine("==================== Crash report ===================="), Times.Once);
            log.Verify(l => l.WriteLine($"Crash report file: {expectedDownloadPath}"), Times.Once);
            log.Verify(l => l.WriteLine(crashContent), Times.Once);
            log.Verify(l => l.WriteLine("==================== End of Crash report ===================="), Times.Once);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HELIX_WORKITEM_UPLOAD_ROOT", originalUploadRoot);

            if (Directory.Exists(uploadRoot))
            {
                Directory.Delete(uploadRoot, true);
            }
        }
    }

    private static string GetArgumentValue(MlaunchArguments args, string argumentName)
    {
        string prefix = $"--{argumentName}=";
        string argument = args.Select(a => a.AsCommandLineArgument())
            .First(a => a.StartsWith(prefix, StringComparison.Ordinal));

        return argument.Substring(prefix.Length).Trim('"');
    }
}
