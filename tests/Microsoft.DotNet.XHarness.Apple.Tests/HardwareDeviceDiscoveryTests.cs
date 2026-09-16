// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.Apple.Tests;

public class HardwareDeviceDiscoveryTests
{
    // Names and simulator UDID from Helix job 81cdd057-5e56-4b85-8189-377f9da2dd53.
    // The XML is reconstructed test data, not a raw CI capture; the hardware UDID is synthetic.
    private const string SimulatorId = "8FAB5D79-D599-4E10-A3AD-25109BAA337F";
    private const string SimulatorName = "Apple TV 4K (3rd generation) (at 1080p)";
    private const string PhysicalId = "physical-apple-tv";
    private const string PhysicalName = "DNCENGTVOS-237";

    private readonly Mock<IMlaunchProcessManager> _processManager = new();
    private readonly MemoryLog _log = new();
    private readonly HardwareDeviceLoader _hardwareLoader;
    private readonly DeviceFinder _finder;
    private XElement _deviceList = new("MTouch");
    private XElement _simulatorList = new("MTouch", new XElement("Simulator"));
    private string _simulatorCatalog = "{\"devices\":{}}";

    public HardwareDeviceDiscoveryTests()
    {
        _processManager.Setup(p => p.RunAsync(It.IsAny<Process>(), It.IsAny<MlaunchArguments>(), It.IsAny<ILog>(), It.IsAny<TimeSpan?>(), It.IsAny<Dictionary<string, string?>>(), It.IsAny<int>(), It.IsAny<CancellationToken?>(), It.IsAny<bool?>()))
            .Returns<Process, MlaunchArguments, ILog, TimeSpan?, Dictionary<string, string?>, int, CancellationToken?, bool?>((process, args, log, timeout, environment, verbosity, token, diagnostics) =>
                WriteList(args.OfType<ListDevicesArgument>().Single(), _deviceList));
        _processManager.Setup(p => p.ExecuteCommandAsync(It.IsAny<MlaunchArguments>(), It.IsAny<ILog>(), It.IsAny<TimeSpan>(), It.IsAny<Dictionary<string, string?>>(), It.IsAny<int>(), It.IsAny<CancellationToken?>()))
            .Returns<MlaunchArguments, ILog, TimeSpan, Dictionary<string, string?>, int, CancellationToken?>((args, log, timeout, environment, verbosity, token) =>
                WriteList(args.OfType<ListSimulatorsArgument>().Single(), _simulatorList));
        _processManager.Setup(p => p.ExecuteXcodeCommandAsync("simctl", It.IsAny<IList<string>>(), It.IsAny<ILog>(), It.IsAny<ILog>(), It.IsAny<ILog>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns<string, IList<string>, ILog, ILog, ILog, TimeSpan, CancellationToken>((command, args, log, stdout, stderr, timeout, token) =>
            {
                Assert.Equal(new[] { "list", "devices", "--json" }, args);
                stdout.WriteLine(_simulatorCatalog);
                return Task.FromResult(new ProcessExecutionResult { ExitCode = 0 });
            });

        _hardwareLoader = new HardwareDeviceLoader(_processManager.Object);
        _finder = new DeviceFinder(_hardwareLoader, new SimulatorLoader(_processManager.Object));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MixedTvListingSelectsPhysicalDeviceRegardlessOfOrder(bool simulatorFirst)
    {
        var simulator = DeviceRecord(SimulatorId, SimulatorName);
        simulator.Add(
            new XElement("BuildVersion", "24J5325d"),
            new XElement("CPUArchitecture", "arm64"),
            new XElement("HardwareModel", "J255"));
        var physical = DeviceRecord(PhysicalId, PhysicalName);
        _deviceList = new XElement("MTouch", simulatorFirst ? new[] { simulator, physical } : new[] { physical, simulator });
        _simulatorCatalog = SimulatorCatalog(SimulatorId);

        var selected = await _finder.FindDevice(new TestTargetOs(TestTarget.Device_tvOS, null), null, _log);

        Assert.Equal(PhysicalId, selected.Device.UDID);
        Assert.Equal(PhysicalId, Assert.Single(_hardwareLoader.ConnectedDevices).UDID);
        Assert.Equal(PhysicalId, Assert.Single(_hardwareLoader.ConnectedTV).UDID);
        Assert.Equal(PhysicalId, (await _hardwareLoader.FindDevice(RunMode.TvOS, _log, includeLocked: false)).UDID);
        Assert.Contains($"Skipping device {SimulatorName} ({SimulatorId}) because it's a simulator.", _log.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(SimulatorName)]
    [InlineData(SimulatorId)]
    public async Task SimulatorOnlyListingCannotSatisfyPhysicalDeviceRequest(string? deviceName)
    {
        _deviceList = new XElement("MTouch", DeviceRecord(SimulatorId, SimulatorName));
        _simulatorList = SimulatorList(SimulatorId);
        _simulatorCatalog = SimulatorCatalog(SimulatorId);

        await Assert.ThrowsAsync<NoDeviceFoundException>(() =>
            _finder.FindDevice(new TestTargetOs(TestTarget.Device_tvOS, null), deviceName, _log));
        await Assert.ThrowsAsync<NoDeviceFoundException>(() =>
            _hardwareLoader.FindDevice(RunMode.TvOS, _log, includeLocked: false));
        Assert.Empty(_hardwareLoader.ConnectedDevices);

        var simulator = await _finder.FindDevice(new TestTargetOs(TestTarget.Simulator_tvOS, null), SimulatorId, _log);
        Assert.Equal(SimulatorId, simulator.Device.UDID);
    }

    [Fact]
    public async Task ClassificationUsesSimulatorIdentifiersNotNamesOrModels()
    {
        _deviceList = new XElement("MTouch",
            DeviceRecord(SimulatorId, PhysicalName),
            DeviceRecord(PhysicalId, SimulatorName));
        _simulatorCatalog = SimulatorCatalog(SimulatorId.ToLowerInvariant());

        var selected = await _finder.FindDevice(new TestTargetOs(TestTarget.Device_tvOS, null), SimulatorName, _log);

        Assert.Equal(PhysicalId, selected.Device.UDID);
        Assert.Equal(PhysicalId, Assert.Single(_hardwareLoader.ConnectedDevices).UDID);
    }

    [Theory]
    [InlineData(DeviceClass.iPhone, "iPhone12,1", DevicePlatform.iOS)]
    [InlineData(DeviceClass.iPad, "iPad2,1", DevicePlatform.iOS)]
    [InlineData(DeviceClass.iPod, "iPod7,1", DevicePlatform.iOS)]
    [InlineData(DeviceClass.AppleTV, "AppleTV14,1", DevicePlatform.tvOS)]
    [InlineData(DeviceClass.Watch, "Watch3,1", DevicePlatform.watchOS)]
    [InlineData(DeviceClass.Watch, "Watch4,1", DevicePlatform.watchOS)]
    [InlineData(DeviceClass.xrOS, "RealityDevice14,1", DevicePlatform.xrOS)]
    public async Task LegacyPhysicalDevicesRemainWithoutOptionalMetadata(DeviceClass deviceClass, string productType, DevicePlatform platform)
    {
        var physical = DeviceRecord(PhysicalId, SimulatorName, deviceClass, productType);
        physical.Element("IsUsableForDebugging")!.Remove();
        _deviceList = new XElement("MTouch", DeviceRecord(SimulatorId, PhysicalName, deviceClass, productType), physical);
        _simulatorCatalog = SimulatorCatalog(SimulatorId);

        await _hardwareLoader.LoadDevices(_log);

        var device = Assert.Single(_hardwareLoader.ConnectedDevices);
        Assert.Equal(PhysicalId, device.UDID);
        Assert.Equal(platform, device.DevicePlatform);
        Assert.Equal("Wifi", device.InterfaceType);
        Assert.Null(device.IsUsableForDebugging);
    }

    [Fact]
    public async Task ForceRefreshAlsoRefreshesSimulatorClassification()
    {
        _deviceList = new XElement("MTouch", DeviceRecord(PhysicalId, PhysicalName));
        await _hardwareLoader.LoadDevices(_log);
        Assert.Equal(PhysicalId, Assert.Single(_hardwareLoader.ConnectedDevices).UDID);

        _deviceList.Add(DeviceRecord(SimulatorId, SimulatorName));
        _simulatorCatalog = SimulatorCatalog(SimulatorId);
        await _hardwareLoader.LoadDevices(_log, forceRefresh: true);
        await _hardwareLoader.LoadDevices(_log);

        Assert.Equal(PhysicalId, Assert.Single(_hardwareLoader.ConnectedDevices).UDID);
        _processManager.Verify(p => p.ExecuteXcodeCommandAsync("simctl", It.IsAny<IList<string>>(), It.IsAny<ILog>(), It.IsAny<ILog>(), It.IsAny<ILog>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task UnavailableSimulatorsAreAlsoExcluded()
    {
        _deviceList = new XElement("MTouch", DeviceRecord(SimulatorId, SimulatorName));
        _simulatorCatalog = SimulatorCatalog(SimulatorId, isAvailable: false);

        await _hardwareLoader.LoadDevices(_log);

        Assert.Empty(_hardwareLoader.ConnectedDevices);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SimulatorListingFailureDoesNotExposeUnclassifiedDevices(bool timedOut)
    {
        _deviceList = new XElement("MTouch", DeviceRecord(SimulatorId, SimulatorName));
        _processManager.Setup(p => p.ExecuteXcodeCommandAsync("simctl", It.IsAny<IList<string>>(), It.IsAny<ILog>(), It.IsAny<ILog>(), It.IsAny<ILog>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult { ExitCode = timedOut ? 0 : 1, TimedOut = timedOut });

        var error = await Assert.ThrowsAsync<Exception>(() => _hardwareLoader.LoadDevices(_log));

        Assert.Contains(timedOut ? "simctl timed out" : "simctl exited with code 1", error.Message);
        Assert.Empty(_hardwareLoader.ConnectedDevices);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{invalid")]
    [InlineData("{\"devices\":{\"runtime\":[{\"udid\":null}]}}")]
    [InlineData("{\"devices\":{\"runtime\":[{\"udid\":\"\"}]}}")]
    public async Task InvalidSimulatorCatalogDoesNotExposeUnclassifiedDevices(string catalog)
    {
        _deviceList = new XElement("MTouch", DeviceRecord(SimulatorId, SimulatorName));
        _simulatorCatalog = catalog;

        await Assert.ThrowsAnyAsync<JsonException>(() => _hardwareLoader.LoadDevices(_log));

        Assert.Empty(_hardwareLoader.ConnectedDevices);
    }

    [Fact]
    public async Task CancellationIsPassedToSimulatorDiscovery()
    {
        _deviceList = new XElement("MTouch", DeviceRecord(SimulatorId, SimulatorName));
        using var cancellation = new CancellationTokenSource();
        _processManager.Setup(p => p.ExecuteXcodeCommandAsync("simctl", It.IsAny<IList<string>>(), It.IsAny<ILog>(), It.IsAny<ILog>(), It.IsAny<ILog>(), It.IsAny<TimeSpan>(), cancellation.Token))
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));

        var error = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _hardwareLoader.LoadDevices(_log, cancellationToken: cancellation.Token));

        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Empty(_hardwareLoader.ConnectedDevices);
    }

    [Fact]
    public async Task EmptyHardwareListingDoesNotRequireSimulatorDiscovery()
    {
        await _hardwareLoader.LoadDevices(_log);

        Assert.Empty(_hardwareLoader.ConnectedDevices);
        _processManager.Verify(p => p.ExecuteXcodeCommandAsync("simctl", It.IsAny<IList<string>>(), It.IsAny<ILog>(), It.IsAny<ILog>(), It.IsAny<ILog>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static XElement DeviceRecord(string identifier, string name, DeviceClass deviceClass = DeviceClass.AppleTV, string productType = "AppleTV14,1") =>
        new("Device",
            new XElement("DeviceIdentifier", identifier),
            new XElement("Name", name),
            new XElement("DeviceClass", deviceClass),
            new XElement("ProductType", productType),
            new XElement("ProductVersion", "27.0"),
            new XElement("InterfaceType", "Wifi"),
            new XElement("IsPaired", "True"),
            new XElement("IsUsableForDebugging", "True"));

    private static XElement SimulatorList(string identifier) =>
        new("MTouch",
            new XElement("Simulator",
                new XElement("AvailableDevices",
                    new XElement("SimDevice",
                        new XAttribute("UDID", identifier),
                        new XAttribute("Name", SimulatorName),
                        new XAttribute("State", "Booted"),
                        new XElement("SimRuntime", "com.apple.CoreSimulator.SimRuntime.tvOS-27-0"),
                        new XElement("SimDeviceType", "com.apple.CoreSimulator.SimDeviceType.Apple-TV-4K-3rd-generation-1080p"),
                        new XElement("DataPath", "/simulator/data"),
                        new XElement("LogPath", "/simulator/logs")))));

    private static string SimulatorCatalog(string identifier, bool isAvailable = true) =>
        $$"""
        {
          "devices": {
            "com.apple.CoreSimulator.SimRuntime.tvOS-27-0": [
              { "udid": "{{identifier}}", "isAvailable": {{(isAvailable ? "true" : "false")}} }
            ]
          }
        }
        """;

    private static Task<ProcessExecutionResult> WriteList(MlaunchArgument argument, XElement devices)
    {
        var tempPath = argument.AsCommandLineArgument();
        tempPath = tempPath.Substring(tempPath.IndexOf('=') + 1).Replace("\"", string.Empty);
        File.WriteAllText(tempPath, devices.ToString());
        return Task.FromResult(new ProcessExecutionResult { ExitCode = 0 });
    }
}
