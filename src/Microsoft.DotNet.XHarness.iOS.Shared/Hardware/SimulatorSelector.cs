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
            TestTarget.Simulator_iOS64 => "com.apple.CoreSimulator.SimRuntime.iOS-",
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
            TestTarget.Simulator_iOS64 => GetiOSDeviceType(target, minVersion),
            TestTarget.Simulator_tvOS => GetTvOSDeviceType(target),
            TestTarget.Simulator_watchOS => "com.apple.CoreSimulator.SimDeviceType." + (minVersion ? "Apple-Watch-38mm" : "Apple-Watch-Series-5-40mm"),
            TestTarget.Simulator_xrOS => "com.apple.CoreSimulator.SimDeviceType.Apple-Vision-Pro",
            _ => throw new Exception(string.Format("Invalid simulator target: {0}", target))
        };
    }

    private static string GetiOSDeviceType(TestTargetOs target, bool minVersion)
    {
        // The min-version tests run on the lowest simulator runtime the installed Xcode ships (which can be
        // newer than the minimum deployment target we build against). iPhone 6s was dropped in iOS 16, so it
        // can no longer be created on an iOS 16+ runtime; use a still-available small device for those. Older
        // runtimes (where iPhone 6s is still valid) keep using it, so behavior there is unchanged.
        if (minVersion && Version.TryParse(target.OSVersion, out var version) && version.Major >= 16)
        {
            return "com.apple.CoreSimulator.SimDeviceType.iPhone-SE-3rd-generation";
        }

        return "com.apple.CoreSimulator.SimDeviceType." + (minVersion ? "iPhone-6s" : "iPhone-11-Pro");
    }

    private static string GetTvOSDeviceType(TestTargetOs target)
    {
        // tvOS 27 removed the legacy non-4K "Apple TV" (Apple-TV-1080p) simulator device type; those
        // runtimes only support the Apple TV 4K device types. Keep using the legacy device type for older
        // tvOS versions (everything before Xcode 27) so behavior is unchanged there. The caller resolves
        // target.OSVersion to the actual runtime version before calling this, so it's set for the simulator
        // we'll create; if it's somehow missing we fall back to the legacy device type.
        if (Version.TryParse(target.OSVersion, out var version) && version.Major >= 27)
        {
            return "com.apple.CoreSimulator.SimDeviceType.Apple-TV-4K-3rd-generation-4K";
        }

        return "com.apple.CoreSimulator.SimDeviceType.Apple-TV-1080p";
    }

    public virtual void GetCompanionRuntimeAndDeviceType(TestTargetOs target, bool minVersion, out string? companionRuntime, out string? companionDeviceType)
    {
        if (target.Platform == TestTarget.Simulator_watchOS)
        {
            companionRuntime = "com.apple.CoreSimulator.SimRuntime.iOS-" + (minVersion ? SdkVersions.MinWatchOSCompanionSimulator : SdkVersions.MaxWatchOSCompanionSimulator).Replace('.', '-');
            companionDeviceType = "com.apple.CoreSimulator.SimDeviceType." + (minVersion ? "iPhone-6s" : "iPhone-11-Pro");
        }
        else
        {
            companionRuntime = null;
            companionDeviceType = null;
        }
    }

    public ISimulatorDevice SelectSimulator(IEnumerable<ISimulatorDevice> simulators)
    {
        // Put Booted/Booting in front of Shutdown/Unknown
        return simulators.OrderByDescending(s => s.State).First();
    }
}
