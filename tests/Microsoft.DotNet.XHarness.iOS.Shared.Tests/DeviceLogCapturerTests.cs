// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests;

public class DeviceLogCapturerTests
{
    [Fact]
    public void HasOnlyKnownWarningDiagnosticsReturnsTrueForKnownWarning()
    {
        string errors = $"{DeviceLogCapturer.WallClockAdjustmentWarning}\n";

        Assert.True(DeviceLogCapturer.HasOnlyKnownWarningDiagnostics(errors));
    }

    [Fact]
    public void HasOnlyKnownWarningDiagnosticsReturnsFalseWhenOtherErrorsArePresent()
    {
        string errors = $"{DeviceLogCapturer.WallClockAdjustmentWarning}\nPermission denied\n";

        Assert.False(DeviceLogCapturer.HasOnlyKnownWarningDiagnostics(errors));
    }

    [Fact]
    public void HasOnlyKnownWarningDiagnosticsReturnsFalseWhenWarningLineContainsOtherText()
    {
        string errors = $"warning: {DeviceLogCapturer.WallClockAdjustmentWarning}\n";

        Assert.False(DeviceLogCapturer.HasOnlyKnownWarningDiagnostics(errors));
    }

    [Fact]
    public void IsKnownWarningDiagnosticReturnsTrueForWallClockWarning()
    {
        Assert.True(DeviceLogCapturer.IsKnownWarningDiagnostic(DeviceLogCapturer.WallClockAdjustmentWarning));
    }

    [Fact]
    public void LogReadDiagnosticsReportsWallClockAdjustmentAsWarning()
    {
        Mock<ILog> log = new Mock<ILog>();
        string errors = $"{DeviceLogCapturer.WallClockAdjustmentWarning}\n";

        DeviceLogCapturer.LogReadDiagnostics(log.Object, errors);

        log.Verify(l => l.WriteLine($"Warnings while reading device logs: {DeviceLogCapturer.WallClockAdjustmentWarning}"), Times.Once);
        log.Verify(l => l.WriteLine(It.Is<string>(s => s.StartsWith("Errors while reading device logs:", System.StringComparison.Ordinal))), Times.Never);
    }

    [Fact]
    public void LogReadDiagnosticsPreservesErrorsWhenStderrContainsOtherMessages()
    {
        Mock<ILog> log = new Mock<ILog>();
        string errors = $"{DeviceLogCapturer.WallClockAdjustmentWarning}\nPermission denied\n";

        DeviceLogCapturer.LogReadDiagnostics(log.Object, errors);

        log.Verify(l => l.WriteLine($"Errors while reading device logs: {DeviceLogCapturer.WallClockAdjustmentWarning}\nPermission denied"), Times.Once);
        log.Verify(l => l.WriteLine(It.Is<string>(s => s.StartsWith("Warnings while reading device logs:", System.StringComparison.Ordinal))), Times.Never);
    }
}
