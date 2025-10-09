// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests.Hardware;

public class DefaultSimulatorSelectorTests
{
    private readonly Mock<IMlaunchProcessManager> _processManager;
    private readonly Mock<ITCCDatabase> _tccDatabase;
    private readonly DefaultSimulatorSelector _simulatorSelector;

    public DefaultSimulatorSelectorTests()
    {
        _processManager = new Mock<IMlaunchProcessManager>();
        _tccDatabase = new Mock<ITCCDatabase>();
        _simulatorSelector = new DefaultSimulatorSelector();
    }

    [Fact]
    public void SelectSimulatorTest()
    {
        var simulator1 = new SimulatorDevice(_processManager.Object, _tccDatabase.Object)
        {
            Name = "Simulator 1",
            UDID = "udid1",
            State = DeviceState.Shutdown,
        };

        var simulator2 = new SimulatorDevice(_processManager.Object, _tccDatabase.Object)
        {
            Name = "Simulator 2",
            UDID = "udid2",
            State = DeviceState.Booted,
        };

        var simulator3 = new SimulatorDevice(_processManager.Object, _tccDatabase.Object)
        {
            Name = "Simulator 3",
            UDID = "udid3",
            State = DeviceState.Booting,
        };

        var simulator = _simulatorSelector.SelectSimulator(new[] { simulator1, simulator2, simulator3 });

        // The Booted one
        Assert.Equal(simulator2, simulator);
    }

    [Theory]
    [InlineData(TestTarget.Simulator_iOS64, false, "com.apple.CoreSimulator.SimDeviceType.iPhone-16")]
    [InlineData(TestTarget.Simulator_iOS64, true, "com.apple.CoreSimulator.SimDeviceType.iPhone-6s")]
    [InlineData(TestTarget.Simulator_tvOS, false, "com.apple.CoreSimulator.SimDeviceType.Apple-TV-1080p")]
    [InlineData(TestTarget.Simulator_tvOS, true, "com.apple.CoreSimulator.SimDeviceType.Apple-TV-1080p")]
    [InlineData(TestTarget.Simulator_watchOS, false, "com.apple.CoreSimulator.SimDeviceType.Apple-Watch-Series-3-38mm")]
    [InlineData(TestTarget.Simulator_watchOS, true, "com.apple.CoreSimulator.SimDeviceType.Apple-Watch-38mm")]
    [InlineData(TestTarget.Simulator_xrOS, false, "com.apple.CoreSimulator.SimDeviceType.Apple-Vision-Pro")]
    [InlineData(TestTarget.Simulator_xrOS, true, "com.apple.CoreSimulator.SimDeviceType.Apple-Vision-Pro")]
    public void GetDeviceTypeTest(TestTarget target, bool minVersion, string expectedDeviceType)
    {
        var deviceType = _simulatorSelector.GetDeviceType(new TestTargetOs(target, null), minVersion);
        Assert.Equal(expectedDeviceType, deviceType);
    }

    [Theory]
    [InlineData(TestTarget.Simulator_watchOS, false, "com.apple.CoreSimulator.SimDeviceType.iPhone-16")]
    [InlineData(TestTarget.Simulator_watchOS, true, "com.apple.CoreSimulator.SimDeviceType.iPhone-6s")]
    public void GetCompanionDeviceTypeTest(TestTarget target, bool minVersion, string expectedDeviceType)
    {
        _simulatorSelector.GetCompanionRuntimeAndDeviceType(new TestTargetOs(target, null), minVersion, out var companionRuntime, out var companionDeviceType);
        Assert.NotNull(companionRuntime);
        Assert.Equal(expectedDeviceType, companionDeviceType);
    }

    [Theory]
    [InlineData(TestTarget.Simulator_iOS64)]
    [InlineData(TestTarget.Simulator_tvOS)]
    [InlineData(TestTarget.Simulator_xrOS)]
    public void GetCompanionDeviceTypeNonWatchTest(TestTarget target)
    {
        _simulatorSelector.GetCompanionRuntimeAndDeviceType(new TestTargetOs(target, null), false, out var companionRuntime, out var companionDeviceType);
        Assert.Null(companionRuntime);
        Assert.Null(companionDeviceType);
    }
}
