// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Logging;
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
                RunMode.iOS, true, "Simulator", "udid", "bundle", _tempFile, CancellationToken.None));

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
                RunMode.iOS, true, "Simulator notanumber", "udid", "bundle", _tempFile, CancellationToken.None));

        Assert.Equal("Simulator OS version is not in the expected format.", exception.Message);
    }

    [Fact]
    public async Task SimulatorOsVersionLessThan18ReturnsFalse()
    {
        Mock<IMlaunchProcessManager> pm = new Mock<IMlaunchProcessManager>();
        Mock<IFileBackedLog> log = new Mock<IFileBackedLog>();
        ResultFileHandler handler = CreateHandler(pm, log);

        bool result = await handler.CopyResultsAsync(
            RunMode.iOS, true, "Simulator 17.4", "udid", "bundle", _tempFile, CancellationToken.None);

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
            RunMode.iOS, true, "Simulator 18.0", "udid", "bundle", _tempFile, CancellationToken.None);

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
            RunMode.iOS, true, "Simulator 18.0", "udid", "bundle", _tempFile, CancellationToken.None);

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
                RunMode.iOS, false, "notanumber", "udid", "bundle", _tempFile, CancellationToken.None));

        Assert.Equal("Device OS version is not in the expected format.", exception.Message);
    }

    [Fact]
    public async Task DeviceOsVersionLessThan18ReturnsTrue()
    {
        Mock<IMlaunchProcessManager> pm = new Mock<IMlaunchProcessManager>();
        Mock<IFileBackedLog> log = new Mock<IFileBackedLog>();
        ResultFileHandler handler = CreateHandler(pm, log);

        bool result = await handler.CopyResultsAsync(
            RunMode.iOS, false, "17.4", "udid", "bundle", _tempFile, CancellationToken.None);

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
            RunMode.iOS, false, "18.0", "udid", "bundle", _tempFile, CancellationToken.None);

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
            RunMode.iOS, false, "18.0", "udid", "bundle", _tempFile, CancellationToken.None);

        Assert.False(result);
        log.Verify(l => l.WriteLine($"Failed to copy results file from device. Expected at: {_tempFile}"), Times.Once);
    }
}
