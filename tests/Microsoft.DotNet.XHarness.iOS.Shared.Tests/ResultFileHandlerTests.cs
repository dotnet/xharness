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
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests;

public class ResultFileHandlerTests : IDisposable
{
    private readonly string _tempFile = Path.GetTempFileName();
    private readonly Mock<IMlaunchProcessManager> _processManager = new();
    private readonly Mock<IFileBackedLog> _mainLog = new();

    public ResultFileHandlerTests()
    {
        _processManager
            .Setup(p => p.ExecuteCommandAsync(
                It.IsAny<string>(),
                It.IsAny<IList<string>>(),
                It.IsAny<ILog>(),
                It.IsAny<ILog>(),
                It.IsAny<ILog>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken?>()))
            .ReturnsAsync(new ProcessExecutionResult { ExitCode = 0 });
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
    }

    [Theory]
    [InlineData("Simulator 17.4", false)]
    [InlineData("Simulator 18.0", true)]
    [InlineData("Simulator 26.0", true)]
    [InlineData("invalid", false)]
    public void DetectsSimulatorFileResultSupport(string osVersion, bool expected)
    {
        var handler = new ResultFileHandler(_processManager.Object, _mainLog.Object);

        Assert.Equal(expected, handler.IsSimulatorVersionSupported(osVersion));
    }

    [Theory]
    [InlineData("Simulator")]
    [InlineData("Simulator invalid")]
    public async Task InvalidOsVersionReturnsFalse(string osVersion)
    {
        var handler = new ResultFileHandler(_processManager.Object, _mainLog.Object);

        bool result = await handler.CopySimulatorResultsAsync(
            RunMode.iOS,
            osVersion,
            "udid",
            "bundle",
            _tempFile,
            CancellationToken.None);

        Assert.False(result);
        _mainLog.Verify(
            l => l.WriteLine("Simulator OS version is not in the expected format, skipping result copying."),
            Times.Once);
        _processManager.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SimulatorBeforeIos18DoesNotCopyResults()
    {
        var handler = new ResultFileHandler(_processManager.Object, _mainLog.Object);

        bool result = await handler.CopySimulatorResultsAsync(
            RunMode.iOS,
            "Simulator 17.4",
            "udid",
            "bundle",
            _tempFile,
            CancellationToken.None);

        Assert.True(result);
        _processManager.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(RunMode.Sim64, "/Documents/test-results.xml")]
    [InlineData(RunMode.TvOS, "/Library/Caches/Documents/test-results.xml")]
    public async Task SimulatorIos18OrNewerClearsPreviousResults(RunMode runMode, string sourcePath)
    {
        var handler = new ResultFileHandler(_processManager.Object, _mainLog.Object);
        const string udid = "simulator-udid";
        const string bundleIdentifier = "net.dot.test";

        bool result = await handler.ClearSimulatorResultsAsync(
            runMode,
            "Simulator 26.0",
            udid,
            bundleIdentifier,
            CancellationToken.None);

        Assert.True(result);
        string expectedCommand = $"rm -f \"$(xcrun simctl get_app_container {udid} {bundleIdentifier} data){sourcePath}\"";
        _processManager.Verify(
            p => p.ExecuteCommandAsync(
                "/bin/bash",
                It.Is<IList<string>>(args => args.SequenceEqual(new[] { "-c", expectedCommand })),
                _mainLog.Object,
                _mainLog.Object,
                _mainLog.Object,
                TimeSpan.FromMinutes(1),
                null,
                CancellationToken.None),
            Times.Once);
    }

    [Theory]
    [InlineData(RunMode.Sim64, "/Documents/test-results.xml")]
    [InlineData(RunMode.TvOS, "/Library/Caches/Documents/test-results.xml")]
    public async Task SimulatorIos18OrNewerCopiesResults(RunMode runMode, string sourcePath)
    {
        var handler = new ResultFileHandler(_processManager.Object, _mainLog.Object);
        const string udid = "simulator-udid";
        const string bundleIdentifier = "net.dot.test";

        bool result = await handler.CopySimulatorResultsAsync(
            runMode,
            "Simulator 26.0",
            udid,
            bundleIdentifier,
            _tempFile,
            CancellationToken.None);

        Assert.True(result);
        string expectedCommand = $"cp \"$(xcrun simctl get_app_container {udid} {bundleIdentifier} data){sourcePath}\" \"{_tempFile}\"";
        _processManager.Verify(
            p => p.ExecuteCommandAsync(
                "/bin/bash",
                It.Is<IList<string>>(args => args.SequenceEqual(new[] { "-c", expectedCommand })),
                _mainLog.Object,
                _mainLog.Object,
                _mainLog.Object,
                TimeSpan.FromMinutes(1),
                null,
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task MissingCopiedFileReturnsFalse()
    {
        File.Delete(_tempFile);
        var handler = new ResultFileHandler(_processManager.Object, _mainLog.Object);

        bool result = await handler.CopySimulatorResultsAsync(
            RunMode.iOS,
            "Simulator 18.0",
            "udid",
            "bundle",
            _tempFile,
            CancellationToken.None);

        Assert.False(result);
        _mainLog.Verify(
            l => l.WriteLine($"Failed to copy results file from simulator. Expected at: {_tempFile}"),
            Times.Once);
    }
}
