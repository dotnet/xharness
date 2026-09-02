// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.XHarness.Android;

public record AndroidDevice
{
    public string DeviceSerial { get; init; }

    public int? ApiVersion { get; set; } = null;

    public string? Architecture { get; set; } = null;

    public IReadOnlyCollection<string>? SupportedArchitectures { get; set; } = null;

    public IReadOnlyCollection<string>? InstalledApplications { get; set; } = null;

    public string? Manufacturer { get; set; } = null;

    public string? Model { get; set; } = null;

    public string? ProductName { get; set; } = null;

    public string? OperatingSystemVersion { get; set; } = null;

    public string? BuildFingerprint { get; set; } = null;

    public string? CpuModel { get; set; } = null;

    public long? CpuMaxFrequencyKiloHertz { get; set; } = null;

    public long? TotalMemoryBytes { get; set; } = null;

    public bool IsEmulator => DeviceSerial.StartsWith("emulator", StringComparison.OrdinalIgnoreCase);

    public AndroidDevice(string deviceSerial) => DeviceSerial = deviceSerial;
}
