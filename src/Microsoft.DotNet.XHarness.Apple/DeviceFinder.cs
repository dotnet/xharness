// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

namespace Microsoft.DotNet.XHarness.Apple;

public interface IDeviceFinder
{
    Task<(IDevice Device, IDevice? CompanionDevice)> FindDevice(TestTargetOs target, string? deviceName, ILog log, bool includeWirelessDevices = true);
}

public class DeviceFinder : IDeviceFinder
{
    private readonly IHardwareDeviceLoader _deviceLoader;
    private readonly ISimulatorLoader _simulatorLoader;

    public DeviceFinder(IHardwareDeviceLoader deviceLoader, ISimulatorLoader simulatorLoader)
    {
        _deviceLoader = deviceLoader ?? throw new ArgumentNullException(nameof(deviceLoader));
        _simulatorLoader = simulatorLoader ?? throw new ArgumentNullException(nameof(simulatorLoader));
    }

    public async Task<(IDevice Device, IDevice? CompanionDevice)> FindDevice(TestTargetOs target, string? deviceName, ILog log, bool includeWirelessDevices = true)
    {
        IDevice? device;
        IDevice? companionDevice = null;

        bool IsMatchingDevice(IDevice device) =>
            device.Name.Equals(deviceName, StringComparison.InvariantCultureIgnoreCase) ||
            device.UDID.Equals(deviceName, StringComparison.InvariantCultureIgnoreCase);

        if (target.Platform.IsSimulator())
        {
            if (deviceName == null)
            {
                (device, companionDevice) = await _simulatorLoader.FindSimulators(target, log, 3);
            }
            else
            {
                await _simulatorLoader.LoadDevices(log, includeLocked: false);

                device = _simulatorLoader.AvailableDevices.FirstOrDefault(IsMatchingDevice)
                    ?? throw new NoDeviceFoundException($"Failed to find a simulator '{deviceName}'");
            }
        }
        else
        {
            // The DeviceLoader.FindDevice will return the fist device of the type, but we want to make sure that
            // the device we use is of the correct arch, therefore, we will use the LoadDevices and handpick one
            await _deviceLoader.LoadDevices(log, includeLocked: false, forceRefresh: false, includeWirelessDevices: includeWirelessDevices);

            if (deviceName == null)
            {

                IHardwareDevice? hardwareDevice = target.Platform switch
                {
                    TestTarget.Simulator_iOS32 => _deviceLoader.Connected32BitIOS.FirstOrDefault(),
                    TestTarget.Device_iOS => _deviceLoader.Connected64BitIOS.FirstOrDefault(),
                    TestTarget.Device_tvOS => _deviceLoader.ConnectedTV.FirstOrDefault(),
                    _ => throw new ArgumentOutOfRangeException(nameof(target), $"Unrecognized device platform {target.Platform}")
                };

                if (target.Platform.IsWatchOSTarget() && hardwareDevice != null)
                {
                    companionDevice = await _deviceLoader.FindCompanionDevice(log, hardwareDevice);
                }

                device = hardwareDevice;
            }
            else
            {
                device = _deviceLoader.ConnectedDevices.FirstOrDefault(IsMatchingDevice)
                    ?? throw new NoDeviceFoundException($"Failed to find a device '{deviceName}'. " +
                                                        "Please make sure the device is connected and unlocked.");
            }
        }

        if (device == null)
        {
            throw new NoDeviceFoundException($"Failed to find a suitable device for target {target.AsString()}");
        }

        return (device, companionDevice);
    }
}
