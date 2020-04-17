// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.iOS.Shared.Hardware
{

    public enum Architecture
    {
        ARMv6,
        ARMv7,
        ARMv7k,
        ARMv7s,
        ARM64,
        ARM64_32,
        i386,
        x86_64,
    }

    public enum DevicePlatform
    {
        Unknown,
        iOS,
        tvOS,
        watchOS,
        macOS,
    }

    public interface IDevice
    {
        string Name { get; }
        string UDID { get; }
        string OSVersion { get; }
    }
}
