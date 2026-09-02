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
        Mock<IFileBackedLog> logMock,
        int[] retryDelaysMs = null)
    {
        // Default to no retry delays in tests to keep them fast
        return new ResultFileHandler(processManagerMock.Object, logMock.Object, retryDelaysMs ?? Array.Empty<int>());
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
        log.Verify(l => l.WriteLine($"Failed to copy results file from simulator (attempt 1). Expected at: {_tempFile}"), Times.Once);
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
        log.Verify(l => l.WriteLine($"Failed to copy results file from device (attempt 1). Expected at: {_tempFile}"), Times.Once);
    }

    [Fact]
    public async Task CopyResultsAsync_WhenFirstAttemptFailsAndSecondSucceeds_ReturnsTrue()
    {
        Mock<IMlaunchProcessManager> pm = new Mock<IMlaunchProcessManager>();
        Mock<IFileBackedLog> log = new Mock<IFileBackedLog>();
        // Use a short delay for the test
        ResultFileHandler handler = CreateHandler(pm, log, new[] { 1 });

        if (File.Exists(_tempFile))
            File.Delete(_tempFile);

        int callCount = 0;
        pm.Setup(m => m.ExecuteCommandAsync(
                It.IsAny<string>(),
                It.IsAny<IList<string>>(),
                It.IsAny<ILog>(),
                It.IsAny<ILog>(),
                It.IsAny<ILog>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken?>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 2)
                {
                    // Simulate success on second attempt by writing the file
                    File.WriteAllText(_tempFile, "results");
                }
                return Task.FromResult(new ProcessExecutionResult { ExitCode = 0 });
            });

        bool result = await handler.CopyResultsAsync(
            RunMode.iOS, false, "18.0", "udid", "bundle", _tempFile);

        Assert.True(result);
        Assert.Equal(2, callCount);
        Assert.Equal(2, handler.LastCopyAttempts);
        log.Verify(l => l.WriteLine(It.Is<string>(s => s.Contains("Retrying results file copy (attempt 2)"))), Times.Once);
    }

    [Fact]
    public async Task CopyResultsAsync_WhenAllRetriesFail_ReturnsFalse()
    {
        Mock<IMlaunchProcessManager> pm = new Mock<IMlaunchProcessManager>();
        Mock<IFileBackedLog> log = new Mock<IFileBackedLog>();
        // Two retries with minimal delay
        ResultFileHandler handler = CreateHandler(pm, log, new[] { 1, 1 });

        if (File.Exists(_tempFile))
            File.Delete(_tempFile);

        int callCount = 0;
        pm.Setup(m => m.ExecuteCommandAsync(
                It.IsAny<string>(),
                It.IsAny<IList<string>>(),
                It.IsAny<ILog>(),
                It.IsAny<ILog>(),
                It.IsAny<ILog>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken?>()))
            .Returns(() =>
            {
                callCount++;
                return Task.FromResult(new ProcessExecutionResult { ExitCode = 1 });
            });

        bool result = await handler.CopyResultsAsync(
            RunMode.iOS, false, "18.0", "udid", "bundle", _tempFile);

        Assert.False(result);
        // 1 initial attempt + 2 retries = 3 total
        Assert.Equal(3, callCount);
        Assert.Equal(3, handler.LastCopyAttempts);
        log.Verify(l => l.WriteLine(It.Is<string>(s => s.Contains("Retrying results file copy (attempt 2)"))), Times.Once);
        log.Verify(l => l.WriteLine(It.Is<string>(s => s.Contains("Retrying results file copy (attempt 3)"))), Times.Once);
    }

    [Fact]
    public async Task CopyCoverageResultsAsync_WhenFirstAttemptFailsAndSecondSucceeds_ReturnsTrue()
    {
        Mock<IMlaunchProcessManager> pm = new Mock<IMlaunchProcessManager>();
        Mock<IFileBackedLog> log = new Mock<IFileBackedLog>();
        ResultFileHandler handler = CreateHandler(pm, log, new[] { 1 });

        if (File.Exists(_tempFile))
            File.Delete(_tempFile);

        int callCount = 0;
        pm.Setup(m => m.ExecuteCommandAsync(
                It.IsAny<string>(),
                It.IsAny<IList<string>>(),
                It.IsAny<ILog>(),
                It.IsAny<ILog>(),
                It.IsAny<ILog>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken?>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 2)
                {
                    File.WriteAllText(_tempFile, "coverage");
                }
                return Task.FromResult(new ProcessExecutionResult { ExitCode = 0 });
            });

        bool result = await handler.CopyCoverageResultsAsync(
            RunMode.iOS, false, "18.0", "udid", "bundle", "coverage.cobertura.xml", _tempFile);

        Assert.True(result);
        Assert.Equal(2, callCount);
        log.Verify(l => l.WriteLine(It.Is<string>(s => s.Contains("Retrying coverage results file copy (attempt 2)"))), Times.Once);
    }

    [Fact]
    public async Task CopyCoverageResultsAsync_MacCatalystUsesLocalContainerPath()
    {
        Mock<IMlaunchProcessManager> pm = new Mock<IMlaunchProcessManager>();
        Mock<IFileBackedLog> log = new Mock<IFileBackedLog>();
        ResultFileHandler handler = CreateHandler(pm, log);

        string command = null;
        pm.Setup(m => m.ExecuteCommandAsync(
                It.IsAny<string>(),
                It.IsAny<IList<string>>(),
                It.IsAny<ILog>(),
                It.IsAny<ILog>(),
                It.IsAny<ILog>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken?>()))
            .Returns((string _, IList<string> args, ILog _, ILog _, ILog _, TimeSpan _, Dictionary<string, string> _, CancellationToken? _) =>
            {
                command = args[1];
                File.WriteAllText(_tempFile, "coverage");
                return Task.FromResult(new ProcessExecutionResult { ExitCode = 0 });
            });

        bool result = await handler.CopyCoverageResultsAsync(
            RunMode.MacOS, false, Environment.OSVersion.Version.ToString(), string.Empty, "com.example.maccatalyst", "coverage.cobertura.xml", _tempFile);

        Assert.True(result);
        Assert.Contains("Library", command);
        Assert.Contains("Containers", command);
        Assert.Contains("com.example.maccatalyst", command);
        Assert.Contains("Documents", command);
        Assert.Contains("coverage.cobertura.xml", command);
    }

}
