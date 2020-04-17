// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.XHarness.iOS.Shared
{
    public enum TestTarget
    {
        None,
        Simulator_iOS,
        Simulator_iOS32,
        Simulator_iOS64,
        Simulator_tvOS,
        Simulator_watchOS,
        Device_iOS,
        Device_tvOS,
        Device_watchOS,
    }

    public static class TestTargetExtensions
    {
        public static readonly Dictionary<string, TestTarget> TestTargetNames = new Dictionary<string, TestTarget>
        {
            { "ios-simulator", TestTarget.Simulator_iOS },
            { "ios-simulator-32", TestTarget.Simulator_iOS32 },
            { "ios-simulator-64", TestTarget.Simulator_iOS64 },
            { "tvos-simulator", TestTarget.Simulator_tvOS },
            { "watchos-simulator", TestTarget.Simulator_watchOS },
            { "ios-device", TestTarget.Device_iOS },
            { "tvos-device", TestTarget.Device_tvOS },
            { "watchos-device", TestTarget.Device_watchOS },
        };

        private static readonly Dictionary<TestTarget, string> s_testTargetStrings = TestTargetNames.ToDictionary(x => x.Value, x => x.Key);

        public static string ToFriendlyString(this TestTarget target)
        {
            if (s_testTargetStrings.TryGetValue(target, out string name))
            {
                return name;
            }

            throw new ArgumentOutOfRangeException($"Unknown target: {target}");
        }

        public static RunMode ToRunMode(this TestTarget target) => target switch
        {
            TestTarget.Simulator_iOS => RunMode.Classic,
            TestTarget.Simulator_iOS32 => RunMode.Sim32,
            TestTarget.Simulator_iOS64 => RunMode.Sim64,
            TestTarget.Simulator_tvOS => RunMode.TvOS,
            TestTarget.Simulator_watchOS => RunMode.WatchOS,
            TestTarget.Device_iOS => RunMode.iOS,
            TestTarget.Device_tvOS => RunMode.TvOS,
            TestTarget.Device_watchOS => RunMode.WatchOS,
            _ => throw new ArgumentOutOfRangeException($"Unknown target: {target}"),
        };

        public static bool IsSimulator(this TestTarget target) => target switch
        {
            TestTarget.Simulator_iOS => true,
            TestTarget.Simulator_iOS32 => true,
            TestTarget.Simulator_iOS64 => true,
            TestTarget.Simulator_tvOS => true,
            TestTarget.Simulator_watchOS => true,
            TestTarget.Device_iOS => false,
            TestTarget.Device_tvOS => false,
            TestTarget.Device_watchOS => false,
            _ => throw new ArgumentOutOfRangeException($"Unknown target: {target}"),
        };

        public static bool IsWatchOSTarget(this TestTarget target) => target switch
        {
            TestTarget.Simulator_watchOS => true,
            TestTarget.Device_watchOS => true,
            _ => false,
        };
    }
}
