// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Hardware;

public interface ISimulatorSelector
{
    string GetRuntimePrefix(TestTargetOs target);
    string GetDeviceType(TestTargetOs target, bool minVersion);
    void GetCompanionRuntimeAndDeviceType(TestTargetOs target, bool minVersion, out string? companionRuntime, out string? companionDeviceType);
    ISimulatorDevice SelectSimulator(IEnumerable<ISimulatorDevice> simulators);
}

public class DefaultSimulatorSelector : ISimulatorSelector
{
    public virtual string GetRuntimePrefix(TestTargetOs target)
    {
        return target.Platform switch
        {
            TestTarget.Simulator_iOS32 => "com.apple.CoreSimulator.SimRuntime.iOS-",
            TestTarget.Simulator_iOS64 => "com.apple.CoreSimulator.SimRuntime.iOS-",
            TestTarget.Simulator_iOS => "com.apple.CoreSimulator.SimRuntime.iOS-",
            TestTarget.Simulator_tvOS => "com.apple.CoreSimulator.SimRuntime.tvOS-",
            TestTarget.Simulator_watchOS => "com.apple.CoreSimulator.SimRuntime.watchOS-",
            TestTarget.Simulator_xrOS => "com.apple.CoreSimulator.SimRuntime.xrOS-",
            _ => throw new Exception(string.Format("Invalid simulator target: {0}", target))
        };
    }

    public virtual string GetDeviceType(TestTargetOs target, bool minVersion)
    {
        return target.Platform switch
        {
            TestTarget.Simulator_iOS => "com.apple.CoreSimulator.SimDeviceType.iPhone-5s",
            TestTarget.Simulator_iOS32 => "com.apple.CoreSimulator.SimDeviceType.iPhone-5s",
            TestTarget.Simulator_iOS64 => GetiOSDeviceType(Version.Parse(target.OSVersion)),
            TestTarget.Simulator_tvOS => "com.apple.CoreSimulator.SimDeviceType.Apple-TV-1080p",
            TestTarget.Simulator_watchOS => GetWatchOSDeviceType(Version.Parse(target.OSVersion)),
            _ => throw new Exception(string.Format("Invalid simulator target: {0}", target))
        };
    }

    public virtual void GetCompanionRuntimeAndDeviceType(TestTargetOs target, bool minVersion, out string? companionRuntime, out string? companionDeviceType)
    {
        companionRuntime = null;
        companionDeviceType = null;

        if (target.Platform == TestTarget.Simulator_watchOS)
        {
            var companionVersion = minVersion ? SdkVersions.MinWatchOSCompanionSimulator : SdkVersions.MaxWatchOSCompanionSimulator;
            companionRuntime = "com.apple.CoreSimulator.SimRuntime.iOS-" + companionVersion.Replace('.', '-');
            companionDeviceType = GetiOSDeviceType(Version.Parse(companionVersion));
        }
    }

    public ISimulatorDevice SelectSimulator(IEnumerable<ISimulatorDevice> simulators)
    {
        // Put Booted/Booting in front of Shutdown/Unknown
        return simulators.OrderByDescending(s => s.State).First();
    }

    string GetiOSDeviceType(Version iOSVersion)
    {
        if (iOSVersion.Major < 13)
            return "com.apple.CoreSimulator.SimDeviceType.iPhone-7";
        if (iOSVersion.Major < 14)
            return "com.apple.CoreSimulator.SimDeviceType.iPhone-8";
        if (iOSVersion.Major < 15)
            return "com.apple.CoreSimulator.SimDeviceType.iPhone-X";
        if (iOSVersion.Major < 16)
            return "com.apple.CoreSimulator.SimDeviceType.iPhone-11";

        return "com.apple.CoreSimulator.SimDeviceType.iPhone-14";
    }

    string GetWatchOSDeviceType(Version watchOSVersion)
    {
        if (watchOSVersion.Major < 7)
            return "com.apple.CoreSimulator.SimDeviceType.Apple-Watch-Series-3-38mm";
        if (watchOSVersion.Major < 8)
            return "com.apple.CoreSimulator.SimDeviceType.Apple-Watch-Series-4-40mm";
        return "com.apple.CoreSimulator.SimDeviceType.Apple-Watch-Series-7-41mm";
    }
}
